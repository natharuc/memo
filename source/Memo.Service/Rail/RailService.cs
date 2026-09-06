using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Memo.Service.Rail
{
    /// <summary>
    /// Persistência da missão em <c>%LOCALAPPDATA%\Memo\rail.json</c> (formato v2:
    /// pool de tarefas com data). Não é segredo: não passa pelo cofre.
    /// Pendências acumulam como atrasadas; só concluídas antigas são podadas.
    /// </summary>
    public class RailService
    {
        private const int DiasHistoricoConcluidas = 14;

        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "rail.json");

        private static readonly object Trava = new object();

        private static string Hoje() => DateTime.Now.ToString("yyyy-MM-dd");

        // ----------------- persistência -----------------

        private RailDados CarregarDados()
        {
            try
            {
                if (!File.Exists(Caminho)) return new RailDados();
                var json = File.ReadAllText(Caminho);

                // v1 era uma lista de dias; v2 é um objeto.
                if (json.TrimStart().StartsWith("["))
                    return MigrarV1(json);

                return JsonConvert.DeserializeObject<RailDados>(json) ?? new RailDados();
            }
            catch
            {
                // arquivo corrompido: recomeça em vez de quebrar.
                return new RailDados();
            }
        }

        /// <summary>Formato antigo (List&lt;MissaoDia&gt;): achata sem perder tarefa.</summary>
        private static RailDados MigrarV1(string json)
        {
            var dados = new RailDados();
            var dias = JsonConvert.DeserializeObject<List<DiaLegado>>(json) ?? new List<DiaLegado>();

            foreach (var dia in dias.OrderBy(d => d.Data, StringComparer.Ordinal))
            {
                foreach (var item in dia.Itens ?? new List<ItemMissao>())
                {
                    item.Data = item.Data ?? dia.Data;
                    dados.Itens.Add(item);
                }
                if (dia.UltimoCheckIn > (dados.UltimoCheckIn ?? DateTime.MinValue))
                    dados.UltimoCheckIn = dia.UltimoCheckIn;
            }
            return dados;
        }

        private class DiaLegado
        {
            public string Data { get; set; }
            public List<ItemMissao> Itens { get; set; }
            public DateTime? UltimoCheckIn { get; set; }
        }

        private void Gravar(RailDados dados)
        {
            // Poda só concluídas antigas. Pendente NUNCA é podada (acumula como atrasada).
            var corte = DateTime.Now.AddDays(-DiasHistoricoConcluidas);
            dados.Itens.RemoveAll(i => i.Concluido && (i.ConcluidoEm ?? DateTime.Now) < corte);

            Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
            File.WriteAllText(Caminho, JsonConvert.SerializeObject(dados, Formatting.Indented));
        }

        // ----------------- consulta -----------------

        /// <summary>Missão visível (atrasadas + hoje + futuras), na ordem canônica.</summary>
        public MissaoVisivel MissaoAtual()
        {
            lock (Trava) return MontarVisivel(CarregarDados().Itens);
        }

        /// <summary>
        /// Missão + último check-in numa única leitura do arquivo — usado pelo
        /// coordenador, que roda em intervalo curto (evita ler o disco 3x por tick).
        /// </summary>
        public (MissaoVisivel Missao, DateTime? UltimoCheckIn) Estado()
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                return (MontarVisivel(dados.Itens), dados.UltimoCheckIn);
            }
        }

        private static MissaoVisivel MontarVisivel(List<ItemMissao> itens)
        {
            var hoje = DateTime.Now.ToString("yyyy-MM-dd");
            return new MissaoVisivel
            {
                Atrasadas = itens.Where(i => i.Atrasada(hoje))
                                 .OrderBy(i => i.Data, StringComparer.Ordinal).ToList(),
                // "Hoje" inclui atrasadas concluídas hoje (ficam visíveis,
                // tachadas, e um clique errado pode ser desfeito).
                DeHoje = itens.Where(i => i.Data == hoje ||
                             (i.Concluido && i.ConcluidoEm?.Date == DateTime.Today &&
                              string.Compare(i.Data, hoje, StringComparison.Ordinal) < 0)).ToList(),
                Futuras = itens.Where(i => string.Compare(i.Data, hoje, StringComparison.Ordinal) > 0)
                               .OrderBy(i => i.Data, StringComparer.Ordinal).ToList()
            };
        }

        /// <summary>Há missão definida para hoje (itens de hoje, mesmo concluídos, ou atrasadas)?</summary>
        public bool ExisteMissaoParaHoje()
        {
            var m = MissaoAtual();
            return m.DeHoje.Count > 0 || m.Atrasadas.Count > 0;
        }

        public DateTime? UltimoCheckIn()
        {
            lock (Trava) return CarregarDados().UltimoCheckIn;
        }

        // ----------------- mutações -----------------

        /// <summary>
        /// Adiciona uma tarefa (data null = hoje). Sem <paramref name="link"/>
        /// explícito, uma URL no texto vira o link da tarefa.
        /// </summary>
        public ItemMissao Adicionar(string texto, string link = null, DateTime? data = null)
        {
            var item = CriarItem(texto, link);
            if (item == null) return null;
            item.Data = (data ?? DateTime.Now).ToString("yyyy-MM-dd");

            lock (Trava)
            {
                var dados = CarregarDados();
                dados.Itens.Add(item);
                Gravar(dados);
                return item;
            }
        }

        /// <summary>Conclui pelo número exibido (1-based sobre a missão visível).</summary>
        public bool Concluir(int numero)
        {
            lock (Trava)
            {
                var lista = MissaoAtual().Lista;
                if (numero < 1 || numero > lista.Count) return false;
                return ConcluirPorId(lista[numero - 1].Id);
            }
        }

        /// <summary>Marca a tarefa pelo Id como concluída.</summary>
        public bool ConcluirPorId(string id)
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                var item = dados.Itens.FirstOrDefault(i => i.Id == id);
                if (item == null) return false;

                item.Concluido = true;
                item.ConcluidoEm = DateTime.Now;
                Gravar(dados);
                return true;
            }
        }

        /// <summary>Alterna concluída/pendente pelo Id. Retorna o novo estado (true = concluída).</summary>
        public bool AlternarConcluido(string id)
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                var item = dados.Itens.FirstOrDefault(i => i.Id == id);
                if (item == null) return false;

                item.Concluido = !item.Concluido;
                item.ConcluidoEm = item.Concluido ? DateTime.Now : (DateTime?)null;
                Gravar(dados);
                return item.Concluido;
            }
        }

        /// <summary>Regrava uma tarefa existente (edição de texto/link/data/estado).</summary>
        public bool AtualizarItem(ItemMissao item)
        {
            if (item == null) return false;

            lock (Trava)
            {
                var dados = CarregarDados();
                var indice = dados.Itens.FindIndex(i => i.Id == item.Id);
                if (indice < 0) return false;

                dados.Itens[indice] = item;
                Gravar(dados);
                return true;
            }
        }

        public void RemoverItem(string id)
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                dados.Itens.RemoveAll(i => i.Id == id);
                Gravar(dados);
            }
        }

        /// <summary>
        /// Reordena uma tarefa dentro do seu grupo (mesma <see cref="ItemMissao.Data"/>),
        /// trocando de lugar com a vizinha de mesma data na direção pedida. Muda a ordem
        /// exibida (a numeração e a "próxima pendente" seguem essa ordem). Retorna false
        /// se não há vizinha de mesma data naquela direção.
        /// </summary>
        public bool Mover(string id, bool subir)
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                var indice = dados.Itens.FindIndex(i => i.Id == id);
                if (indice < 0) return false;

                var data = dados.Itens[indice].Data;
                var vizinho = -1;
                if (subir)
                {
                    for (var j = indice - 1; j >= 0; j--)
                        if (dados.Itens[j].Data == data) { vizinho = j; break; }
                }
                else
                {
                    for (var j = indice + 1; j < dados.Itens.Count; j++)
                        if (dados.Itens[j].Data == data) { vizinho = j; break; }
                }
                if (vizinho < 0) return false;

                var tmp = dados.Itens[indice];
                dados.Itens[indice] = dados.Itens[vizinho];
                dados.Itens[vizinho] = tmp;
                Gravar(dados);
                return true;
            }
        }

        /// <summary>Reordena pelo número exibido (1-based sobre a missão visível).</summary>
        public bool MoverPorNumero(int numero, bool subir)
        {
            lock (Trava)
            {
                var lista = MissaoAtual().Lista;
                if (numero < 1 || numero > lista.Count) return false;
                return Mover(lista[numero - 1].Id, subir);
            }
        }

        /// <summary>Registra que um check-in aconteceu agora (controla o intervalo).</summary>
        public void RegistrarCheckIn()
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                dados.UltimoCheckIn = DateTime.Now;
                Gravar(dados);
            }
        }

        /// <summary>Apaga só os itens de hoje ("recomeçar o dia"); atrasadas ficam.</summary>
        public void LimparHoje()
        {
            lock (Trava)
            {
                var dados = CarregarDados();
                dados.Itens.RemoveAll(i => i.Data == Hoje());
                Gravar(dados);
            }
        }

        // ----------------- helpers -----------------

        /// <summary>
        /// Interpreta uma data digitada: <c>hoje</c>, <c>amanha/amanhã</c>,
        /// <c>dd/MM</c>, <c>dd/MM/yyyy</c> ou <c>yyyy-MM-dd</c>. Null se não entender.
        /// </summary>
        public static DateTime? ParseData(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            texto = texto.Trim().ToLowerInvariant();

            if (texto == "hoje") return DateTime.Today;
            if (texto == "amanha" || texto == "amanhã") return DateTime.Today.AddDays(1);

            var formatos = new[] { "dd/MM", "dd/MM/yyyy", "yyyy-MM-dd" };
            foreach (var f in formatos)
            {
                if (DateTime.TryParseExact(texto, f, CultureInfo.GetCultureInfo("pt-BR"),
                        DateTimeStyles.None, out var data))
                {
                    // "dd/MM" assume o ano corrente.
                    if (f == "dd/MM") data = new DateTime(DateTime.Today.Year, data.Month, data.Day);
                    return data.Date;
                }
            }
            return null;
        }

        private static readonly Regex RegexLink =
            new Regex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Monta um item a partir do texto: se ele contiver uma URL (e nenhum link
        /// for passado), a URL sai do texto e vira a ação da tarefa.
        /// </summary>
        public static ItemMissao CriarItem(string texto, string link = null)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            texto = texto.Trim();

            if (string.IsNullOrWhiteSpace(link))
            {
                var m = RegexLink.Match(texto);
                if (m.Success)
                {
                    link = m.Value;
                    texto = RegexLink.Replace(texto, "").Trim();
                    texto = Regex.Replace(texto, @"[ \t]{2,}", " ");
                    if (texto.Length == 0) texto = link; // tarefa que é só o link
                }
            }

            return new ItemMissao { Texto = texto, Link = link?.Trim() };
        }

        /// <summary>Abre a ação (link) da tarefa no app padrão. Nunca lança.</summary>
        public static void AbrirLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link)) return;
            try
            {
                Process.Start(new ProcessStartInfo(link.Trim()) { UseShellExecute = true });
            }
            catch
            {
                // Link inválido/sem app associado: ignora.
            }
        }

        // ----------------- heurística de distração -----------------

        /// <summary>
        /// True se o processo/título da janela ativa bate com algum termo da lista de
        /// distrações (comparação case-insensitive por substring).
        /// </summary>
        public static bool EhDistracao(string processo, string titulo, IEnumerable<string> termos)
        {
            if (termos == null) return false;

            foreach (var termo in termos)
            {
                if (string.IsNullOrWhiteSpace(termo)) continue;
                var t = termo.Trim();

                if (!string.IsNullOrEmpty(processo) &&
                    processo.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (!string.IsNullOrEmpty(titulo) &&
                    titulo.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True se **algum processo em execução** casa (substring, case-insensitive)
        /// com um dos termos — usado pelo "não perturbe" para pausar o Rail enquanto
        /// um app configurado (ex.: um jogo ou app de reunião) estiver aberto.
        /// </summary>
        public static bool AlgumAppAberto(IEnumerable<string> termos)
        {
            if (termos == null) return false;

            var lista = termos.Where(t => !string.IsNullOrWhiteSpace(t))
                              .Select(t => t.Trim()).ToList();
            if (lista.Count == 0) return false;

            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        string nome;
                        try { nome = p.ProcessName; } catch { continue; }
                        if (string.IsNullOrEmpty(nome)) continue;

                        if (lista.Any(t => nome.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                            return true;
                    }
                }
            }
            catch
            {
                // Enumerar processos nunca pode derrubar o Rail.
            }
            return false;
        }
    }
}
