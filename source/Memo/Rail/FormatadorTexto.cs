using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Memo.Rail
{
    /// <summary>
    /// Formatação leve das tarefas: <c>**negrito**</c>, <c>*itálico*</c> e quebras
    /// de linha. Sem dependências — parser por regex, tolerante a marcadores soltos.
    /// </summary>
    internal static class FormatadorTexto
    {
        // **negrito** primeiro (senão o * simples engoliria o duplo).
        private static readonly Regex Marcadores =
            new Regex(@"\*\*(.+?)\*\*|\*(.+?)\*", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>Preenche os Inlines do TextBlock a partir do texto com marcadores.</summary>
        public static void AplicarInlines(TextBlock bloco, string texto)
        {
            bloco.Inlines.Clear();
            if (string.IsNullOrEmpty(texto)) return;

            var linhas = texto.Replace("\r\n", "\n").Split('\n');
            for (var l = 0; l < linhas.Length; l++)
            {
                if (l > 0) bloco.Inlines.Add(new LineBreak());
                AplicarLinha(bloco, linhas[l]);
            }
        }

        private static void AplicarLinha(TextBlock bloco, string linha)
        {
            var posicao = 0;
            foreach (Match m in Marcadores.Matches(linha))
            {
                if (m.Index > posicao)
                    bloco.Inlines.Add(new Run(linha.Substring(posicao, m.Index - posicao)));

                if (m.Groups[1].Success)
                    bloco.Inlines.Add(new Run(m.Groups[1].Value) { FontWeight = FontWeights.Bold });
                else
                    bloco.Inlines.Add(new Run(m.Groups[2].Value) { FontStyle = FontStyles.Italic });

                posicao = m.Index + m.Length;
            }

            if (posicao < linha.Length)
                bloco.Inlines.Add(new Run(linha.Substring(posicao)));
        }

        /// <summary>Texto "cru" (sem marcadores, quebras viram espaço) — para cerebrinho/toast/CLI.</summary>
        public static string SemFormatacao(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;

            var plano = Marcadores.Replace(texto.Replace("\r\n", "\n"),
                m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            return plano.Replace('\n', ' ').Trim();
        }
    }
}
