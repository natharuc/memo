using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Memo
{
    /// <summary>Pequenos utilitários de interop com o Windows.</summary>
    internal static class Nativo
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>
        /// Ajusta a barra de título da janela conforme o tema atual (escuro/claro),
        /// no Windows 10 1809+ / 11. Sem suporte em Windows mais antigo, é ignorado.
        /// </summary>
        public static void AplicarBarraTitulo(Window janela)
        {
            void Aplicar()
            {
                try
                {
                    var hwnd = new WindowInteropHelper(janela).Handle;
                    if (hwnd == IntPtr.Zero) return;
                    int usar = Tema.EhEscuro ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref usar, sizeof(int));
                }
                catch
                {
                    // Sem suporte em Windows mais antigo — ignora.
                }
            }

            if (janela.IsLoaded) Aplicar();
            else janela.SourceInitialized += (_, __) => Aplicar();
        }
    }
}
