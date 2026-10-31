using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Memo.Service.Auth;

namespace Memo
{
    public enum AcaoChaveAuth { Editar, Renomear, Remover }

    public partial class JanelaAuthEditar : Window
    {
        private readonly GerenciadorAuthService _gerenciador;
        private readonly EntradaAuth _selecionada;
        private readonly AcaoChaveAuth _acao;
        private ChaveAuthEditavel _chave;
        private bool _gravando;
        private bool _fechada;

        public ResultadoAuth Resultado { get; private set; }

        public JanelaAuthEditar(EntradaAuth entrada, AcaoChaveAuth acao, GerenciadorAuthService gerenciador)
        {
            _selecionada = EdicaoAuth.Copiar(entrada);
            _acao = acao;
            _gerenciador = gerenciador;
            InitializeComponent();
            Nativo.AplicarBarraTitulo(this);
            Title = textoTitulo.Text = acao == AcaoChaveAuth.Remover ? "Remover chave"
                : acao == AcaoChaveAuth.Renomear ? "Renomear chave" : "Editar chave";
            textoRotulo.Text = entrada.Rotulo;
            if (acao == AcaoChaveAuth.Remover)
            {
                painelEdicao.Visibility = Visibility.Collapsed;
                painelRemocao.Visibility = Visibility.Visible;
                botaoConfirmar.Content = "Remover do Ente";
                botaoConfirmar.Style = (Style)FindResource("BotaoPerigo");
            }
            else if (acao == AcaoChaveAuth.Renomear)
                painelSegredo.Visibility = Visibility.Collapsed;
            Loaded += async (_, __) => await CarregarAsync();
            Closed += (_, __) => { _fechada = true; campoSegredo.Clear(); campoSenhaCofre.Clear(); };
        }

        private async Task CarregarAsync()
        {
            try
            {
                var chave = await Task.Run(() => _gerenciador.Preparar(_selecionada));
                if (_fechada) return;
                _chave = chave;
                var entrada = chave.Entrada;
                textoRotulo.Text = entrada.Rotulo;
                campoServico.Text = entrada.Issuer;
                campoConta.Text = entrada.Account;
                campoPeriodo.Text = entrada.Periodo.ToString(CultureInfo.InvariantCulture);
                Selecionar(campoAlgoritmo, entrada.Algoritmo);
                Selecionar(campoDigitos, entrada.Digitos.ToString(CultureInfo.InvariantCulture));
                painelEdicao.IsEnabled = true;
                botaoConfirmar.IsEnabled = true;
                textoStatus.Text = "";
                if (_acao == AcaoChaveAuth.Remover) campoSenhaCofre.Focus();
                else { campoServico.Focus(); campoServico.SelectAll(); }
            }
            catch (Exception)
            {
                if (!_fechada) Erro("Não consegui localizar esta chave no Ente. Atualize a lista e tente novamente. Chaves idênticas não podem ser alteradas por este cache.");
            }
        }

        private static void Selecionar(ComboBox campo, string valor)
        {
            foreach (ComboBoxItem item in campo.Items)
                if (string.Equals((string)item.Content, valor, StringComparison.OrdinalIgnoreCase))
                    campo.SelectedItem = item;
        }

        private async void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            if (_gravando || _chave == null) return;
            EntradaAuth nova = null;
            if (_acao != AcaoChaveAuth.Remover)
            {
                nova = _chave.Entrada;
                nova.Issuer = campoServico.Text;
                nova.Account = campoConta.Text;
                if (_acao == AcaoChaveAuth.Editar)
                {
                    if (!string.IsNullOrEmpty(campoSegredo.Password)) nova.Secret = campoSegredo.Password;
                    nova.Algoritmo = (campoAlgoritmo.SelectedItem as ComboBoxItem)?.Content as string;
                    nova.Digitos = int.TryParse((campoDigitos.SelectedItem as ComboBoxItem)?.Content as string, out var d) ? d : 0;
                    nova.Periodo = int.TryParse(campoPeriodo.Text, out var p) ? p : 0;
                }
                try { EdicaoAuth.MontarUri(_chave.UriOriginal, nova); }
                catch (InvalidOperationException ex) { Erro(ex.Message); return; }
            }
            var senha = campoSenhaCofre.Password;
            campoSenhaCofre.Clear();
            _gravando = true;
            botaoCancelar.IsEnabled = botaoConfirmar.IsEnabled = painelEdicao.IsEnabled = painelRemocao.IsEnabled = false;
            textoStatus.SetResourceReference(ForegroundProperty, "CorTextoFraco");
            textoStatus.Text = _acao == AcaoChaveAuth.Remover ? "Validando e removendo…" : "Salvando no Ente…";
            try
            {
                var r = await Task.Run(() => _acao == AcaoChaveAuth.Remover
                    ? _gerenciador.Remover(_chave, senha)
                    : _gerenciador.Salvar(_chave, nova, _acao == AcaoChaveAuth.Renomear));
                _gravando = false;
                if (r.Sucesso)
                {
                    Resultado = r;
                    DialogResult = true;
                }
                else
                {
                    Erro(r.Mensagem);
                    campoSenhaCofre.Focus();
                }
            }
            catch (Exception) { Erro("Não foi possível concluir. Atualize a lista antes de tentar novamente."); }
            finally
            {
                senha = null;
                _gravando = false;
                if (!_fechada)
                {
                    botaoCancelar.IsEnabled = botaoConfirmar.IsEnabled = painelEdicao.IsEnabled = painelRemocao.IsEnabled = true;
                    if (_acao == AcaoChaveAuth.Remover) campoSenhaCofre.Focus();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Evita perder o resultado de uma alteração já enviada ao servidor.
            if (_gravando) e.Cancel = true;
            base.OnClosing(e);
        }

        private void Erro(string mensagem)
        {
            textoStatus.SetResourceReference(ForegroundProperty, "CorPerigo");
            textoStatus.Text = mensagem;
        }
    }
}
