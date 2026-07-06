using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Memo.Service.Rail
{
    /// <summary>
    /// Persistência da missão do dia em <c>%LOCALAPPDATA%\Memo\rail.json</c>
    /// (mesmo padrão do LembreteService). Não é segredo: não passa pelo cofre.
    /// Guarda os últimos dias para permitir um resumo/histórico curto.
    /// </summary>
    public class RailService
    {
        private const int DiasHistorico = 14;

        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "rail.json");

        private static readonly object Trava = new object();

        private static string Hoje() => DateTime.Now.ToString("yyyy-MM-dd");

        // ----------------- persistência -----------------

        private List<MissaoDia> CarregarTodas()
        {
            try
            {
                if (File.Exists(Caminho))
                    return JsonConvert.DeserializeObject<List<MissaoDia>>(File.ReadAllText(Caminho))
                           ?? new List<MissaoDia>();
            }
            catch
            {
                // arquivo corrompido: recomeça em vez de quebrar.
            }
            return new List<MissaoDia>();
        }

        private void Gravar(List<MissaoDia> lista)
        {
            // Poda o histórico antigo.
            var corte = DateTime.Now.AddDays(-DiasHistorico).ToString("yyyy-MM-dd");
            lista.RemoveAll(m => string.Compare(m.Data, corte, StringComparison.Ordinal) < 0);

            Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
            File.WriteAllText(Caminho, JsonConvert.SerializeObject(lista, Formatting.Indented));
        }

        // ----------------- missão do dia -----------------

        /// <summary>Missão de hoje, ou null se ainda não foi definida.</summary>
        public MissaoDia MissaoDeHoje()
        {
            lock (Trava)
                return CarregarTodas().FirstOrDefault(m => m.Data == Hoje());
        }

        /// <summary>
        /// Adiciona uma tarefa à missão de hoje (cria a missão se não existir).
        /// Sem <paramref name="link"/> explícito, uma URL no texto vira o link da tarefa.
        /// </summary>
        public ItemMissao Adicionar(string texto, string link = null)
        {
            var item = CriarItem(texto, link);
            if (item == null) return null;

            lock (Trava)
            {
                var todas = CarregarTodas();
                var hoje = todas.FirstOrDefault(m => m.Data == Hoje());
                if (hoje == null)
                {
                    hoje = new MissaoDia { Data = Hoje() };
                    todas.Add(hoje);
                }

                hoje.Itens.Add(item);
                Gravar(todas);
                return item;
            }
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
                    texto = Regex.Replace(texto, @"\s{2,}", " ");
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

        /// <summary>Marca a tarefa pelo número (1-based) como concluída. False se não achou.</summary>
        public bool Concluir(int numero)
        {
            lock (Trava)
            {
                var todas = CarregarTodas();
                var hoje = todas.FirstOrDefault(m => m.Data == Hoje());
                if (hoje == null || numero < 1 || numero > hoje.Itens.Count) return false;

                var item = hoje.Itens[numero - 1];
                if (!item.Concluido)
                {
                    item.Concluido = true;
                    item.ConcluidoEm = DateTime.Now;
                    Gravar(todas);
                }
                return true;
            }
        }

        /// <summary>Marca a tarefa pelo Id como concluída (usado pela UI).</summary>
        public bool ConcluirPorId(string id)
        {
            lock (Trava)
            {
                var todas = CarregarTodas();
                var hoje = todas.FirstOrDefault(m => m.Data == Hoje());
                var item = hoje?.Itens.FirstOrDefault(i => i.Id == id);
                if (item == null) return false;

                item.Concluido = true;
                item.ConcluidoEm = DateTime.Now;
                Gravar(todas);
                return true;
            }
        }

        /// <summary>Desmarca/remarca ou remove itens: regrava a missão de hoje inteira.</summary>
        public void SalvarHoje(MissaoDia missao)
        {
            if (missao == null) return;
            missao.Data = Hoje();

            lock (Trava)
            {
                var todas = CarregarTodas();
                todas.RemoveAll(m => m.Data == missao.Data);
                todas.Add(missao);
                Gravar(todas);
            }
        }

        /// <summary>Registra que um check-in aconteceu agora (controla o intervalo).</summary>
        public void RegistrarCheckIn()
        {
            lock (Trava)
            {
                var todas = CarregarTodas();
                var hoje = todas.FirstOrDefault(m => m.Data == Hoje());
                if (hoje == null) return;
                hoje.UltimoCheckIn = DateTime.Now;
                Gravar(todas);
            }
        }

        /// <summary>Apaga a missão de hoje (recomeçar o dia).</summary>
        public void LimparHoje()
        {
            lock (Trava)
            {
                var todas = CarregarTodas();
                todas.RemoveAll(m => m.Data == Hoje());
                Gravar(todas);
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
    }
}
