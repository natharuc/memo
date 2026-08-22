using System.Windows;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Configurações do Memo Rail numa janela própria, abrível direto da
    /// <see cref="JanelaMissao"/>. Reaproveita o <see cref="PainelRail"/> — o mesmo
    /// componente usado na aba "Memo Rail" das Configurações.
    /// </summary>
    public partial class JanelaConfigRail : Window
    {
        private bool _salvou;

        public JanelaConfigRail()
        {
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);
        }

        /// <summary>Mostra o diálogo. Retorna true se o usuário salvou.</summary>
        public static bool Mostrar(Window dono)
        {
            var janela = new JanelaConfigRail { Owner = dono };
            janela.ShowDialog();
            return janela._salvou;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var cfg = Configuracoes.Atual;
            painelRail.AplicarEm(cfg.Rail ?? (cfg.Rail = new RailConfig()));
            cfg.Salvar();
            _salvou = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
