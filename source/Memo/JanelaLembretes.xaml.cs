using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Memo.Service.Lembretes;

namespace Memo
{
    public partial class JanelaLembretes : Window
    {
        private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

        private readonly LembreteService _service;

        public JanelaLembretes(LembreteService service)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _service = service;
            campoQuando.Text = DateTime.Now.AddHours(1).ToString("dd/MM/yyyy HH:mm", Cultura);
            chkInicio.IsChecked = Inicializacao.Habilitado;

            Recarregar();
            Loaded += (_, __) => campoTexto.Focus();
        }

        private void Recarregar() => listaLembretes.ItemsSource = _service.Listar();

        private void Adicionar_Click(object sender, RoutedEventArgs e)
        {
            var texto = campoTexto.Text?.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                Erro("Escreva o que você quer lembrar.");
                return;
            }

            if (!DateTime.TryParse(campoQuando.Text, Cultura, DateTimeStyles.None, out var quando))
            {
                Erro("Data/hora inválida. Use o formato dd/mm/aaaa hh:mm.");
                return;
            }

            int.TryParse(campoRepetir.Text, out var repetir);
            if (repetir < 0) repetir = 0;

            // Sem repetição e no passado: provavelmente engano.
            if (repetir == 0 && quando <= DateTime.Now)
            {
                Erro("Esse horário já passou. Escolha um horário futuro (ou use repetição).");
                return;
            }

            _service.Adicionar(texto, quando, repetir);

            campoTexto.Clear();
            textoErro.Visibility = Visibility.Collapsed;
            campoTexto.Focus();
            Recarregar();
        }

        private void Excluir_Click(object sender, RoutedEventArgs e)
        {
            var id = (string)((Button)sender).Tag;
            _service.Remover(id);
            Recarregar();
        }

        private void Inicio_Click(object sender, RoutedEventArgs e)
        {
            try { Inicializacao.Definir(chkInicio.IsChecked == true); }
            catch
            {
                Erro("Não consegui alterar o início automático.");
                chkInicio.IsChecked = Inicializacao.Habilitado;
            }
        }

        private void Testar_Click(object sender, RoutedEventArgs e)
        {
            // Popup de exemplo: sem assinantes, os botões apenas fecham (não cria nada).
            new JanelaLembrete("Lembrete de teste — é assim que ele aparece 🙂") { Owner = this }.Show();
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => Close();

        private void Erro(string mensagem)
        {
            textoErro.Text = mensagem;
            textoErro.Visibility = Visibility.Visible;
        }
    }
}
