using System;
using System.Windows;
using Memo.Service.Atualizacao;

namespace Memo
{
    public partial class JanelaAtualizacao : Window
    {
        private readonly AtualizadorService _atualizador;
        private readonly InfoAtualizacao _info;

        private JanelaAtualizacao(AtualizadorService atualizador, InfoAtualizacao info)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _atualizador = atualizador;
            _info = info;

            textoVersao.Text = $"A versão {info.Versao} está disponível " +
                               $"(você tem a {atualizador.VersaoAtual}).";
            textoNotas.Text = string.IsNullOrWhiteSpace(info.Notas)
                ? "Sem notas de versão."
                : info.Notas.Trim();
        }

        /// <summary>Mostra o diálogo de atualização sobre a janela dona.</summary>
        public static void Mostrar(Window dono, AtualizadorService atualizador, InfoAtualizacao info)
        {
            var janela = new JanelaAtualizacao(atualizador, info) { Owner = dono };
            janela.ShowDialog();
        }

        private async void Atualizar_Click(object sender, RoutedEventArgs e)
        {
            botaoAtualizar.IsEnabled = false;
            botaoDepois.IsEnabled = false;
            textoErro.Visibility = Visibility.Collapsed;
            barraProgresso.Visibility = Visibility.Visible;

            try
            {
                var progresso = new Progress<double>(v => barraProgresso.Value = v);
                var novoExe = await _atualizador.BaixarAsync(_info, progresso);

                _atualizador.AplicarEReiniciar(novoExe);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MostrarErro($"Não foi possível atualizar: {ex.Message}");
                barraProgresso.Visibility = Visibility.Collapsed;
                botaoAtualizar.IsEnabled = true;
                botaoDepois.IsEnabled = true;
            }
        }

        private void Depois_Click(object sender, RoutedEventArgs e) => Close();

        private void MostrarErro(string mensagem)
        {
            textoErro.Text = mensagem;
            textoErro.Visibility = Visibility.Visible;
        }
    }
}
