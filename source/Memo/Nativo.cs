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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        /// <summary>
        /// Traz a janela para o primeiro plano de forma confiável, contornando a
        /// proteção do Windows contra "roubo de foco" (AttachThreadInput). Útil ao
        /// abrir a partir da bandeja ou de uma 2ª instância, quando só
        /// <c>Activate()</c> não vence o bloqueio. NÃO chama <c>Show()</c> — a janela
        /// já deve estar visível (serve para janelas normais e modais).
        /// </summary>
        public static void TrazerParaFrente(Window janela)
        {
            if (janela == null) return;

            janela.Activate();

            try
            {
                var hwnd = new WindowInteropHelper(janela).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    var primeiroPlano = GetForegroundWindow();
                    var threadAtual = GetCurrentThreadId();
                    var threadAlvo = primeiroPlano == IntPtr.Zero
                        ? threadAtual
                        : GetWindowThreadProcessId(primeiroPlano, out _);

                    var anexar = threadAlvo != threadAtual;
                    if (anexar) AttachThreadInput(threadAlvo, threadAtual, true);

                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);

                    if (anexar) AttachThreadInput(threadAlvo, threadAtual, false);
                }
            }
            catch
            {
                // best-effort: o fallback abaixo ainda tenta trazer à frente.
            }

            // Fallback gerenciado: piscar Topmost garante a vinda à frente nos casos
            // em que o SetForegroundWindow nativo não foi suficiente.
            if (!janela.Topmost)
            {
                janela.Topmost = true;
                janela.Topmost = false;
            }
        }

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
