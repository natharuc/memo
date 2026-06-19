using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memo.Service.Classes;
using Memo.Services;

namespace Memo
{
    public partial class JanelaPrincipal : Window
    {
        private const string Mascara = "••••••••••••";

        private readonly MemoService _service;
        private readonly ObservableCollection<Documento> _visiveis = new ObservableCollection<Documento>();
        private readonly DispatcherTimer _timerSessao;
        private List<Documento> _todos;
        private bool _valorVisivel;

        public JanelaPrincipal(MemoService service)
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _service = service;
            Title = $"Memo — {_visiveis.Count}";
            listaDocumentos.ItemsSource = _visiveis;

            Recarregar();

            _timerSessao = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerSessao.Tick += (_, __) => TickSessao();
            _timerSessao.Start();
            AtualizarBadge();

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

            if (doc == null)
            {
                painelDetalhes.Visibility = Visibility.Collapsed;
                return;
            }

            painelDetalhes.Visibility = Visibility.Visible;
            textoChave.Text = doc.Key;

            // Mantém a escolha de mostrar/ocultar ao navegar entre documentos.
            campoValor.Text = _valorVisivel ? doc.Value : Mascara;
            botaoMostrar.Content = _valorVisivel ? "Ocultar" : "Mostrar";
        }

        // ---------------- eventos ----------------

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void Busca_TextChanged(object sender, TextChangedEventArgs e) => Filtrar();

        private void Busca_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_visiveis.Count == 0) return;

            if (e.Key == Key.Enter)
            {
                if (listaDocumentos.SelectedIndex < 0) listaDocumentos.SelectedIndex = 0;
                Copiar();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Move o foco do teclado para a lista, selecionando o item atual (ou o primeiro).
                var indice = listaDocumentos.SelectedIndex < 0 ? 0 : listaDocumentos.SelectedIndex;
                listaDocumentos.SelectedIndex = indice;
                FocarItem(indice);
                e.Handled = true;
            }
        }

        /// <summary>Dá foco de teclado ao contêiner do item, para as setas navegarem na lista.</summary>
        private void FocarItem(int indice)
        {
            listaDocumentos.ScrollIntoView(listaDocumentos.SelectedItem);
            listaDocumentos.UpdateLayout();

            if (listaDocumentos.ItemContainerGenerator.ContainerFromIndex(indice) is ListBoxItem item)
                item.Focus();
            else
                listaDocumentos.Focus();
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
            else if (e.Key == Key.Up && listaDocumentos.SelectedIndex <= 0)
            {
                // Voltar da primeira posição devolve o foco para a busca.
                campoBusca.Focus();
                e.Handled = true;
            }
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

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            if (JanelaConfiguracoes.Mostrar(this))
            {
                _service.Cofre.RenovarSessao(); // aplica a nova duração ao prazo atual
                AtualizarBadge();
            }
        }

        private void Lembretes_Click(object sender, RoutedEventArgs e)
        {
            new JanelaLembretes(new Memo.Service.Lembretes.LembreteService()) { Owner = this }.ShowDialog();
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

            if (!JanelaDialogo.Confirmar(this, "Confirmar exclusão",
                    $"Excluir \"{doc.Key}\"?", perigo: true))
                return;

            _service.Deletar(doc);
            Recarregar();
            Status($"\"{doc.Key}\" excluído");
        }

        private void Status(string mensagem) => textoStatus.Text = mensagem;

        // ---------------- sessão / badge ----------------

        private void TickSessao()
        {
            // A sessão vem do DISCO (estado compartilhado com o memo-cli). Se o CLI
            // trancou (apagou a sessão) ou o prazo venceu, a tela tranca também.
            var expira = _service.Cofre.ExpiraEmDiscoUtc();
            if (expira == null || expira.Value <= DateTime.UtcNow)
            {
                Bloquear();
                return;
            }
            AtualizarBadge();
        }

        private void AtualizarBadge()
        {
            var expira = _service.Cofre.ExpiraEmDiscoUtc();
            if (expira == null)
            {
                botaoSessao.Content = "🔒 bloqueado";
                return;
            }

            var ts = expira.Value - DateTime.UtcNow;
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;

            string texto;
            if (ts.TotalDays >= 1) texto = $"{(int)ts.TotalDays}d {ts.Hours:00}h";
            else if (ts.TotalHours >= 1) texto = $"{(int)ts.TotalHours}h {ts.Minutes:00}m";
            else texto = $"{ts.Minutes:00}:{ts.Seconds:00}";

            botaoSessao.Content = "🔒 " + texto;
            botaoSessao.Foreground = ts.TotalSeconds <= 60
                ? (Brush)FindResource("CorPerigo")
                : (Brush)FindResource("CorTextoFraco");
        }

        /// <summary>Clique no badge: tranca o cofre agora (ex.: antes de emprestar o PC).</summary>
        private void Sessao_Click(object sender, RoutedEventArgs e) => Bloquear();

        /// <summary>
        /// Tranca: limpa os segredos da tela, esquece a chave e mostra o overlay de
        /// bloqueio. Usado tanto pelo clique no badge quanto pela expiração da sessão.
        /// </summary>
        private void Bloquear()
        {
            _timerSessao.Stop();

            _valorVisivel = false;
            _todos = new List<Documento>();
            _visiveis.Clear();
            painelDetalhes.Visibility = Visibility.Collapsed;
            campoBusca.Clear();

            _service.Cofre.Trancar();

            botaoSessao.Content = "🔒 Trancado";
            overlayBloqueio.Visibility = Visibility.Visible;
        }

        /// <summary>Botão "Destrancar" do overlay: pede a senha e recarrega.</summary>
        private void Destrancar_Click(object sender, RoutedEventArgs e)
        {
            if (!JanelaSenha.Solicitar(_service.Cofre))
                return; // continua trancado se cancelar

            overlayBloqueio.Visibility = Visibility.Collapsed;
            Recarregar();
            _timerSessao.Start();
            AtualizarBadge();
            campoBusca.Focus();
        }
    }
}
