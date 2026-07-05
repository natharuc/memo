using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Memo.Service;
using Memo.Service.Notificacoes;
using Memo.Services;

namespace Memo
{
    public partial class JanelaConfiguracoes : Window
    {
        private readonly string _temaOriginal;
        private string _temaSelecionado;
        private int _minutosSelecionado;
        private bool _salvou;

        private readonly NotificacaoService _notificacoes = new NotificacaoService();

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

            CarregarNotificacoes();
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

            _notificacoes.Salvar(LerNotificacoes());

            Tema.Aplicar(_temaSelecionado);
            _salvou = true;
            Close();
        }

        // ----------------- Notificações -----------------

        private void CarregarNotificacoes()
        {
            var n = _notificacoes.Carregar();

            tgHabilitado.IsChecked = n.Telegram.Habilitado;
            tgToken.Text = n.Telegram.BotToken;
            tgChatId.Text = n.Telegram.ChatId;

            emHabilitado.IsChecked = n.Email.Habilitado;
            emServidor.Text = n.Email.Servidor;
            emPorta.Text = n.Email.Porta.ToString(CultureInfo.InvariantCulture);
            emSsl.IsChecked = n.Email.UsarSsl;
            emUsuario.Text = n.Email.Usuario;
            emSenha.Password = n.Email.Senha ?? string.Empty;
            emDe.Text = n.Email.De;
            emPara.Text = n.Email.Para;
        }

        private NotificacaoConfig LerNotificacoes() => new NotificacaoConfig
        {
            Telegram = new CanalTelegram
            {
                Habilitado = tgHabilitado.IsChecked == true,
                BotToken = tgToken.Text?.Trim(),
                ChatId = tgChatId.Text?.Trim()
            },
            Email = new CanalEmail
            {
                Habilitado = emHabilitado.IsChecked == true,
                Servidor = emServidor.Text?.Trim(),
                Porta = int.TryParse(emPorta.Text, out var p) ? p : 587,
                UsarSsl = emSsl.IsChecked == true,
                Usuario = emUsuario.Text?.Trim(),
                Senha = emSenha.Password,
                De = emDe.Text?.Trim(),
                Para = emPara.Text?.Trim()
            }
        };

        private async void TestarTelegram_Click(object sender, RoutedEventArgs e)
        {
            var canal = LerNotificacoes().Telegram;
            await TestarAsync(() => _notificacoes.EnviarTelegram(canal, "Memo", "Notificação de teste do Memo."));
        }

        private async void TestarEmail_Click(object sender, RoutedEventArgs e)
        {
            var canal = LerNotificacoes().Email;
            await TestarAsync(() => _notificacoes.EnviarEmail(canal, "Memo", "Notificação de teste do Memo."));
        }

        private async Task TestarAsync(Func<ResultadoCli> envio)
        {
            MostrarStatus("Enviando teste…", ok: null);
            var r = await Task.Run(envio);
            MostrarStatus(r.Mensagem, r.Sucesso);
        }

        private void MostrarStatus(string texto, bool? ok)
        {
            notifStatus.Text = texto;
            notifStatus.Visibility = Visibility.Visible;
            notifStatus.SetResourceReference(ForegroundProperty,
                ok == false ? "CorPerigo" : ok == true ? "CorDestaque" : "CorTextoFraco");
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
