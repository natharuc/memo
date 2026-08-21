using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Memo.Service.Rail;
using Newtonsoft.Json.Linq;

namespace Memo.Service.Notificacoes
{
    /// <summary>
    /// Escuta comandos do Telegram (long-polling <c>getUpdates</c>) enquanto o Memo
    /// está na bandeja e controla o <b>Memo Rail</b> por <b>linguagem natural</b>
    /// (sem emojis, sem botões): ver, criar, concluir, reordenar, reagendar,
    /// renomear e remover tarefas. Se não entender, responde "não entendi".
    ///
    /// Segurança: só obedece o <see cref="CanalTelegram.ChatId"/> configurado. Mexe
    /// apenas no Rail (não é segredo); nunca toca no cofre nem em documentos. Opt-in
    /// (<see cref="CanalTelegram.OuvirComandos"/>).
    /// </summary>
    public class TelegramBotListener
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        private const int PollTimeoutSegundos = 25;

        private readonly NotificacaoService _notificacoes = new NotificacaoService();
        private readonly RailService _rail = new RailService();

        private Thread _thread;
        private volatile bool _rodando;

        // ----------------- ciclo de vida -----------------

        public void Iniciar()
        {
            if (_rodando) return;
            _rodando = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "TelegramBotListener" };
            _thread.Start();
        }

        public void Parar() => _rodando = false;

        private void Loop()
        {
            long offset = 0;
            string tokenAtivo = null;

            while (_rodando)
            {
                try
                {
                    var cfg = _notificacoes.Carregar().Telegram;
                    if (cfg == null || !cfg.OuvirComandos ||
                        string.IsNullOrWhiteSpace(cfg.BotToken) || string.IsNullOrWhiteSpace(cfg.ChatId))
                    {
                        Dormir(5);
                        continue;
                    }

                    var token = cfg.BotToken.Trim();
                    if (token != tokenAtivo)
                    {
                        // Bot novo/trocado: zera o offset, remove webhook (mutuamente
                        // exclusivo com getUpdates) e descarta updates pendentes — não
                        // reexecuta comandos que chegaram com o Memo fechado.
                        tokenAtivo = token;
                        offset = 0;
                        ChamarApi(token, "deleteWebhook",
                            new Dictionary<string, string> { ["drop_pending_updates"] = "true" });
                    }

                    var resp = ChamarApi(token, "getUpdates", new Dictionary<string, string>
                    {
                        ["offset"] = offset.ToString(CultureInfo.InvariantCulture),
                        ["timeout"] = PollTimeoutSegundos.ToString(CultureInfo.InvariantCulture),
                        ["allowed_updates"] = "[\"message\"]"
                    });

                    if (!(resp?["result"] is JArray updates))
                    {
                        Dormir(3);
                        continue;
                    }

                    var chatAutorizado = cfg.ChatId.Trim();
                    foreach (var up in updates)
                    {
                        offset = Math.Max(offset, (long)up["update_id"] + 1);
                        try { ProcessarUpdate(token, chatAutorizado, up); }
                        catch { /* um update ruim não derruba o loop */ }
                    }
                }
                catch
                {
                    Dormir(3);
                }
            }
        }

        private void Dormir(int segundos)
        {
            for (var i = 0; i < segundos && _rodando; i++) Thread.Sleep(1000);
        }

        private void ProcessarUpdate(string token, string chatAutorizado, JToken up)
        {
            var msg = up["message"];
            if (msg == null) return;

            var chatId = msg["chat"]?["id"]?.ToString();
            if (chatId != chatAutorizado) return; // só o dono comanda o bot

            var texto = msg["text"]?.ToString();
            if (string.IsNullOrWhiteSpace(texto)) return;

            var resposta = Interpretar(texto.Trim());
            Enviar(token, chatId, resposta);
        }

        // ================= interpretação (linguagem natural) =================
        //
        // Matriz de decisão: cada intenção é detectada por palavras-chave sobre o
        // texto NORMALIZADO (minúsculo, sem acento). A ordem importa (a 1ª que casa
        // vence). Números referenciam a numeração exibida em "minhas missões".

        private string Interpretar(string original)
        {
            // "/comando" e "/comando@bot" viram texto comum ("/done 2" -> "done 2").
            original = Regex.Replace(original, @"^/(\w+)(@\w+)?", "$1").Trim();
            var norm = Normalizar(original);

            // --- ajuda / saudação ---
            if (Casa(norm, @"^(ajuda|help|comandos|start|como funciona|o que voce faz|o que vc faz|menu)\b"))
                return Ajuda();
            if (Casa(norm, @"^(oi|ola|opa|eai|e ai|bom dia|boa tarde|boa noite|hey|hi|hello)\b"))
                return "Olá! " + Lista();

            // --- listar ---
            if (EhListar(norm))
                return Lista();

            // --- limpar o dia (antes de "remover", que exige número) ---
            if (Casa(norm, @"\b(limpa|limpar|limpe|apagar tudo|apaga tudo|zera|zerar|resetar|reseta|recomecar|recomeca)\b")
                && Casa(norm, @"\b(hoje|dia|tudo)\b"))
            {
                _rail.LimparHoje();
                return "Tarefas de hoje apagadas (as atrasadas continuam).";
            }

            // --- criar (verbo no início; o resto é o texto, com data e link auto) ---
            var criar = Regex.Match(norm,
                @"^(nova|novo|criar|crie|cria|adicionar|adiciona|adicione|add|anotar|anota|anote|registrar|registra)\b\s*(missao|missoes|tarefa|tarefas|item)?\s*[:\-]?\s*",
                RegexOptions.IgnoreCase);
            if (!criar.Success)
                criar = Regex.Match(norm, @"^(missao|tarefa)\s*:\s*", RegexOptions.IgnoreCase);
            if (criar.Success)
                return Criar(Fatiar(original, norm, criar.Length).Trim());

            // --- reabrir (antes de "concluir": "não terminei" contém verbo de concluir) ---
            if (Casa(norm, @"\b(reabrir|reabre|reabra|desmarcar|desmarca|desmarque|desfazer|desfaz|reverter|reverte)\b")
                || Casa(norm, @"\bnao (fiz|terminei|conclui|completei|acabei)\b"))
                return ComNumero(norm, Reabrir);

            // --- concluir ---
            if (Casa(norm, @"\b(concluir|conclui|concluida|concluido|finalizar|finaliza|finalizei|terminar|termina|terminei|acabei|completar|completa|completei|fiz|feito|feita|done|pronto|pronta|check|marcar|marca)\b")
                || Casa(norm, @"^ok\b"))
                return ComNumero(norm, Concluir);

            // --- reordenar: topo / final / cima / baixo ---
            if (Casa(norm, @"\b(prioriza|priorizar|priorize|prioridade|primeiro|no topo|pro topo|para o topo|topo|mais importante|urgente)\b"))
                return ComNumero(norm, n => MoverExtremo(n, subir: true, "Priorizada"));
            if (Casa(norm, @"\b(ultimo|por ultimo|pro final|para o final|final|menos importante)\b"))
                return ComNumero(norm, n => MoverExtremo(n, subir: false, "Movida para o fim"));
            if (Casa(norm, @"\b(sobe|subir|suba|acima|(pra|para) cima|antes)\b"))
                return ComNumero(norm, n => MoverUm(n, subir: true));
            if (Casa(norm, @"\b(desce|descer|desca|abaixo|(pra|para) baixo|depois)\b"))
                return ComNumero(norm, n => MoverUm(n, subir: false));

            // --- mudar: reagendar (data) ou renomear (texto) — "verbo N para RESTO" ---
            var mudar = Regex.Match(norm,
                @"\b(adiar|adie|remarcar|remarca|reagendar|reagenda|passar|passa|renomear|renomeie|editar|edita|edite|corrigir|corrige|alterar|altera|altere|trocar|troca|troque|mudar|muda|mude|mover|mova|jogar|joga)\b.*?(\d+)\s*(?:para|pra|pro|por|no dia|em|:|->)\s*(.+)$",
                RegexOptions.IgnoreCase);
            if (mudar.Success)
                return Mudar(mudar.Groups[1].Value, int.Parse(mudar.Groups[2].Value),
                    Fatiar(original, norm, mudar.Groups[3].Index).Trim(), mudar.Groups[3].Value.Trim());

            // --- remover ---
            if (Casa(norm, @"\b(remover|remove|remova|excluir|exclui|exclua|apagar|apaga|apague|deletar|deleta|delete|tirar|tira|tire|cancelar|cancela|cancele)\b"))
                return ComNumero(norm, Remover);

            // --- link: "linkar 2 https://..." / "link na 2: https://..." ---
            if (Casa(norm, @"\b(link|linkar|linka|vincular|vincula|url)\b"))
            {
                var url = Regex.Match(original, @"https?://\S+", RegexOptions.IgnoreCase);
                var num = PrimeiroNumero(norm);
                if (num != null && url.Success) return DefinirLink(num.Value, url.Value);
            }

            return NaoEntendi();
        }

        private static bool EhListar(string norm)
        {
            if (Casa(norm, @"^(rail|status|lista|listar|missoes|missao|tarefas|pendencias|pendentes|agenda)\b")) return true;
            if (Casa(norm, @"\bminhas? (missoes|missao|tarefas|tarefa)\b")) return true;
            if (Casa(norm, @"\b(quais|que|mostra|mostrar|ver|vendo|listar?|manda|traz)\b.*\b(missao|missoes|tarefa|tarefas|lista|pendencias)\b")) return true;
            if (Casa(norm, @"\bo que (eu )?(tenho|falta|preciso|resta)\b")) return true;
            return false;
        }

        // ----------------- ações -----------------

        private string Criar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "Diga o texto da missão. Ex.: nova missão: revisar PR amanhã.";

            var (semData, data) = ExtrairData(texto);
            var item = _rail.Adicionar(semData, link: null, data: data);
            if (item == null)
                return "Não entendi a missão. Ex.: nova missão: revisar PR amanhã.";

            var quando = item.Data != Hoje() ? $" para {DataCurta(item.Data)}" : string.Empty;
            var link = string.IsNullOrWhiteSpace(item.Link) ? string.Empty : $"\nLink: {Esc(item.Link)}";
            return $"Missão criada{quando}: \"{Fmt(item.Texto)}\".{link}";
        }

        private string Concluir(int n)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            _rail.ConcluirPorId(it.Id);
            var prox = _rail.MissaoAtual().ProximaPendente();
            return prox == null
                ? $"Concluída: \"{Fmt(it.Texto)}\". Missão do dia completa."
                : $"Concluída: \"{Fmt(it.Texto)}\". Próxima: \"{Fmt(prox.Texto)}\".";
        }

        private string Reabrir(int n)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            it.Concluido = false;
            it.ConcluidoEm = null;
            _rail.AtualizarItem(it);
            return $"Reaberta: \"{Fmt(it.Texto)}\".";
        }

        private string Remover(int n)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            _rail.RemoverItem(it.Id);
            return $"Removida: \"{Fmt(it.Texto)}\".";
        }

        private string MoverUm(int n, bool subir)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            if (!_rail.Mover(it.Id, subir))
                return subir ? "Já está no topo." : "Já está no fim.";
            return "Pronto.\n\n" + Lista();
        }

        private string MoverExtremo(int n, bool subir, string rotulo)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            while (_rail.Mover(it.Id, subir)) { }
            return $"{rotulo}: \"{Fmt(it.Texto)}\".\n\n" + Lista();
        }

        private string Mudar(string verbo, int n, string restoOriginal, string restoNorm)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);

            var textoVerbo = Verbo(verbo, "renomear", "renomeie", "editar", "edita", "edite", "corrigir", "corrige");
            var dataVerbo = Verbo(verbo, "adiar", "adie", "remarcar", "remarca", "reagendar", "reagenda");
            var data = ParseDataFlex(restoNorm);

            if (dataVerbo && data == null)
                return "Não entendi a data. Ex.: adiar 2 para amanhã (ou 25/12).";

            if (data != null && !textoVerbo)
            {
                it.Data = data.Value.ToString("yyyy-MM-dd");
                _rail.AtualizarItem(it);
                return $"Remarcada para {data.Value:dd/MM}: \"{Fmt(it.Texto)}\".";
            }

            if (string.IsNullOrWhiteSpace(restoOriginal))
                return "Diga o novo texto. Ex.: renomear 2 para revisar contrato.";

            it.Texto = restoOriginal;
            _rail.AtualizarItem(it);
            return $"Atualizada: \"{Fmt(restoOriginal)}\".";
        }

        private string DefinirLink(int n, string url)
        {
            var it = PorNumero(n);
            if (it == null) return NaoAchei(n);
            it.Link = url.Trim();
            _rail.AtualizarItem(it);
            return $"Link definido em \"{Fmt(it.Texto)}\": {Esc(it.Link)}";
        }

        // ----------------- listagem -----------------

        private string Lista()
        {
            var m = _rail.MissaoAtual();
            if (m.Lista.Count == 0)
                return "Nenhuma missão. Diga, por exemplo: nova missão: revisar PR amanhã.";

            var sb = new StringBuilder();
            sb.Append("<b>Missões</b>\n");
            var n = 0;

            void Secao(string titulo, List<ItemMissao> itens, bool comData)
            {
                if (itens.Count == 0) return;
                sb.Append('\n').Append("<b>").Append(titulo).Append("</b>\n");
                foreach (var it in itens)
                {
                    n++;
                    var caixa = it.Concluido ? "[x]" : "[ ]";
                    var txt = it.Concluido ? $"<s>{Fmt(it.Texto)}</s>" : Fmt(it.Texto);
                    var extra = comData ? $" ({DataCurta(it.Data)})" : string.Empty;
                    var link = string.IsNullOrWhiteSpace(it.Link) ? string.Empty : $" — {Esc(it.Link)}";
                    sb.Append($"{n}. {caixa} {txt}{extra}{link}\n");
                }
            }

            Secao("Atrasadas", m.Atrasadas, comData: true);
            Secao("Hoje", m.DeHoje, comData: false);
            Secao("Próximas", m.Futuras, comData: true);

            sb.Append($"\nResumo: {m.Concluidos}/{m.Ativas.Count} concluídas");
            if (m.Atrasadas.Count > 0) sb.Append($" · {m.Atrasadas.Count} atrasada(s)");
            return sb.ToString();
        }

        private static string Ajuda() =>
            "Sou o assistente da sua missão do dia. Fale naturalmente. Exemplos:\n\n" +
            "Ver: \"minhas missões\", \"o que tenho pra hoje\"\n" +
            "Criar: \"nova missão: revisar PR amanhã\", \"adiciona comprar café\"\n" +
            "Concluir: \"concluir 2\", \"terminei a 3\"\n" +
            "Reordenar: \"priorizar 2\", \"sobe a 3\", \"desce a 1\"\n" +
            "Reagendar: \"adiar 2 para amanhã\"\n" +
            "Renomear: \"renomear 2 para novo texto\"\n" +
            "Remover: \"remover 1\", \"apaga a 4\"\n" +
            "Limpar o dia: \"limpar hoje\"";

        private static string NaoEntendi() =>
            "Não entendi. Tente algo como: \"minhas missões\", \"nova missão: revisar PR amanhã\", " +
            "\"priorizar 2\", \"concluir 2\" ou \"remover 1\".";

        private static string NaoAchei(int n) =>
            $"Não achei a missão {n}. Diga \"minhas missões\" para ver os números.";

        // ----------------- helpers de intenção -----------------

        /// <summary>Extrai o primeiro número da frase e executa a ação; senão pede o número.</summary>
        private string ComNumero(string norm, Func<int, string> acao)
        {
            var n = PrimeiroNumero(norm);
            return n != null ? acao(n.Value)
                : "Qual missão? Diga o número, ex.: \"concluir 2\".";
        }

        private ItemMissao PorNumero(int n)
        {
            var lista = _rail.MissaoAtual().Lista;
            return (n >= 1 && n <= lista.Count) ? lista[n - 1] : null;
        }

        private static int? PrimeiroNumero(string norm)
        {
            var m = Regex.Match(norm, @"\d+");
            return m.Success && int.TryParse(m.Value, out var n) ? n : (int?)null;
        }

        private static bool Casa(string norm, string padrao) =>
            Regex.IsMatch(norm, padrao, RegexOptions.IgnoreCase);

        private static bool Verbo(string v, params string[] verbos)
        {
            foreach (var x in verbos) if (v == x) return true;
            return false;
        }

        // ----------------- data -----------------

        /// <summary>Extrai uma data do fim/meio do texto (hoje/amanhã/dd/MM/…) e a remove.</summary>
        private static (string Texto, DateTime? Data) ExtrairData(string texto)
        {
            var norm = Normalizar(texto);
            var m = Regex.Match(norm,
                @"\b(?:(?:pra|para|pro|no dia|dia|em)\s+)?(hoje|amanha|depois de amanha|\d{1,2}/\d{1,2}(?:/\d{2,4})?|\d{4}-\d{2}-\d{2})\b",
                RegexOptions.IgnoreCase);
            if (!m.Success) return (texto, null);

            var data = ParseDataFlex(m.Groups[1].Value);
            if (data == null) return (texto, null);

            var limpo = (texto.Length == norm.Length ? texto : norm).Remove(m.Index, m.Length);
            limpo = Regex.Replace(limpo, @"\s{2,}", " ").Trim(' ', ',', '-', ':');
            return (limpo, data);
        }

        /// <summary>hoje / amanhã / depois de amanhã / dd/MM / dd/MM/yyyy / yyyy-MM-dd.</summary>
        private static DateTime? ParseDataFlex(string texto)
        {
            var t = Normalizar(texto ?? string.Empty).Trim();
            if (t.Length == 0) return null;
            if (t == "hoje") return DateTime.Today;
            if (t == "amanha") return DateTime.Today.AddDays(1);
            if (t == "depois de amanha") return DateTime.Today.AddDays(2);
            return RailService.ParseData(t);
        }

        // ----------------- texto / Telegram -----------------

        private static string Hoje() => DateTime.Now.ToString("yyyy-MM-dd");

        private static string DataCurta(string yyyyMMdd) =>
            DateTime.TryParseExact(yyyyMMdd, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d) ? d.ToString("dd/MM") : yyyyMMdd;

        /// <summary>Só escapa HTML (para URLs e trechos crus).</summary>
        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty
            : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>Escapa HTML e converte a formatação leve (**negrito**, *itálico*).</summary>
        private static string Fmt(string texto)
        {
            var s = Esc(texto);
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<b>$1</b>", RegexOptions.Singleline);
            s = Regex.Replace(s, @"\*(.+?)\*", "<i>$1</i>", RegexOptions.Singleline);
            return s;
        }

        /// <summary>Minúsculo e sem acento, preservando o comprimento (índices batem com o original).</summary>
        private static string Normalizar(string s)
        {
            s = s.ToLowerInvariant();
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                switch (c)
                {
                    case 'á': case 'à': case 'â': case 'ã': case 'ä': sb.Append('a'); break;
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'í': case 'ì': case 'î': case 'ï': sb.Append('i'); break;
                    case 'ó': case 'ò': case 'ô': case 'õ': case 'ö': sb.Append('o'); break;
                    case 'ú': case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'ç': sb.Append('c'); break;
                    case 'ñ': sb.Append('n'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Fatia o original a partir de um índice do texto normalizado (comprimentos batem).</summary>
        private static string Fatiar(string original, string norm, int indice)
        {
            var baseTexto = original.Length == norm.Length ? original : norm;
            return indice >= 0 && indice <= baseTexto.Length ? baseTexto.Substring(indice) : string.Empty;
        }

        private void Enviar(string token, string chatId, string html)
        {
            ChamarApi(token, "sendMessage", new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = html,
                ["parse_mode"] = "HTML",
                ["disable_web_page_preview"] = "true"
            });
        }

        private JObject ChamarApi(string token, string metodo, Dictionary<string, string> p)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{token}/{metodo}";
                using (var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(p) })
                using (var resp = Http.Send(req))
                using (var reader = new StreamReader(resp.Content.ReadAsStream()))
                {
                    return JObject.Parse(reader.ReadToEnd());
                }
            }
            catch
            {
                return null; // rede/timeout/JSON inválido — o loop trata
            }
        }
    }
}
