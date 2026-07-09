using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Memo.Rail
{
    /// <summary>Snapshot da janela em primeiro plano.</summary>
    internal sealed class JanelaAtiva
    {
        public IntPtr Hwnd { get; set; }       // handle da janela (monitor / comparação)
        public string Processo { get; set; }   // ex.: "chrome"
        public string Titulo { get; set; }     // ex.: "YouTube - Google Chrome"
    }

    /// <summary>
    /// Lê o estado do usuário via Win32: janela ativa (processo + título) e tempo
    /// ocioso. Privacidade: nada disso é gravado em disco nem sai da máquina —
    /// é lido, comparado em memória e descartado.
    /// </summary>
    internal static class MonitorFoco
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder texto, int max);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        /// <summary>Janela em primeiro plano, ou null se não der para ler.</summary>
        public static JanelaAtiva ObterJanelaAtiva()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                var sb = new StringBuilder(512);
                GetWindowText(hwnd, sb, sb.Capacity);

                GetWindowThreadProcessId(hwnd, out var pid);
                string processo = null;
                if (pid != 0)
                {
                    try { using (var p = Process.GetProcessById((int)pid)) processo = p.ProcessName; }
                    catch { /* processo já saiu */ }
                }

                return new JanelaAtiva { Hwnd = hwnd, Processo = processo, Titulo = sb.ToString() };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Há quanto tempo não há input (teclado/mouse) do usuário.</summary>
        public static TimeSpan TempoOcioso()
        {
            try
            {
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
                if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;

                var ms = unchecked(Environment.TickCount - (int)info.dwTime);
                return ms > 0 ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }
}
