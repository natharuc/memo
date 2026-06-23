using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Memo.Service.Seguranca
{
    /// <summary>
    /// Limpa do histórico do "Executar" do Windows (Win+R) os comandos
    /// "memo set ..." — eles ficam gravados no registro (RunMRU) com o segredo
    /// em texto puro e apareceriam no autocomplete. Best-effort.
    /// </summary>
    public static class HistoricoExecutar
    {
        // O Explorer anexa o caractere SOH (U+0001) ao fim do comando no RunMRU.
        private static readonly char SufixoRunMru = (char)1;

        private const string CaminhoRunMru =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";

        public static void LimparComandosSet()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(CaminhoRunMru, writable: true))
                {
                    if (key == null) return;

                    var mruList = key.GetValue("MRUList") as string ?? string.Empty;
                    var removidos = string.Empty;

                    foreach (var nome in key.GetValueNames())
                    {
                        if (nome.Length != 1) continue; // entradas são "a", "b", "c"...

                        if (EhMemoSet(key.GetValue(nome) as string))
                        {
                            key.DeleteValue(nome, throwOnMissingValue: false);
                            removidos += nome;
                        }
                    }

                    if (removidos.Length > 0)
                    {
                        var nova = new string(mruList.Where(c => removidos.IndexOf(c) < 0).ToArray());
                        key.SetValue("MRUList", nova, RegistryValueKind.String);
                    }
                }
            }
            catch
            {
                // Histórico é higiene; nunca pode quebrar o app.
            }
        }

        /// <summary>True se o comando do RunMRU for um "memo set ..." (carrega segredo).</summary>
        private static bool EhMemoSet(string dado)
        {
            if (string.IsNullOrWhiteSpace(dado)) return false;

            var texto = dado.TrimEnd(SufixoRunMru).Trim();
            var tokens = texto.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;

            string prog;
            try { prog = Path.GetFileNameWithoutExtension(tokens[0]); }
            catch { prog = tokens[0]; }

            return (string.Equals(prog, "memo", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prog, "memo-cli", StringComparison.OrdinalIgnoreCase))
                   && string.Equals(tokens[1], "set", StringComparison.OrdinalIgnoreCase);
        }
    }
}
