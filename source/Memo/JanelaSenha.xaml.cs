using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Memo.Service.Seguranca;

namespace Memo
{
    public partial class JanelaSenha : Window
    {
        private readonly Cofre _cofre;
        private readonly bool _modoCriacao;

        private JanelaSenha(Cofre cofre)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _cofre = cofre;
            _modoCriacao = !cofre.Inicializado;

            if (_modoCriacao)
            {
                textoTitulo.Text = "Criar cofre";
                textoSubtitulo.Text = "Defina uma senha-mestra. Ela protege todos os seus documentos " +
                                      "e não pode ser recuperada se for esquecida.";
                campoConfirmar.Visibility = Visibility.Visible;
                botaoConfirmar.Content = "Criar";
            }

            // Ao abrir (inclusive a partir da bandeja), garante prioridade do Windows
            // e foco no campo de senha — senão o que o usuário digita iria pra outra
            // janela.
            Loaded += (_, __) =>
            {
                Nativo.TrazerParaFrente(this);
                FocarSenha();
            };
        }

        /// <summary>Foca o campo de senha de forma confiável (após a janela renderizar).</summary>
        private void FocarSenha()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                campoSenha.Focus();
                Keyboard.Focus(campoSenha);
            }), DispatcherPriority.Input);
        }

        /// <summary>Mostra a janela e devolve true se o cofre foi destrancado/criado.</summary>
        public static bool Solicitar(Cofre cofre)
        {
            var janela = new JanelaSenha(cofre);
            return janela.ShowDialog() == true;
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            var senha = campoSenha.Password;

            if (string.IsNullOrEmpty(senha))
            {
                MostrarErro("Digite a senha.");
                return;
            }

            if (_modoCriacao)
            {
                if (senha.Length < 4)
                {
                    MostrarErro("Use ao menos 4 caracteres.");
                    return;
                }
                if (senha != campoConfirmar.Password)
                {
                    MostrarErro("As senhas não conferem.");
                    return;
                }

                _cofre.Inicializar(senha);
                DialogResult = true;
                return;
            }

            if (!_cofre.Destrancar(senha))
            {
                MostrarErro("Senha incorreta.");
                campoSenha.SelectAll();
                campoSenha.Focus();
                return;
            }

            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void MostrarErro(string mensagem)
        {
            textoErro.Text = mensagem;
            textoErro.Visibility = Visibility.Visible;
        }
    }
}
