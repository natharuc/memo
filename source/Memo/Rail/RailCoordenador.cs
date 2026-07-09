using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Orquestra o Memo Rail a partir do timer da bandeja: pergunta a missão do
    /// dia, dispara check-ins periódicos e avisa desvios de foco (com a
    /// insistência do nível configurado). Todas as decisões anti-perturbação
    /// (cooldowns, ociosidade, horário, dias, silenciados) vivem aqui.
    /// Estado de monitoramento fica só em memória.
    /// </summary>
    internal sealed class RailCoordenador
    {
        private static readonly TimeSpan OciosidadeMaxima = TimeSpan.FromMinutes(3);

        private readonly RailService _rail = new RailService();
        private readonly BloqueioFoco _bloqueio = new BloqueioFoco();
        private readonly TimeSpan _tick;

        // Estado em memória (privacidade: nada disso vai para disco).
        private double _minutosEmDistracao;
        private DateTime _ultimaAparicao = DateTime.MinValue;   // qualquer popup
        private DateTime _ultimoDesvio = DateTime.MinValue;     // só avisos de desvio
        private DateTime? _checkInAdiadoPara;
        private readonly Dictionary<string, DateTime> _silenciados =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private string _dataMissaoPerguntada;
        private bool _popupAberto;

        public RailCoordenador(TimeSpan intervaloTick)
        {
            _tick = intervaloTick;
        }

        /// <summary>Chamado a cada tick do agendador da bandeja (thread de UI).</summary>
        public void Tick()
        {
            // Modo foco funciona independentemente dos gates abaixo (horário etc.)
            // e mostra/esconde o backdrop conforme a janela ativa.
            _bloqueio.Avaliar();

            var cfg = Configuracoes.Atual.Rail;
            if (cfg == null || !cfg.Habilitado) return;

            var agora = DateTime.Now;
            if (!cfg.DentroDoHorario(agora)) return;
            if (_popupAberto) return;

            // Usuário longe do PC: não conta distração nem perturba.
            if (MonitorFoco.TempoOcioso() > OciosidadeMaxima)
            {
                _minutosEmDistracao = 0;
                return;
            }

            // Uma única leitura do rail.json por tick (o timer roda em intervalo curto).
            var (missao, ultimoCheckIn) = _rail.Estado();

            // 1) Sem missão para hoje (nem atrasadas): pergunta uma vez por dia.
            if (missao.DeHoje.Count == 0 && missao.Atrasadas.Count == 0)
            {
                var hoje = agora.ToString("yyyy-MM-dd");
                if (cfg.PerguntarMissao && _dataMissaoPerguntada != hoje)
                {
                    _dataMissaoPerguntada = hoje;
                    JanelaMissao.Mostrar();
                }
                _minutosEmDistracao = 0;
                return;
            }

            var pendente = missao.ProximaPendente();
            if (pendente == null)
            {
                _minutosEmDistracao = 0; // missão cumprida: silêncio até amanhã
                return;
            }

            // 2) Desvio de foco: parâmetros vêm do nível de distração.
            var desvio = cfg.Desvio();
            var termo = TermoDistracaoAtiva(cfg);
            _minutosEmDistracao = termo != null ? _minutosEmDistracao + _tick.TotalMinutes : 0;

            // Se o modo foco está ativo, o backdrop já cuida da distração — não
            // precisa também abrir o cerebrinho.
            if (termo != null && !_bloqueio.Ativo &&
                _minutosEmDistracao >= desvio.AvisarAposMinutos &&
                agora - _ultimoDesvio >= TimeSpan.FromMinutes(desvio.CooldownMinutos))
            {
                MostrarDesvio(termo, pendente, desvio);
                return;
            }

            // 3) Check-in por tempo (cooldown próprio, independente do desvio).
            if (ultimoCheckIn == null || ultimoCheckIn.Value.Date != agora.Date)
            {
                // Primeiro tick do dia: ancora agora para contar o intervalo.
                _rail.RegistrarCheckIn();
                return;
            }

            var proximo = _checkInAdiadoPara ?? ultimoCheckIn.Value.AddMinutes(cfg.CheckInMinutos);
            var cooldownOk = agora - _ultimaAparicao >= TimeSpan.FromMinutes(cfg.CooldownMinutos);
            if (agora >= proximo && cooldownOk)
                MostrarCheckIn(pendente);
        }

        // ----------------- distração -----------------

        /// <summary>Termo de distração da janela ativa, ou null (inclui silenciados).</summary>
        private string TermoDistracaoAtiva(RailConfig cfg)
        {
            var janela = MonitorFoco.ObterJanelaAtiva();
            if (janela == null) return null;

            var termo = cfg.Distracoes?.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t) &&
                RailService.EhDistracao(janela.Processo, janela.Titulo, new[] { t }));

            if (termo == null) return null;

            // "Estou trabalhando" silencia o termo por uma janela de tempo (do nível).
            if (_silenciados.TryGetValue(termo, out var ate) && DateTime.Now < ate)
                return null;

            return termo;
        }

        // ----------------- aparições -----------------

        private static string TextoTarefa(ItemMissao tarefa)
        {
            var texto = FormatadorTexto.SemFormatacao(tarefa.Texto);
            return tarefa.Atrasada(DateTime.Now.ToString("yyyy-MM-dd"))
                ? $"Atrasada: {texto}"
                : texto;
        }

        private static TimeSpan Realocar()
        {
            var min = Math.Max(1, Configuracoes.Atual.Rail?.RealocarMinutos ?? 2);
            return TimeSpan.FromMinutes(min);
        }

        private void MostrarCheckIn(ItemMissao tarefa)
        {
            var popup = JanelaCerebrinho.CheckIn(TextoTarefa(tarefa), tarefa.Link, Realocar());

            popup.Concluiu += () =>
            {
                _rail.ConcluirPorId(tarefa.Id);
                var proxima = _rail.MissaoAtual().ProximaPendente();
                Toast.Mostrar(proxima == null
                    ? "Missão do dia concluída! 🎉"
                    : $"Boa! Próxima: \"{FormatadorTexto.SemFormatacao(proxima.Texto)}\"", true);
            };
            popup.Adiou += () => _checkInAdiadoPara = DateTime.Now.AddMinutes(15);

            Abrir(popup, aoFechar: () =>
            {
                _rail.RegistrarCheckIn();
                if (_checkInAdiadoPara == null || _checkInAdiadoPara <= DateTime.Now)
                    _checkInAdiadoPara = null;
            });
        }

        private void MostrarDesvio(string termo, ItemMissao tarefa, ParametrosDesvio desvio)
        {
            var popup = JanelaCerebrinho.Desvio(termo, TextoTarefa(tarefa), tarefa.Link, Realocar());

            popup.Trabalhando += () =>
                _silenciados[termo] = DateTime.Now.AddMinutes(desvio.SilencioTrabalhandoMinutos);

            popup.PrecisoFocar += duracao =>
            {
                _bloqueio.Iniciar(duracao, FormatadorTexto.SemFormatacao(tarefa.Texto));
                Toast.Mostrar($"Modo foco por {duracao.TotalMinutes:0} min — distrações bloqueadas 🧠", true);
            };

            _ultimoDesvio = DateTime.Now;
            Abrir(popup, aoFechar: () => _minutosEmDistracao = 0);
        }

        private void Abrir(JanelaCerebrinho popup, Action aoFechar)
        {
            _popupAberto = true;
            _ultimaAparicao = DateTime.Now;

            popup.Closed += (_, __) =>
            {
                _popupAberto = false;
                aoFechar?.Invoke();
            };
            popup.Show();
        }
    }
}
