using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Modo foco: por um tempo escolhido, toda vez que a janela ativa for uma
    /// distração, cobre a tela dela com um backdrop (o usuário não consegue ver).
    /// Reage **na hora** à troca de janela (hook de foreground do Windows) além do
    /// tick periódico. Sair da distração esconde o backdrop imediatamente; o botão
    /// "Fechar distração" fecha a janela/aba e encerra o modo foco.
    /// </summary>
    internal sealed class BloqueioFoco
    {
        private JanelaBloqueio _overlay;
        private DateTime _ativoAte = DateTime.MinValue;
        private string _telaAtual;
        private string _tarefa;
        private IntPtr _alvoHwnd;
        private string _alvoProcesso;

        private readonly WinEventDelegate _cbForeground;
        private IntPtr _hook;

        public BloqueioFoco()
        {
            _cbForeground = (_, __, ___, ____, _____, ______, _______) => Avaliar();
        }

        public bool Ativo => DateTime.Now < _ativoAte;

        /// <summary>Inicia o modo foco pela duração escolhida.</summary>
        public void Iniciar(TimeSpan duracao, string tarefa)
        {
            _ativoAte = DateTime.Now.Add(duracao);
            _tarefa = tarefa;

            // Reage instantaneamente à troca de janela em primeiro plano.
            if (_hook == IntPtr.Zero)
                _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _cbForeground, 0, 0, WINEVENT_OUTOFCONTEXT);

            Avaliar();
        }

        public void Encerrar()
        {
            _ativoAte = DateTime.MinValue;
            if (_hook != IntPtr.Zero) { UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
            Esconder();
        }

        /// <summary>Mostra/esconde o backdrop conforme a janela ativa. Nunca lança.</summary>
        public void Avaliar()
        {
            if (!Ativo)
            {
                Esconder();
                return;
            }

            try
            {
                var janela = MonitorFoco.ObterJanelaAtiva();
                if (janela == null) return;

                // A própria overlay em primeiro plano não conta como "saiu da distração".
                if (_overlay != null && _overlay.IsVisible && janela.Hwnd == _overlay.Handle)
                {
                    _overlay.AtualizarContagem(_ativoAte - DateTime.Now);
                    return;
                }

                var distracoes = Configuracoes.Atual.Rail?.Distracoes;
                if (RailService.EhDistracao(janela.Processo, janela.Titulo, distracoes))
                {
                    _alvoProcesso = janela.Processo;
                    MostrarSobre(janela.Hwnd);
                }
                else
                {
                    Esconder(); // saiu da distração → some na hora
                }
            }
            catch
            {
                // Bloqueio nunca pode derrubar o app.
            }
        }

        private void MostrarSobre(IntPtr hwnd)
        {
            _alvoHwnd = hwnd;

            if (_overlay == null)
            {
                _overlay = new JanelaBloqueio();
                _overlay.EncerrarSolicitado += Encerrar;
                _overlay.FecharDistracaoSolicitado += FecharDistracao;
                _overlay.Closed += (_, __) => { _overlay = null; _telaAtual = null; };
            }

            var tela = System.Windows.Forms.Screen.FromHandle(hwnd);

            _overlay.DefinirTarefa(_tarefa);
            if (!_overlay.IsVisible) _overlay.Show();

            // Reposiciona só quando muda de monitor (evita flicker).
            if (_telaAtual != tela.DeviceName)
            {
                _telaAtual = tela.DeviceName;
                PosicionarNaTela(tela);
            }

            _overlay.Topmost = true;
            _overlay.AtualizarContagem(_ativoAte - DateTime.Now);
        }

        private static readonly string[] Navegadores =
        {
            "chrome", "msedge", "edge", "firefox", "brave", "opera", "vivaldi",
            "iexplore", "arc", "chromium", "librewolf", "waterfox"
        };

        private static bool EhNavegador(string processo) =>
            !string.IsNullOrEmpty(processo) &&
            Navegadores.Any(n => processo.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Fecha a distração **mas mantém o modo foco ativo** (pelo tempo escolhido,
        /// para reincidências voltarem a ser bloqueadas). Em navegador, fecha só a
        /// **aba ativa** (Ctrl+W); em outros apps, fecha a janela (WM_CLOSE). O
        /// backdrop some sozinho quando a distração deixa de estar em primeiro plano.
        /// </summary>
        private void FecharDistracao()
        {
            try
            {
                if (_alvoHwnd == IntPtr.Zero) return;

                if (EhNavegador(_alvoProcesso))
                {
                    // Foca a janela do navegador e manda Ctrl+W (fecha a aba atual).
                    SetForegroundWindow(_alvoHwnd);
                    AposDelay(90, () => { EnviarCtrlW(); AposDelay(350, Avaliar); });
                }
                else
                {
                    SendMessage(_alvoHwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    AposDelay(300, Avaliar);
                }
            }
            catch
            {
                // Fechar nunca pode derrubar o app; o modo foco segue ativo.
            }
        }

        private static void AposDelay(int ms, Action acao)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (_, __) => { t.Stop(); acao(); };
            t.Start();
        }

        private static void EnviarCtrlW()
        {
            const byte VK_CONTROL = 0x11, VK_W = 0x57, KEYUP = 0x02;
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_W, 0, 0, UIntPtr.Zero);
            keybd_event(VK_W, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
        }

        private void PosicionarNaTela(System.Windows.Forms.Screen tela)
        {
            var b = tela.Bounds; // pixels de dispositivo
            var origem = PresentationSource.FromVisual(_overlay);
            if (origem?.CompositionTarget != null)
            {
                var m = origem.CompositionTarget.TransformFromDevice;
                var canto = m.Transform(new Point(b.Left, b.Top));
                var tam = m.Transform(new Vector(b.Width, b.Height));
                _overlay.Left = canto.X;
                _overlay.Top = canto.Y;
                _overlay.Width = tam.X;
                _overlay.Height = tam.Y;
            }
            else
            {
                _overlay.Left = b.Left; _overlay.Top = b.Top;
                _overlay.Width = b.Width; _overlay.Height = b.Height;
            }
        }

        private void Esconder()
        {
            if (_overlay != null && _overlay.IsVisible) _overlay.Hide();
            _telaAtual = null;
        }

        // ----------------- Win32 -----------------

        private delegate void WinEventDelegate(IntPtr hook, uint evt, IntPtr hwnd,
            int idObject, int idChild, uint thread, uint time);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WM_CLOSE = 0x0010;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
