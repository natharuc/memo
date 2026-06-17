using System;
using System.Windows;
using Memo.Service.Classes;
using Memo.Service.Repositorio;

namespace Memo
{
    public partial class JanelaEditar : Window
    {
        private Documento _resultado;

        private JanelaEditar(Documento documento, bool novo)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            Title = novo ? "Novo documento" : "Editar documento";
            campoChave.Text = documento?.Key ?? string.Empty;
            campoValor.Text = documento?.Value ?? string.Empty;

            // Renomear trocaria o arquivo; ao editar, a chave fica travada.
            campoChave.IsReadOnly = !novo;

            Loaded += (_, __) => (novo ? campoChave : (System.Windows.Controls.Control)campoValor).Focus();
        }

        public static Documento Criar(Window dono) => Mostrar(dono, null, novo: true);

        public static Documento Editar(Window dono, Documento documento) =>
            Mostrar(dono, documento, novo: false);

        private static Documento Mostrar(Window dono, Documento documento, bool novo)
        {
            var janela = new JanelaEditar(documento, novo) { Owner = dono };
            if (dono == null) janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return janela.ShowDialog() == true ? janela._resultado : null;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var chave = campoChave.Text?.Trim();

            try
            {
                DocumentoRepository.SanitizarChave(chave);
            }
            catch (ArgumentException ex)
            {
                MostrarErro(ex.Message);
                return;
            }

            _resultado = new Documento(chave, campoValor.Text);
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void GerarSenha_Click(object sender, RoutedEventArgs e)
        {
            var senha = JanelaGerarSenha.Gerar(this);
            if (senha != null) campoValor.Text = senha;
        }

        private void MostrarErro(string mensagem)
        {
            textoErro.Text = mensagem;
            textoErro.Visibility = Visibility.Visible;
        }
    }
}
