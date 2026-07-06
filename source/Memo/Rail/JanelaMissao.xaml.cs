using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Checklist da missão do dia: define de manhã, marca/edita durante o dia.
    /// Cada tarefa é um card com sua ação (🔗) e detalhes. Não exige o cofre
    /// destrancado (missão não é segredo).
    /// </summary>
    public partial class JanelaMissao : Window
    {
        private readonly RailService _rail = new RailService();
        private MissaoDia _missao;

        public JanelaMissao()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            _missao = _rail.MissaoDeHoje() ?? new MissaoDia();
            Recarregar();

            Loaded += (_, __) => campoNova.Focus();
        }

        public static void Mostrar(Window dono = null)
        {
            var janela = new JanelaMissao();
            if (dono != null)
            {
                janela.Owner = dono;
                janela.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            janela.Show();
            janela.Activate();
        }

        // ----------------- cards -----------------

        private void Recarregar()
        {
            painelItens.Children.Clear();

            for (var i = 0; i < _missao.Itens.Count; i++)
                painelItens.Children.Add(CriarCard(_missao.Itens[i], i + 1));

            textoProgresso.Text = _missao.Itens.Count == 0
                ? "O que vamos fazer hoje? Liste as tarefas — o Memo te mantém no trilho."
                : $"{_missao.Concluidos} de {_missao.Itens.Count} concluída(s)" +
                  (_missao.Pendentes == 0 ? " — missão cumprida! 🎉" : "");

            botaoLimpar.Visibility = _missao.Itens.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Monta o card de uma tarefa: check + texto/detalhes + ações.</summary>
        private Border CriarCard(ItemMissao item, int numero)
        {
            var grade = new Grid();
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Check de concluído.
            var check = new CheckBox
            {
                IsChecked = item.Concluido,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                ToolTip = item.Concluido ? "Desmarcar" : "Concluir",
                Tag = item
            };
            check.Checked += Item_Alterado;
            check.Unchecked += Item_Alterado;
            Grid.SetColumn(check, 0);
            grade.Children.Add(check);

            // Texto + linha de detalhes.
            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titulo = new TextBlock
            {
                Text = item.Texto,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            titulo.SetResourceReference(TextBlock.ForegroundProperty,
                item.Concluido ? "CorTextoFraco" : "CorTexto");
            if (item.Concluido) titulo.TextDecorations = TextDecorations.Strikethrough;
            textos.Children.Add(titulo);

            var detalhes = $"#{numero}";
            if (!string.IsNullOrWhiteSpace(item.Link))
                detalhes += $"  ·  🔗 {HostDoLink(item.Link)}";
            if (item.Concluido && item.ConcluidoEm.HasValue)
                detalhes += $"  ·  concluída às {item.ConcluidoEm:HH:mm}";

            var subtitulo = new TextBlock
            {
                Text = detalhes,
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0)
            };
            subtitulo.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoFraco");
            textos.Children.Add(subtitulo);

            Grid.SetColumn(textos, 1);
            grade.Children.Add(textos);

            // Ações do card.
            var acoes = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            if (!string.IsNullOrWhiteSpace(item.Link))
            {
                var abrir = new Button
                {
                    Content = "🔗 Abrir",
                    FontSize = 12,
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    ToolTip = item.Link,
                    Tag = item
                };
                abrir.Click += Acao_Click;
                acoes.Children.Add(abrir);
            }

            var remover = new Button
            {
                Content = "✕",
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                ToolTip = "Remover tarefa",
                Tag = item
            };
            remover.SetResourceReference(ForegroundProperty, "CorTextoFraco");
            remover.Click += Remover_Click;
            acoes.Children.Add(remover);

            Grid.SetColumn(acoes, 2);
            grade.Children.Add(acoes);

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 11, 12, 11),
                Margin = new Thickness(0, 0, 0, 8),
                Child = grade
            };
            card.SetResourceReference(Border.BackgroundProperty, "CorPainel");
            return card;
        }

        private static string HostDoLink(string link) =>
            Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.Host : link;

        // ----------------- eventos -----------------

        private void Item_Alterado(object sender, RoutedEventArgs e)
        {
            var check = (CheckBox)sender;
            var item = (ItemMissao)check.Tag;
            item.Concluido = check.IsChecked == true;
            item.ConcluidoEm = item.Concluido ? DateTime.Now : (DateTime?)null;
            Salvar();
        }

        private void Acao_Click(object sender, RoutedEventArgs e)
        {
            var item = (ItemMissao)((Button)sender).Tag;
            RailService.AbrirLink(item.Link);
        }

        private void Remover_Click(object sender, RoutedEventArgs e)
        {
            var item = (ItemMissao)((Button)sender).Tag;
            _missao.Itens.Remove(item);
            Salvar();
        }

        private void Adicionar_Click(object sender, RoutedEventArgs e) => Adicionar();

        private void Nova_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Adicionar();
        }

        private void Adicionar()
        {
            // Uma URL no texto vira automaticamente a ação (🔗) da tarefa.
            var item = RailService.CriarItem(campoNova.Text);
            if (item == null) return;

            _missao.Itens.Add(item);
            campoNova.Clear();
            campoNova.Focus();
            Salvar();
        }

        private void Testar_Click(object sender, RoutedEventArgs e)
        {
            // Prévia do cerebrinho: usa a tarefa atual (ou um exemplo), sem efeitos.
            var pendente = _missao.ProximaPendente();
            var popup = JanelaCerebrinho.CheckIn(
                pendente?.Texto ?? "exemplo: revisar o relatório",
                pendente?.Link);
            popup.Show();
        }

        private void Limpar_Click(object sender, RoutedEventArgs e)
        {
            if (!JanelaDialogo.Confirmar(this, "Recomeçar o dia",
                    "Apagar a missão de hoje e começar de novo?", perigo: true))
                return;

            _missao = new MissaoDia();
            _rail.LimparHoje();
            Recarregar();
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => Close();

        private void Salvar()
        {
            _rail.SalvarHoje(_missao);
            Recarregar();
        }
    }
}
