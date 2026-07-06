using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Memo.Rail
{
    /// <summary>
    /// O cerebrinho: uma bolha redonda 🧠 que surge **na posição do mouse**
    /// (difícil de ignorar), pulsando. Um clique expande para o cartão com a
    /// pergunta e os botões. Sem roubar o foco do teclado; se ignorado por ~30s,
    /// fecha sozinho (evento Ignorado) e só volta no próximo ciclo.
    /// </summary>
    public partial class JanelaCerebrinho : Window
    {
        public event Action Concluiu;        // check-in: terminou a tarefa atual
        public event Action Continuou;       // check-in: segue nela
        public event Action Adiou;           // check-in: +15 min
        public event Action VoltouTrilho;    // desvio: fechou a distração
        public event Action Trabalhando;     // desvio: falso positivo, silencia
        public event Action Ignorado;        // fechou sozinho sem interação

        private readonly DispatcherTimer _autoFechar;
        private readonly string _link;
        private bool _interagiu;

        private JanelaCerebrinho(string titulo, string mensagem, bool modoCheckIn, string link)
        {
            InitializeComponent();

            textoTitulo.Text = titulo;
            textoMensagem.Text = mensagem;
            (modoCheckIn ? painelCheckIn : painelDesvio).Visibility = Visibility.Visible;

            // Ação da tarefa: atalho para o link (WhatsApp, ticket…) no check-in.
            _link = link;
            if (modoCheckIn && !string.IsNullOrWhiteSpace(link))
            {
                botaoAcao.Visibility = Visibility.Visible;
                botaoAcao.ToolTip = link;
            }

            Loaded += (_, __) =>
            {
                PosicionarNoMouse();
                Pulsar();

                Opacity = 0;
                BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            };

            _autoFechar = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoFechar.Tick += (_, __) => { if (!_interagiu) { Ignorado?.Invoke(); Close(); } };
            _autoFechar.Start();

            Closed += (_, __) => _autoFechar.Stop();
        }

        /// <summary>Check-in periódico: "ainda na tarefa X?".</summary>
        public static JanelaCerebrinho CheckIn(string tarefa, string link = null) =>
            new JanelaCerebrinho("Memo Rail — check-in", $"Ainda na \"{tarefa}\"?",
                modoCheckIn: true, link);

        /// <summary>Aviso de desvio: detectou distração com missão pendente.</summary>
        public static JanelaCerebrinho Desvio(string distracao, string tarefa, string link = null) =>
            new JanelaCerebrinho("Memo Rail — voltar pro trilho?",
                $"{distracao} não parece a missão… A tarefa atual é \"{tarefa}\".",
                modoCheckIn: false, link);

        // ----------------- posição e animação -----------------

        /// <summary>Centraliza a bolha na posição atual do mouse (limitada à área útil).</summary>
        private void PosicionarNoMouse()
        {
            // Cursor em pixels de dispositivo → unidades WPF (DPI-aware).
            var cursor = System.Windows.Forms.Cursor.Position;
            var ponto = new Point(cursor.X, cursor.Y);

            var origem = PresentationSource.FromVisual(this);
            if (origem?.CompositionTarget != null)
                ponto = origem.CompositionTarget.TransformFromDevice.Transform(ponto);

            Left = ponto.X - ActualWidth / 2;
            Top = ponto.Y - ActualHeight / 2;
            GrudarNaTela();
        }

        /// <summary>Mantém a janela dentro da área útil (também após expandir).</summary>
        private void GrudarNaTela()
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - ActualWidth));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - ActualHeight));
        }

        /// <summary>Pulso contínuo da bolha, para chamar o olhar.</summary>
        private void Pulsar()
        {
            var pulso = new DoubleAnimation(1.0, 1.12, TimeSpan.FromMilliseconds(650))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            escalaBolha.BeginAnimation(ScaleTransform.ScaleXProperty, pulso);
            escalaBolha.BeginAnimation(ScaleTransform.ScaleYProperty, pulso);
        }

        /// <summary>Clique na bolha: expande para o cartão com os botões.</summary>
        private void Bolha_Click(object sender, MouseButtonEventArgs e)
        {
            bolha.Visibility = Visibility.Collapsed;
            cartao.Visibility = Visibility.Visible;

            // Renova o prazo de auto-fechar e re-ancora na tela após o layout crescer.
            _autoFechar.Stop();
            _autoFechar.Start();
            Dispatcher.BeginInvoke(new Action(GrudarNaTela), DispatcherPriority.Loaded);
        }

        // ----------------- respostas -----------------

        private void Responder(Action evento)
        {
            _interagiu = true;
            evento?.Invoke();
            Close();
        }

        private void Conclui_Click(object s, RoutedEventArgs e) => Responder(Concluiu);
        private void AindaNela_Click(object s, RoutedEventArgs e) => Responder(Continuou);
        private void Adiar_Click(object s, RoutedEventArgs e) => Responder(Adiou);
        private void Trabalhando_Click(object s, RoutedEventArgs e) => Responder(Trabalhando);

        private void Acao_Click(object s, RoutedEventArgs e)
        {
            // Abre a ação da tarefa e conta como "ainda nela".
            Memo.Service.Rail.RailService.AbrirLink(_link);
            Responder(Continuou);
        }

        private void VoltarTrilho_Click(object s, RoutedEventArgs e)
        {
            // Se a tarefa tem uma ação, "voltar pro trilho" já te leva direto pra ela.
            Memo.Service.Rail.RailService.AbrirLink(_link);
            Responder(VoltouTrilho);
        }
    }
}
