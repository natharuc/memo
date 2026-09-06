using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Componente reutilizável com as preferências do Memo Rail. Usado na aba
    /// "Memo Rail" das Configurações e na <see cref="JanelaConfigRail"/> (aberta
    /// direto da janela da missão). Carrega o estado atual ao ser criado; o host
    /// chama <see cref="AplicarEm"/> ao salvar.
    /// </summary>
    public partial class PainelRail : UserControl
    {
        private int _checkIn;
        private NivelDistracao _nivel;
        private readonly System.Collections.Generic.HashSet<DayOfWeek> _dias =
            new System.Collections.Generic.HashSet<DayOfWeek>();

        public PainelRail()
        {
            InitializeComponent();

            MascaraHora.Aplicar(railInicio);
            MascaraHora.Aplicar(railFim);

            Carregar();
        }

        /// <summary>Preenche os controles com a configuração salva atual.</summary>
        private void Carregar()
        {
            var r = Configuracoes.Atual.Rail ?? new RailConfig();

            railHabilitado.IsChecked = r.Habilitado;
            railPerguntar.IsChecked = r.PerguntarMissao;
            railInicio.Text = r.HoraInicio;
            railFim.Text = r.HoraFim;
            railDistracoes.Text = string.Join(Environment.NewLine,
                r.Distracoes ?? new System.Collections.Generic.List<string>());

            railPausarTelaCheia.IsChecked = r.PausarEmTelaCheia;
            railOcultarCapturas.IsChecked = r.OcultarDeCapturas;
            railAppsPausam.Text = string.Join(Environment.NewLine,
                r.AppsQuePausam ?? new System.Collections.Generic.List<string>());

            _checkIn = r.CheckInMinutos;
            _nivel = r.Nivel;

            _dias.Clear();
            foreach (var dia in r.DiasEfetivos()) _dias.Add(dia);

            Destacar(painelRailCheckIn, _checkIn.ToString(CultureInfo.InvariantCulture));
            Destacar(painelRailNivel, _nivel.ToString());
            AtualizarNivelDescricao();
            AtualizarDias();
        }

        /// <summary>Escreve o estado dos controles no <paramref name="r"/> informado.</summary>
        public void AplicarEm(RailConfig r)
        {
            if (r == null) return;

            r.Habilitado = railHabilitado.IsChecked == true;
            r.PerguntarMissao = railPerguntar.IsChecked == true;
            r.CheckInMinutos = _checkIn;
            r.Nivel = _nivel;
            r.HoraInicio = railInicio.Text?.Trim();
            r.HoraFim = railFim.Text?.Trim();
            r.DiasAtivos = _dias.OrderBy(d => d).ToList();
            r.Distracoes = railDistracoes.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();

            r.PausarEmTelaCheia = railPausarTelaCheia.IsChecked == true;
            r.OcultarDeCapturas = railOcultarCapturas.IsChecked == true;
            r.AppsQuePausam = railAppsPausam.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }

        // ----------------- eventos -----------------

        private void RailCheckIn_Click(object sender, RoutedEventArgs e)
        {
            _checkIn = int.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture);
            Destacar(painelRailCheckIn, _checkIn.ToString(CultureInfo.InvariantCulture));
        }

        private void RailNivel_Click(object sender, RoutedEventArgs e)
        {
            _nivel = (NivelDistracao)Enum.Parse(typeof(NivelDistracao), (string)((Button)sender).Tag);
            Destacar(painelRailNivel, _nivel.ToString());
            AtualizarNivelDescricao();
        }

        private void AtualizarNivelDescricao()
        {
            var p = new RailConfig { Nivel = _nivel }.Desvio();
            var quando = p.AvisarAposMinutos < 1
                ? $"~{Math.Round(p.AvisarAposMinutos * 60)}s após abrir a distração"
                : $"após {p.AvisarAposMinutos:0} min de distração";
            railNivelDescricao.Text =
                $"Avisa {quando} · repete a cada {p.CooldownMinutos:0} min · " +
                $"\"estou trabalhando\" silencia por {p.SilencioTrabalhandoMinutos:0} min.";
        }

        private void RailDia_Click(object sender, RoutedEventArgs e)
        {
            var dia = (DayOfWeek)int.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture);
            if (!_dias.Add(dia)) _dias.Remove(dia);
            if (_dias.Count == 0) _dias.Add(dia); // ao menos um dia ativo
            AtualizarDias();
        }

        /// <summary>Realça os toggles dos dias selecionados (multi-seleção).</summary>
        private void AtualizarDias()
        {
            var estiloPrimario = (Style)FindResource("BotaoPrimario");
            var estiloPadrao = (Style)FindResource(typeof(Button));
            foreach (var filho in painelRailDias.Children)
            {
                if (filho is Button botao)
                {
                    var dia = (DayOfWeek)int.Parse((string)botao.Tag, CultureInfo.InvariantCulture);
                    botao.Style = _dias.Contains(dia) ? estiloPrimario : estiloPadrao;
                }
            }
        }

        /// <summary>Realça o botão cujo Tag corresponde ao valor; os demais voltam ao normal.</summary>
        private void Destacar(Panel painel, string valor)
        {
            var estiloPrimario = (Style)FindResource("BotaoPrimario");
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
