using System.Windows;

namespace Memo
{
    /// <summary>Diálogo temizado do Memo, substituindo o MessageBox do Windows.</summary>
    public partial class JanelaDialogo : Window
    {
        private JanelaDialogo(string titulo, string mensagem, bool confirmacao, bool perigo, string aviso)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            Title = "Memo";
            textoTitulo.Text = titulo;
            textoMensagem.Text = mensagem;
            marcador.SetResourceReference(BackgroundProperty, perigo ? "CorPerigo" : "CorDestaque");

            if (!string.IsNullOrWhiteSpace(aviso))
            {
                textoAviso.Text = aviso;
                caixaAviso.Visibility = Visibility.Visible;
            }

            if (confirmacao)
            {
                botaoOk.Content = "Sim";
                botaoCancelar.Content = "Não";
            }
            else
            {
                // Só "OK": some o cancelar e o ESC fecha pelo próprio OK.
                botaoOk.Content = "OK";
                botaoCancelar.Visibility = Visibility.Collapsed;
                botaoOk.IsCancel = true;
            }
        }

        /// <summary>
        /// Pergunta Sim/Não. Retorna true se confirmado. <paramref name="aviso"/>
        /// (opcional) mostra um rótulo de alerta destacado, ex.: ações irreversíveis.
        /// </summary>
        public static bool Confirmar(Window dono, string titulo, string mensagem, bool perigo = false, string aviso = null)
        {
            var janela = new JanelaDialogo(titulo, mensagem, confirmacao: true, perigo, aviso);
            Posicionar(janela, dono);
            return janela.ShowDialog() == true;
        }

        /// <summary>Mostra uma mensagem informativa com um botão OK.</summary>
        public static void Informar(Window dono, string titulo, string mensagem)
        {
            var janela = new JanelaDialogo(titulo, mensagem, confirmacao: false, perigo: false, aviso: null);
            Posicionar(janela, dono);
            janela.ShowDialog();
        }

        private static void Posicionar(Window janela, Window dono)
        {
            if (dono != null)
            {
                janela.Owner = dono;
                janela.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
