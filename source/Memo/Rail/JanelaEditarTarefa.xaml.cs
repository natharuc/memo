using System;
using System.Windows;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Edição de uma tarefa da missão: texto (com formatação leve e quebras de
    /// linha), link (ação) e data. Altera o objeto recebido só ao salvar.
    /// </summary>
    public partial class JanelaEditarTarefa : Window
    {
        private readonly ItemMissao _item;
        private bool _salvou;

        private JanelaEditarTarefa(ItemMissao item)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _item = item;
            campoTexto.Text = item.Texto ?? string.Empty;
            campoLink.Text = item.Link ?? string.Empty;
            campoData.SelectedDate = RailService.ParseData(item.Data) ?? DateTime.Today;

            Loaded += (_, __) => { campoTexto.Focus(); campoTexto.CaretIndex = campoTexto.Text.Length; };
        }

        /// <summary>Abre a edição; true se o usuário salvou (o item já sai atualizado).</summary>
        public static bool Editar(Window dono, ItemMissao item)
        {
            var janela = new JanelaEditarTarefa(item) { Owner = dono };
            janela.ShowDialog();
            return janela._salvou;
        }

        private void Hoje_Click(object sender, RoutedEventArgs e) =>
            campoData.SelectedDate = DateTime.Today;

        private void Amanha_Click(object sender, RoutedEventArgs e) =>
            campoData.SelectedDate = DateTime.Today.AddDays(1);

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var texto = campoTexto.Text?.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                MostrarErro("O texto da tarefa não pode ficar vazio.");
                return;
            }

            _item.Texto = texto;
            _item.Link = string.IsNullOrWhiteSpace(campoLink.Text) ? null : campoLink.Text.Trim();
            if (campoData.SelectedDate != null)
                _item.Data = campoData.SelectedDate.Value.ToString("yyyy-MM-dd");

            _salvou = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void MostrarErro(string mensagem)
        {
            textoErro.Text = mensagem;
            textoErro.Visibility = Visibility.Visible;
        }
    }
}
