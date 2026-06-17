using System.Diagnostics;
using Microsoft.Win32;

namespace Memo
{
    /// <summary>Liga/desliga o início do Memo com o Windows (modo bandeja).</summary>
    public static class Inicializacao
    {
        private const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Nome = "Memo";

        public static bool Habilitado
        {
            get
            {
                using (var chave = Registry.CurrentUser.OpenSubKey(ChaveRun))
                    return chave?.GetValue(Nome) != null;
            }
        }

        public static void Definir(bool habilitar)
        {
            using (var chave = Registry.CurrentUser.OpenSubKey(ChaveRun, writable: true))
            {
                if (chave == null) return;

                if (habilitar)
                {
                    var exe = Process.GetCurrentProcess().MainModule.FileName;
                    chave.SetValue(Nome, $"\"{exe}\" --tray");
                }
                else
                {
                    chave.DeleteValue(Nome, throwOnMissingValue: false);
                }
            }
        }
    }
}
