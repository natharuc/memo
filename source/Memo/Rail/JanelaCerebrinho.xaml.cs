using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Memo.Rail
{
    /// <summary>
    /// O cerebrinho: uma bolha redonda 🧠 que surge **na posição do mouse**
    /// (difícil de ignorar), pulsando. Um clique expande para o cartão com a
    /// pergunta e os botões. Sem roubar o foco do teclado. **Nunca some sozinho**:
    /// se for ignorado por N minutos, ele se reposiciona na posição atual do mouse
    /// (some de onde estava e reaparece onde você está), até você clicar.
    /// </summary>
    public partial class JanelaCerebrinho : Window
    {
        public event Action Concluiu;        // check-in: terminou a tarefa atual
        public event Action Continuou;       // check-in: segue nela
        public event Action Adiou;           // check-in: +15 min
        public event Action VoltouTrilho;    // desvio: fechou a distração
        public event Action Trabalhando;     // desvio: falso positivo, silencia
        public event Action<TimeSpan> PrecisoFocar; // desvio: ativa o modo foco por N min

        private readonly DispatcherTimer _realocar;
        private readonly string _link;
        private bool _expandido;

        private JanelaCerebrinho(string titulo, string mensagem, bool modoCheckIn, string link,
            TimeSpan realocarApos)
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

            Loaded += (_, __) => Aparecer();

            // Enquanto continuar como bolha (não interagido), reaparece na posição
            // atual do mouse a cada N minutos — nunca fecha sozinho.
            _realocar = new DispatcherTimer { Interval = realocarApos };
            _realocar.Tick += (_, __) => { if (!_expandido) Realocar(); };
            _realocar.Start();

            Closed += (_, __) => _realocar.Stop();
        }

        /// <summary>Check-in periódico: "ainda na tarefa X?".</summary>
        public static JanelaCerebrinho CheckIn(string tarefa, string link = null, TimeSpan? realocarApos = null) =>
            new JanelaCerebrinho("Memo Rail — check-in", $"Ainda na \"{tarefa}\"?",
                modoCheckIn: true, link, realocarApos ?? TimeSpan.FromMinutes(2));

        /// <summary>Aviso de desvio: detectou distração com missão pendente.</summary>
        public static JanelaCerebrinho Desvio(string distracao, string tarefa, string link = null, TimeSpan? realocarApos = null) =>
            new JanelaCerebrinho("Memo Rail — voltar pro trilho?",
                $"{distracao} não parece a missão… A tarefa atual é \"{tarefa}\".",
                modoCheckIn: false, link, realocarApos ?? TimeSpan.FromMinutes(2));

        // ----------------- posição e animação -----------------

        private void Aparecer()
        {
            PosicionarNoMouse();
            Pulsar();
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }

        /// <summary>Some de onde está e reaparece na posição atual do mouse.</summary>
        private void Realocar()
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, __) =>
            {
                PosicionarNoMouse();
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            };
            BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>Centraliza a bolha na posição atual do mouse (no monitor do mouse).</summary>
        private void PosicionarNoMouse()
        {
            // Cursor em pixels de dispositivo (coords do desktop virtual) → unidades WPF.
            var cursor = System.Windows.Forms.Cursor.Position;
            var ponto = DispositivoParaWpf().Transform(new Point(cursor.X, cursor.Y));

            Left = ponto.X - ActualWidth / 2;
            Top = ponto.Y - ActualHeight / 2;
            GrudarNaTela();
        }

        /// <summary>
        /// Mantém a janela dentro da área útil do monitor **onde ela está** (não só o
        /// primário) — assim a bolha aparece no mouse mesmo em telas secundárias.
        /// </summary>
        private void GrudarNaTela()
        {
            // Monitor sob o centro atual da janela.
            var centro = WpfParaDispositivo().Transform(new Point(Left + ActualWidth / 2, Top + ActualHeight / 2));
            var tela = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)centro.X, (int)centro.Y));

            var area = AreaEmWpf(tela.WorkingArea);
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - ActualWidth));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - ActualHeight));
        }

        private Matrix DispositivoParaWpf() =>
            PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        private Matrix WpfParaDispositivo() =>
            PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

        private Rect AreaEmWpf(System.Drawing.Rectangle px)
        {
            var m = DispositivoParaWpf();
            var canto = m.Transform(new Point(px.Left, px.Top));
            var tam = m.Transform(new Vector(px.Width, px.Height));
            return new Rect(canto, new Size(tam.X, tam.Y));
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
            _expandido = true;
            _realocar.Stop(); // já está interagindo: para de se mover
            bolha.Visibility = Visibility.Collapsed;
            cartao.Visibility = Visibility.Visible;

            // Re-ancora na tela após o layout crescer.
            Dispatcher.BeginInvoke(new Action(GrudarNaTela), DispatcherPriority.Loaded);
        }

        // ----------------- respostas -----------------

        private void Responder(Action evento)
        {
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

        private void PrecisoFocar_Click(object s, RoutedEventArgs e)
        {
            // Mostra as opções de tempo; expande e re-ancora na tela.
            painelDesvio.Visibility = Visibility.Collapsed;
            painelFoco.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(GrudarNaTela), DispatcherPriority.Loaded);
        }

        private void Foco_Click(object s, RoutedEventArgs e)
        {
            var min = int.Parse((string)((Button)s).Tag);
            Responder(() => PrecisoFocar?.Invoke(TimeSpan.FromMinutes(min)));
        }
    }
}
