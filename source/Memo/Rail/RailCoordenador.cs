using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Service;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Orquestra o Memo Rail a partir do timer da bandeja: pergunta a missão do
    /// dia, dispara check-ins periódicos e avisa desvios de foco. Todas as
    /// decisões anti-perturbação (cooldown, ociosidade, horário, silenciados)
    /// vivem aqui. Estado de monitoramento fica só em memória.
    /// </summary>
    internal sealed class RailCoordenador
    {
        private static readonly TimeSpan OciosidadeMaxima = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan SilencioTrabalhando = TimeSpan.FromMinutes(60);

        private readonly RailService _rail = new RailService();
        private readonly TimeSpan _tick;

        // Estado em memória (privacidade: nada disso vai para disco).
        private double _minutosEmDistracao;
        private DateTime _ultimaAparicao = DateTime.MinValue;
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

            var missao = _rail.MissaoDeHoje();

            // 1) Sem missão hoje: pergunta uma vez (por dia, por instância).
            if (missao == null || missao.Itens.Count == 0)
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

            var cooldownOk = agora - _ultimaAparicao >= TimeSpan.FromMinutes(cfg.CooldownMinutos);

            // 2) Desvio de foco: distração contínua além do limite.
            var termo = TermoDistracaoAtiva(cfg);
            _minutosEmDistracao = termo != null ? _minutosEmDistracao + _tick.TotalMinutes : 0;

            if (termo != null && _minutosEmDistracao >= cfg.DesvioMinutos && cooldownOk)
            {
                MostrarDesvio(termo, pendente);
                return;
            }

            // 3) Check-in por tempo.
            if (missao.UltimoCheckIn == null)
            {
                // Sem check-in registrado hoje: ancora agora para contar o intervalo.
                _rail.RegistrarCheckIn();
                return;
            }

            var proximo = _checkInAdiadoPara
                          ?? missao.UltimoCheckIn.Value.AddMinutes(cfg.CheckInMinutos);
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

            // "Estou trabalhando" silencia o termo por uma janela de tempo.
            if (_silenciados.TryGetValue(termo, out var ate) && DateTime.Now < ate)
                return null;

            return termo;
        }

        // ----------------- aparições -----------------

        private void MostrarCheckIn(ItemMissao tarefa)
        {
            var popup = JanelaCerebrinho.CheckIn(tarefa.Texto, tarefa.Link);

            popup.Concluiu += () =>
            {
                _rail.ConcluirPorId(tarefa.Id);
                var proxima = _rail.MissaoDeHoje()?.ProximaPendente();
                Toast.Mostrar(proxima == null
                    ? "Missão do dia concluída! 🎉"
                    : $"Boa! Próxima: \"{proxima.Texto}\"", true);
            };
            popup.Adiou += () => _checkInAdiadoPara = DateTime.Now.AddMinutes(15);

            Abrir(popup, aoFechar: () =>
            {
                _rail.RegistrarCheckIn();
                if (_checkInAdiadoPara == null || _checkInAdiadoPara <= DateTime.Now)
                    _checkInAdiadoPara = null;
            });
        }

        private void MostrarDesvio(string termo, ItemMissao tarefa)
        {
            var popup = JanelaCerebrinho.Desvio(termo, tarefa.Texto, tarefa.Link);

            popup.Trabalhando += () => _silenciados[termo] = DateTime.Now.Add(SilencioTrabalhando);

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
