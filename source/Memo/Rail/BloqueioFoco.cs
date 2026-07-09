using System;
using System.Windows;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Modo foco: por um tempo escolhido, toda vez que a janela ativa for uma
    /// distração, cobre a tela dela com um backdrop (o usuário não consegue ver).
    /// Avaliado a cada tick do Rail. Sair da distração (ir para o trabalho) esconde
    /// o backdrop — ele só aparece sobre a distração.
    /// </summary>
    internal sealed class BloqueioFoco
    {
        private JanelaBloqueio _overlay;
        private DateTime _ativoAte = DateTime.MinValue;
        private string _telaAtual;
        private string _tarefa;

        public bool Ativo => DateTime.Now < _ativoAte;

        /// <summary>Inicia o modo foco pela duração escolhida.</summary>
        public void Iniciar(TimeSpan duracao, string tarefa)
        {
            _ativoAte = DateTime.Now.Add(duracao);
            _tarefa = tarefa;
        }

        public void Encerrar()
        {
            _ativoAte = DateTime.MinValue;
            Esconder();
        }

        /// <summary>Chamado a cada tick: mostra/esconde o backdrop conforme a janela ativa.</summary>
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
                    MostrarSobre(janela.Hwnd);
                else
                    Esconder();
            }
            catch
            {
                // Bloqueio nunca pode derrubar o app.
            }
        }

        private void MostrarSobre(IntPtr hwnd)
        {
            if (_overlay == null)
            {
                _overlay = new JanelaBloqueio();
                _overlay.EncerrarSolicitado += Encerrar;
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
    }
}
