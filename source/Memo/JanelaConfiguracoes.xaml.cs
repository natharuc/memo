using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Memo.Service;

namespace Memo
{
    public partial class JanelaConfiguracoes : Window
    {
        private readonly string _temaOriginal;
        private string _temaSelecionado;
        private int _minutosSelecionado;
        private bool _salvou;

        public JanelaConfiguracoes()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);

            var cfg = Configuracoes.Atual;
            _temaOriginal = cfg.Tema;
            _temaSelecionado = cfg.Tema;
            _minutosSelecionado = cfg.DuracaoSessaoMinutos;

            Destacar(painelTema, _temaSelecionado);
            Destacar(painelDuracao, _minutosSelecionado.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Mostra as configurações. Retorna true se o usuário salvou.</summary>
        public static bool Mostrar(Window dono)
        {
            var janela = new JanelaConfiguracoes { Owner = dono };
            janela.ShowDialog();
            return janela._salvou;
        }

        private void Tema_Click(object sender, RoutedEventArgs e)
        {
            _temaSelecionado = (string)((Button)sender).Tag;
            Destacar(painelTema, _temaSelecionado);
            Tema.Aplicar(_temaSelecionado); // pré-visualização ao vivo
        }

        private void Duracao_Click(object sender, RoutedEventArgs e)
        {
            var tag = (string)((Button)sender).Tag;
            _minutosSelecionado = int.Parse(tag, CultureInfo.InvariantCulture);
            Destacar(painelDuracao, tag);
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var cfg = Configuracoes.Atual;
            cfg.Tema = _temaSelecionado;
            cfg.DuracaoSessaoMinutos = _minutosSelecionado;
            cfg.Salvar();

            Tema.Aplicar(_temaSelecionado);
            _salvou = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            // Desfaz a pré-visualização do tema se o usuário não salvou.
            if (!_salvou && !string.Equals(Tema.EhEscuro ? Configuracoes.TemaEscuro : Configuracoes.TemaClaro,
                    _temaOriginal, StringComparison.OrdinalIgnoreCase))
                Tema.Aplicar(_temaOriginal);

            base.OnClosed(e);
        }

        /// <summary>Realça o botão cujo Tag corresponde ao valor; os demais voltam ao normal.</summary>
        private void Destacar(Panel painel, string valor)
        {
            var estiloPrimario = (Style)FindResource("BotaoPrimario");
            // O estilo padrão é o implícito de Button (definido em Tema.xaml); usar
            // null aqui desativaria o estilo e cairia no visual padrão do Windows.
            var estiloPadrao = (Style)FindResource(typeof(Button));
            foreach (var filho in painel.Children)
            {
                if (filho is Button botao)
                    botao.Style = string.Equals((string)botao.Tag, valor, StringComparison.Ordinal)
                        ? estiloPrimario
                        : estiloPadrao;
            }
        }
    }
}
