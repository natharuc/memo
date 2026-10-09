using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memo.Service.Auth;

namespace Memo
{
    public partial class JanelaAuth : Window
    {
        private static JanelaAuth _aberta;

        private readonly EnteAuthService _svc = new EnteAuthService();
        private readonly List<CartaoAuthVm> _todos = new List<CartaoAuthVm>();
        private CartaoAuthVm[] _visiveis = Array.Empty<CartaoAuthVm>();
        private readonly DispatcherTimer _timer;
        private int _indice = -1;
        private bool _ocupado;

        public JanelaAuth()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) =>
            {
                foreach (var c in _todos) c.Atualizar();
            };
            Loaded += (_, __) =>
            {
                Carregar(false);
                FocarBusca();
            };
            Closed += (_, __) =>
            {
                _timer.Stop();
                if (_aberta == this) _aberta = null;
            };
        }

        public static void Mostrar()
        {
            if (_aberta != null)
            {
                if (_aberta.WindowState == WindowState.Minimized)
                    _aberta.WindowState = WindowState.Normal;
                _aberta.Activate();
                _aberta.FocarBusca();
                return;
            }
            _aberta = new JanelaAuth();
            _aberta.Show();
            _aberta.Activate();
        }

        private void Atualizar_Click(object sender, RoutedEventArgs e) => Carregar(true);

        private void Adicionar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new JanelaAuthAdicionar { Owner = this };
            if (dlg.ShowDialog() == true)
                Carregar(false);
        }

        private void Busca_Changed(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private void Cartao_Click(object sender, RoutedEventArgs e)
        {
            var vm = (sender as Button)?.Tag as CartaoAuthVm;
            if (vm == null) return;
            var i = Array.IndexOf(_visiveis, vm);
            if (i >= 0) Selecionar(i, false);
            Copiar(vm);
        }

        private void Janela_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            if (e.Key == Key.F && mods == ModifierKeys.Control)
            {
                FocarBusca();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.N && mods == ModifierKeys.Control)
            {
                Adicionar_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                if (Keyboard.FocusedElement is Button botao && !(botao.Tag is CartaoAuthVm))
                    return;
                CopiarSelecionado();
                e.Handled = true;
                return;
            }

            if ((mods & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;
            if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
                return;
            if (Keyboard.FocusedElement is TextBox caixa && (e.Key == Key.Left || e.Key == Key.Right))
            {
                var noInicio = caixa.SelectionLength == 0 && caixa.CaretIndex == 0;
                var noFim = caixa.SelectionLength == 0 && caixa.CaretIndex >= (caixa.Text ?? string.Empty).Length;
                if (e.Key == Key.Left && !noInicio) return;
                if (e.Key == Key.Right && !noFim) return;
            }

            Navegar(e.Key);
            e.Handled = true;
        }

        private void FocarBusca()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                campoBusca.Focus();
                campoBusca.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void Navegar(Key tecla)
        {
            var n = _visiveis.Length;
            if (n == 0) return;
            if (_indice < 0)
            {
                Selecionar(tecla == Key.Up || tecla == Key.Left ? n - 1 : 0, true);
                return;
            }

            var cols = Colunas();
            var i = _indice;
            if (tecla == Key.Left) i--;
            else if (tecla == Key.Right) i++;
            else if (tecla == Key.Up) i -= cols;
            else i += cols;
            if (i < 0) i = 0;
            if (i >= n) i = n - 1;
            Selecionar(i, true);
        }

        private int Colunas()
        {
            double item = 352;
            if (_visiveis.Length > 0)
            {
                var c = lista.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                if (c != null && c.ActualWidth > 1) item = c.ActualWidth;
            }
            var largura = lista.ActualWidth;
            if (largura < 1) largura = Math.Max(1, ActualWidth - 56);
            return Math.Max(1, (int)Math.Floor((largura + 0.5) / item));
        }

        private void Selecionar(int indice, bool rolar)
        {
            if (_visiveis.Length == 0 || indice < 0)
            {
                _indice = -1;
                foreach (var c in _todos) c.Selecionado = false;
                return;
            }
            if (indice >= _visiveis.Length) indice = _visiveis.Length - 1;
            _indice = indice;
            var escolhido = _visiveis[indice];
            foreach (var c in _todos) c.Selecionado = ReferenceEquals(c, escolhido);
            if (!rolar) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var el = lista.ItemContainerGenerator.ContainerFromItem(escolhido) as FrameworkElement;
                el?.BringIntoView();
            }), DispatcherPriority.Loaded);
        }

        private void CopiarSelecionado()
        {
            if (_visiveis.Length == 0) return;
            var i = _indice < 0 ? 0 : _indice;
            Selecionar(i, true);
            Copiar(_visiveis[i]);
        }

        private void Copiar(CartaoAuthVm vm)
        {
            if (vm == null || string.IsNullOrEmpty(vm.CodigoCru)) return;
            try
            {
                Clipboard.SetText(vm.CodigoCru);
                textoStatus.Text = "Copiado · " + vm.Rotulo;
            }
            catch (Exception ex)
            {
                textoStatus.Text = ex.Message;
            }
        }

        private void Carregar(bool forcar)
        {
            if (_ocupado) return;
            _ocupado = true;
            textoStatus.Text = forcar ? "Sincronizando com o Ente…" : "Carregando…";
            var svc = _svc;
            System.Threading.Tasks.Task.Run(() =>
            {
                if (!forcar)
                {
                    var cache = CacheAuth.Carregar();
                    if (!cache.Vazio && !cache.Vencido)
                        return ResultadoAuth.Ok(null, cache.Entradas);
                }
                return svc.Sincronizar();
            })
                .ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _ocupado = false;
                        if (t.IsFaulted)
                        {
                            textoStatus.Text = t.Exception?.GetBaseException().Message ?? "Falha ao carregar.";
                            return;
                        }
                        var r = t.Result;
                        if (!r.Sucesso)
                        {
                            _todos.Clear();
                            _visiveis = Array.Empty<CartaoAuthVm>();
                            _indice = -1;
                            lista.ItemsSource = null;
                            textoStatus.Text = r.Mensagem;
                            return;
                        }
                        _todos.Clear();
                        foreach (var entrada in r.Entradas ?? new List<EntradaAuth>())
                            _todos.Add(new CartaoAuthVm(entrada));
                        AplicarFiltro();
                        _timer.Start();
                        textoStatus.Text = _todos.Count == 1
                            ? "1 chave"
                            : _todos.Count + " chaves";
                    });
                });
        }

        private void AplicarFiltro()
        {
            var anterior = _indice >= 0 && _indice < _visiveis.Length ? _visiveis[_indice] : null;
            var q = (campoBusca.Text ?? string.Empty).Trim();
            if (q.Length == 0)
            {
                _visiveis = _todos.ToArray();
            }
            else
            {
                var partes = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var visiveis = new List<CartaoAuthVm>();
                foreach (var c in _todos)
                {
                    var ok = true;
                    foreach (var p in partes)
                    {
                        if (c.Rotulo.IndexOf(p, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) visiveis.Add(c);
                }
                _visiveis = visiveis.ToArray();
            }
            lista.ItemsSource = _visiveis;
            var novo = -1;
            if (anterior != null)
            {
                var i = Array.IndexOf(_visiveis, anterior);
                novo = i >= 0 ? i : (_visiveis.Length > 0 ? 0 : -1);
            }
            Selecionar(novo, false);
        }
    }

    public sealed class CartaoAuthVm : INotifyPropertyChanged
    {
        private readonly EntradaAuth _entrada;
        private string _codigo;
        private string _proximo;
        private string _segundos;

        public CartaoAuthVm(EntradaAuth entrada)
        {
            _entrada = entrada;
            Atualizar();
        }

        public string Issuer => string.IsNullOrWhiteSpace(_entrada.Issuer) ? _entrada.Rotulo : _entrada.Issuer;
        public string Conta => _entrada.Account ?? string.Empty;
        public string Rotulo => _entrada.Rotulo;
        public string Codigo => _codigo;
        public string CodigoCru { get; private set; }
        public string Proximo => _proximo;
        public string Segundos => _segundos;

        private bool _selecionado;
        public bool Selecionado
        {
            get => _selecionado;
            set
            {
                if (_selecionado == value) return;
                _selecionado = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selecionado)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Atualizar()
        {
            if (!GeradorTotp.Tentar(_entrada, out var codigo, out var seg))
            {
                CodigoCru = null;
                Definir("—", "—", "");
                return;
            }
            CodigoCru = codigo;
            GeradorTotp.Proximo(_entrada, out var prox);
            Definir(Agrupar(codigo), Agrupar(prox), seg + "s");
        }

        private void Definir(string codigo, string proximo, string segundos)
        {
            if (_codigo == codigo && _proximo == proximo && _segundos == segundos) return;
            _codigo = codigo;
            _proximo = proximo;
            _segundos = segundos;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Codigo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Proximo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Segundos)));
        }

        private static string Agrupar(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return string.Empty;
            var partes = new List<string>();
            for (var i = 0; i < codigo.Length; i += 3)
                partes.Add(codigo.Substring(i, Math.Min(3, codigo.Length - i)));
            return string.Join(" ", partes);
        }
    }

    /// <summary>Quebra os cartões em linhas e centra cada linha na janela.</summary>
    public class FaixaCentralizada : Panel
    {
        protected override Size MeasureOverride(Size available)
        {
            var largura = double.IsInfinity(available.Width) ? 10000 : available.Width;
            double x = 0, y = 0, linha = 0, maior = 0;
            foreach (UIElement filho in InternalChildren)
            {
                filho.Measure(new Size(largura, double.PositiveInfinity));
                var w = filho.DesiredSize.Width;
                var h = filho.DesiredSize.Height;
                if (x > 0 && x + w > largura)
                {
                    maior = Math.Max(maior, x);
                    y += linha;
                    x = 0;
                    linha = 0;
                }
                x += w;
                linha = Math.Max(linha, h);
            }
            maior = Math.Max(maior, x);
            return new Size(double.IsInfinity(available.Width) ? maior : available.Width, y + linha);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var linhas = new List<List<UIElement>>();
            var alturas = new List<double>();
            var atual = new List<UIElement>();
            double x = 0, linha = 0;
            foreach (UIElement filho in InternalChildren)
            {
                var w = filho.DesiredSize.Width;
                if (atual.Count > 0 && x + w > finalSize.Width + 0.5)
                {
                    linhas.Add(atual);
                    alturas.Add(linha);
                    atual = new List<UIElement>();
                    x = 0;
                    linha = 0;
                }
                atual.Add(filho);
                x += w;
                linha = Math.Max(linha, filho.DesiredSize.Height);
            }
            if (atual.Count > 0)
            {
                linhas.Add(atual);
                alturas.Add(linha);
            }

            double y = 0;
            for (var i = 0; i < linhas.Count; i++)
            {
                double ocupado = 0;
                foreach (var f in linhas[i]) ocupado += f.DesiredSize.Width;
                var cx = Math.Max(0, (finalSize.Width - ocupado) / 2);
                foreach (var f in linhas[i])
                {
                    f.Arrange(new Rect(cx, y, f.DesiredSize.Width, f.DesiredSize.Height));
                    cx += f.DesiredSize.Width;
                }
                y += alturas[i];
            }
            return finalSize;
        }
    }
}
