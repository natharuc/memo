using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memo.Service;

namespace Memo
{
    public partial class JanelaGerarSenha : Window
    {
        private static readonly Brush Verde = new SolidColorBrush(Color.FromRgb(0x3B, 0xA5, 0x5D));
        private static readonly Brush Amarelo = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x30));

        private string _resultado;
        private bool _pronta;

        private JanelaGerarSenha()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            Verde.Freeze();
            Amarelo.Freeze();

            // Carrega as preferências salvas do usuário.
            var prefs = Configuracoes.Atual.Senha;
            controleComprimento.Value = prefs.Comprimento;
            optMaiusculas.IsChecked = prefs.Maiusculas;
            optMinusculas.IsChecked = prefs.Minusculas;
            optNumeros.IsChecked = prefs.Numeros;
            optSimbolos.IsChecked = prefs.Simbolos;

            _pronta = true;
            Gerar();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Persiste a configuração escolhida (usada também pela CLI `memo pass`).
            Configuracoes.Atual.Senha = Opcoes();
            Configuracoes.Atual.Salvar();
            base.OnClosed(e);
        }

        /// <summary>Abre o gerador. Retorna a senha escolhida ou null se cancelado.</summary>
        public static string Gerar(Window dono)
        {
            var janela = new JanelaGerarSenha { Owner = dono };
            return janela.ShowDialog() == true ? janela._resultado : null;
        }

        private OpcoesSenha Opcoes() => new OpcoesSenha
        {
            Comprimento = (int)controleComprimento.Value,
            Maiusculas = optMaiusculas.IsChecked == true,
            Minusculas = optMinusculas.IsChecked == true,
            Numeros = optNumeros.IsChecked == true,
            Simbolos = optSimbolos.IsChecked == true
        };

        private void Gerar()
        {
            if (!_pronta) return;

            var opcoes = Opcoes();
            textoComprimento.Text = opcoes.Comprimento.ToString();
            campoSenha.Text = GeradorSenha.Gerar(opcoes);

            var forca = GeradorSenha.Avaliar(opcoes);
            barraForca.Value = forca.Fracao;
            textoForca.Text = forca.Rotulo;

            var cor = forca.Nivel == NivelForca.Forte ? Verde
                : forca.Nivel == NivelForca.Media ? Amarelo
                : (Brush)FindResource("CorPerigo");
            barraForca.Foreground = cor;
            textoForca.Foreground = cor;
        }

        private void Regerar_Click(object sender, RoutedEventArgs e) => Gerar();

        private void Comprimento_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Gerar();

        private void Opcao_Click(object sender, RoutedEventArgs e)
        {
            // Impede desmarcar o último conjunto: sempre há ao menos um selecionado.
            if (optMaiusculas.IsChecked != true && optMinusculas.IsChecked != true &&
                optNumeros.IsChecked != true && optSimbolos.IsChecked != true)
            {
                ((CheckBox)sender).IsChecked = true;
                return;
            }

            Gerar();
        }

        private void Usar_Click(object sender, RoutedEventArgs e)
        {
            _resultado = campoSenha.Text;
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
