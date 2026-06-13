using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memo.Service.Classes;
using Memo.Services;

namespace Memo
{
    public partial class JanelaPrincipal : Window
    {
        private const string Mascara = "••••••••••••";

        private readonly MemoService _service;
        private readonly ObservableCollection<Documento> _visiveis = new ObservableCollection<Documento>();
        private List<Documento> _todos;
        private bool _valorVisivel;

        public JanelaPrincipal(MemoService service)
        {
            InitializeComponent();
            Nativo.AplicarBarraTituloEscura(this);

            _service = service;
            Title = $"Memo — {_visiveis.Count}";
            listaDocumentos.ItemsSource = _visiveis;

            Recarregar();
            Loaded += (_, __) => campoBusca.Focus();
        }

        private void Recarregar(string selecionarKey = null)
        {
            _todos = _service.GetDocumentos();
            Filtrar();

            if (selecionarKey != null)
            {
                var alvo = _visiveis.FirstOrDefault(d => d.Key == selecionarKey);
                if (alvo != null) listaDocumentos.SelectedItem = alvo;
            }
        }

        private void Filtrar()
        {
            var termo = campoBusca.Text?.Trim();

            IEnumerable<Documento> resultado = _todos;
            if (!string.IsNullOrEmpty(termo))
                resultado = _todos.Where(d => d.Key != null &&
                    d.Key.IndexOf(termo, StringComparison.OrdinalIgnoreCase) >= 0);

            _visiveis.Clear();
            foreach (var d in resultado) _visiveis.Add(d);

            Title = $"Memo — {_todos.Count}";
        }

        private Documento Selecionado => listaDocumentos.SelectedItem as Documento;

        private void AtualizarDetalhes()
        {
            var doc = Selecionado;
            _valorVisivel = false;
            botaoMostrar.Content = "Mostrar";

            if (doc == null)
            {
                painelDetalhes.Visibility = Visibility.Collapsed;
                return;
            }

            painelDetalhes.Visibility = Visibility.Visible;
            textoChave.Text = doc.Key;
            campoValor.Text = Mascara;
        }

        // ---------------- eventos ----------------

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void Busca_TextChanged(object sender, TextChangedEventArgs e) => Filtrar();

        private void Busca_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _visiveis.Count > 0)
            {
                if (listaDocumentos.SelectedIndex < 0) listaDocumentos.SelectedIndex = 0;
                Copiar();
            }
            else if (e.Key == Key.Down && _visiveis.Count > 0)
            {
                if (listaDocumentos.SelectedIndex < 0) listaDocumentos.SelectedIndex = 0;
                listaDocumentos.Focus();
            }
        }

        private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e) => AtualizarDetalhes();

        private void Lista_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Selecionado != null) Copiar();
        }

        private void Lista_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Copiar();
            else if (e.Key == Key.Delete) Excluir();
        }

        private void Copiar_Click(object sender, RoutedEventArgs e) => Copiar();

        private void Mostrar_Click(object sender, RoutedEventArgs e)
        {
            var doc = Selecionado;
            if (doc == null) return;

            _valorVisivel = !_valorVisivel;
            campoValor.Text = _valorVisivel ? doc.Value : Mascara;
            botaoMostrar.Content = _valorVisivel ? "Ocultar" : "Mostrar";
        }

        private void Editar_Click(object sender, RoutedEventArgs e)
        {
            var doc = Selecionado;
            if (doc == null) return;

            var editado = JanelaEditar.Editar(this, doc);
            if (editado == null) return;

            _service.Salvar(editado);
            Recarregar(editado.Key);
            Status($"\"{editado.Key}\" salvo");
        }

        private void Novo_Click(object sender, RoutedEventArgs e)
        {
            var novo = JanelaEditar.Criar(this);
            if (novo == null) return;

            _service.Salvar(novo);
            Recarregar(novo.Key);
            Status($"\"{novo.Key}\" criado");
        }

        private void Excluir_Click(object sender, RoutedEventArgs e) => Excluir();

        private void Copiar()
        {
            var doc = Selecionado;
            if (doc == null) return;

            _service.CopiarValor(doc);
            Status($"\"{doc.Key}\" copiado para a área de transferência");
        }

        private void Excluir()
        {
            var doc = Selecionado;
            if (doc == null) return;

            var resposta = MessageBox.Show(this,
                $"Excluir \"{doc.Key}\"?", "Confirmar exclusão",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (resposta != MessageBoxResult.Yes) return;

            _service.Deletar(doc);
            Recarregar();
            Status($"\"{doc.Key}\" excluído");
        }

        private void Status(string mensagem) => textoStatus.Text = mensagem;
    }
}
