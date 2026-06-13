using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Memo
{
    public partial class Toast : Window
    {
        private Toast(string mensagem, bool sucesso)
        {
            InitializeComponent();

            textoMensagem.Text = mensagem;
            marcador.Background = sucesso
                ? (Brush)FindResource("CorDestaque")
                : (Brush)FindResource("CorPerigo");

            Loaded += AoCarregar;
        }

        /// <summary>Exibe o aviso no canto inferior direito e encerra ao desaparecer.</summary>
        public static void Mostrar(string mensagem, bool sucesso)
        {
            new Toast(mensagem, sucesso).Show();
        }

        private void AoCarregar(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top = area.Bottom - Height - 16;

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
                fade.Completed += (___, ____) => Close();
                BeginAnimation(OpacityProperty, fade);
            };
            timer.Start();
        }
    }
}
