using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Missão do dia: atrasadas acumuladas + tarefas de hoje + próximas. Cada
    /// tarefa é um card com formatação leve, ação (🔗), edição (✏) e remoção.
    /// Não exige o cofre destrancado (missão não é segredo).
    /// </summary>
    public partial class JanelaMissao : Window
    {
        private readonly RailService _rail = new RailService();
        private MissaoVisivel _missao;

        public JanelaMissao()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            campoDataNova.SelectedDate = DateTime.Today;
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

        // ----------------- seções e cards -----------------

        private void Recarregar()
        {
            _missao = _rail.MissaoAtual();
            painelItens.Children.Clear();

            // Numeração contínua na ordem canônica (mesma da CLI).
            var numero = 1;
            var numeros = new Dictionary<string, int>();
            foreach (var item in _missao.Lista) numeros[item.Id] = numero++;

            if (_missao.Atrasadas.Count > 0)
            {
                painelItens.Children.Add(Secao("ATRASADAS", perigo: true));
                foreach (var item in _missao.Atrasadas)
                    painelItens.Children.Add(CriarCard(item, numeros[item.Id], atrasada: true));
            }

            if (_missao.Atrasadas.Count > 0 || _missao.Futuras.Count > 0)
                painelItens.Children.Add(Secao("HOJE"));
            foreach (var item in _missao.DeHoje)
                painelItens.Children.Add(CriarCard(item, numeros[item.Id]));

            if (_missao.Futuras.Count > 0)
            {
                painelItens.Children.Add(Secao("PRÓXIMAS"));
                foreach (var item in _missao.Futuras)
                    painelItens.Children.Add(CriarCard(item, numeros[item.Id], futura: true));
            }

            var total = _missao.Ativas.Count;
            textoProgresso.Text = total == 0 && _missao.Futuras.Count == 0
                ? "O que vamos fazer hoje? Liste as tarefas — o Memo te mantém no trilho."
                : $"{_missao.Concluidos} de {total} concluída(s)" +
                  (_missao.Atrasadas.Count > 0 ? $" · {_missao.Atrasadas.Count} atrasada(s)" : "") +
                  (total > 0 && _missao.Pendentes == 0 ? " — missão cumprida! 🎉" : "");

            botaoLimpar.Visibility = _missao.DeHoje.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private TextBlock Secao(string titulo, bool perigo = false)
        {
            var rotulo = new TextBlock
            {
                Text = titulo,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 6, 0, 6)
            };
            rotulo.SetResourceReference(TextBlock.ForegroundProperty, perigo ? "CorPerigo" : "CorTextoFraco");
            return rotulo;
        }

        /// <summary>Monta o card de uma tarefa: check + texto/detalhes + ações.</summary>
        private Border CriarCard(ItemMissao item, int numero, bool atrasada = false, bool futura = false)
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

            // Texto (com formatação leve) + linha de detalhes.
            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titulo = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            FormatadorTexto.AplicarInlines(titulo, item.Texto);
            titulo.SetResourceReference(TextBlock.ForegroundProperty,
                item.Concluido ? "CorTextoFraco" : "CorTexto");
            if (item.Concluido) titulo.TextDecorations = TextDecorations.Strikethrough;
            textos.Children.Add(titulo);

            var detalhes = $"#{numero}";
            var dataItem = RailService.ParseData(item.Data);
            if (atrasada && dataItem.HasValue)
                detalhes += $"  ·  de {dataItem:dd/MM}";
            if (futura && dataItem.HasValue)
                detalhes += $"  ·  para {dataItem:dd/MM}";
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
            subtitulo.SetResourceReference(TextBlock.ForegroundProperty,
                atrasada ? "CorPerigo" : "CorTextoFraco");
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

            var editar = new Button
            {
                Content = "✏",
                FontSize = 12,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "Editar tarefa",
                Tag = item
            };
            editar.Click += Editar_Click;
            acoes.Children.Add(editar);

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
                Child = grade,
                Tag = item
            };
            card.SetResourceReference(Border.BackgroundProperty, "CorPainel");
            if (atrasada)
            {
                card.BorderThickness = new Thickness(1);
                card.SetResourceReference(Border.BorderBrushProperty, "CorPerigo");
            }

            // Duplo-clique no card também abre a edição.
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) EditarItem(item);
            };

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
            _rail.AtualizarItem(item);
            Recarregar();
        }

        private void Acao_Click(object sender, RoutedEventArgs e)
        {
            var item = (ItemMissao)((Button)sender).Tag;
            RailService.AbrirLink(item.Link);
        }

        private void Editar_Click(object sender, RoutedEventArgs e) =>
            EditarItem((ItemMissao)((Button)sender).Tag);

        private void EditarItem(ItemMissao item)
        {
            if (!JanelaEditarTarefa.Editar(this, item)) return;
            _rail.AtualizarItem(item);
            Recarregar();
        }

        private void Remover_Click(object sender, RoutedEventArgs e)
        {
            var item = (ItemMissao)((Button)sender).Tag;
            _rail.RemoverItem(item.Id);
            Recarregar();
        }

        private void Adicionar_Click(object sender, RoutedEventArgs e) => Adicionar();

        private void Nova_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter adiciona; Shift+Enter quebra linha (o TextBox é multiline).
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                Adicionar();
                e.Handled = true;
            }
        }

        private void DataHoje_Click(object sender, RoutedEventArgs e) =>
            campoDataNova.SelectedDate = DateTime.Today;

        private void DataAmanha_Click(object sender, RoutedEventArgs e) =>
            campoDataNova.SelectedDate = DateTime.Today.AddDays(1);

        private void Adicionar()
        {
            var texto = campoNova.Text;
            if (string.IsNullOrWhiteSpace(texto)) return;

            // URL no texto vira a ação (🔗); a data vem do DatePicker (padrão hoje).
            _rail.Adicionar(texto, link: null, data: campoDataNova.SelectedDate ?? DateTime.Today);

            campoNova.Clear();
            campoDataNova.SelectedDate = DateTime.Today;
            campoNova.Focus();
            Recarregar();
        }

        private void Config_Click(object sender, RoutedEventArgs e) => JanelaConfigRail.Mostrar(this);

        private void Testar_Click(object sender, RoutedEventArgs e)
        {
            // Prévia do cerebrinho: usa a tarefa atual (ou um exemplo), sem efeitos.
            var pendente = _missao.ProximaPendente();
            var popup = JanelaCerebrinho.CheckIn(
                pendente != null ? FormatadorTexto.SemFormatacao(pendente.Texto) : "exemplo: revisar o relatório",
                pendente?.Link);
            popup.Show();
        }

        private void Limpar_Click(object sender, RoutedEventArgs e)
        {
            if (!JanelaDialogo.Confirmar(this, "Recomeçar o dia",
                    "Apagar as tarefas de HOJE e começar de novo?\nAs atrasadas continuam na missão.",
                    perigo: true))
                return;

            _rail.LimparHoje();
            Recarregar();
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
