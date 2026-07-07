using System;
using System.Collections.Generic;
using System.Linq;

namespace Memo.Service.Rail
{
    /// <summary>Quão rápido/insistente é o aviso de desvio de foco.</summary>
    public enum NivelDistracao { Baixo, Medio, Alto, MuitoAlto }

    /// <summary>Efeitos do nível: limiar, cooldown e silêncio do "estou trabalhando".</summary>
    public class ParametrosDesvio
    {
        public double AvisarAposMinutos { get; set; }
        public double CooldownMinutos { get; set; }
        public double SilencioTrabalhandoMinutos { get; set; }
    }

    /// <summary>
    /// Preferências do Memo Rail (assistente de foco). Não contém segredos —
    /// vive dentro de <c>Configuracoes</c> (config.json).
    /// </summary>
    public class RailConfig
    {
        /// <summary>Liga/desliga o Rail inteiro (padrão: desligado; opt-in).</summary>
        public bool Habilitado { get; set; }

        /// <summary>Perguntar a missão do dia na primeira abertura dentro do horário.</summary>
        public bool PerguntarMissao { get; set; } = true;

        /// <summary>Intervalo entre check-ins ("ainda nessa tarefa?"), em minutos.</summary>
        public int CheckInMinutos { get; set; } = 45;

        /// <summary>Silêncio mínimo entre check-ins, em minutos.</summary>
        public int CooldownMinutos { get; set; } = 10;

        /// <summary>Nível do detector de desvio (quanto maior, mais rápido e insistente).</summary>
        public NivelDistracao Nivel { get; set; } = NivelDistracao.Medio;

        /// <summary>Obsoleto (v1): substituído pelo Nivel. Mantido só para o JSON antigo.</summary>
        public int DesvioMinutos { get; set; } = 5;

        /// <summary>Janela de atuação (formato HH:mm).</summary>
        public string HoraInicio { get; set; } = "09:00";
        public string HoraFim { get; set; } = "18:00";

        /// <summary>Obsoleto (v1): substituído por DiasAtivos. Mantido para migração.</summary>
        public bool SomenteDiasUteis { get; set; } = true;

        /// <summary>
        /// Dias da semana em que o Rail atua. Null = derivar de SomenteDiasUteis
        /// (config antiga): true → seg–sex; false → todos os dias.
        /// </summary>
        public List<DayOfWeek> DiasAtivos { get; set; }

        /// <summary>
        /// Termos que marcam uma janela como distração (comparados por substring,
        /// sem diferenciar maiúsculas, contra o processo e o título da janela ativa).
        /// </summary>
        public List<string> Distracoes { get; set; } = PadraoDistracoes();

        public static List<string> PadraoDistracoes() => new List<string>
        {
            "YouTube", "Instagram", "TikTok", "Twitter", "x.com", "Reddit",
            "Netflix", "Twitch", "Facebook", "Prime Video", "Disney+", "9GAG", "Steam"
        };

        /// <summary>Efeitos do nível de distração configurado.</summary>
        public ParametrosDesvio Desvio()
        {
            switch (Nivel)
            {
                case NivelDistracao.Baixo:
                    return new ParametrosDesvio { AvisarAposMinutos = 10, CooldownMinutos = 20, SilencioTrabalhandoMinutos = 120 };
                case NivelDistracao.Alto:
                    return new ParametrosDesvio { AvisarAposMinutos = 2, CooldownMinutos = 5, SilencioTrabalhandoMinutos = 30 };
                case NivelDistracao.MuitoAlto:
                    return new ParametrosDesvio { AvisarAposMinutos = 1, CooldownMinutos = 2, SilencioTrabalhandoMinutos = 15 };
                default: // Medio
                    return new ParametrosDesvio { AvisarAposMinutos = 5, CooldownMinutos = 10, SilencioTrabalhandoMinutos = 60 };
            }
        }

        /// <summary>Dias efetivos (resolve a compatibilidade com SomenteDiasUteis).</summary>
        public List<DayOfWeek> DiasEfetivos()
        {
            if (DiasAtivos != null && DiasAtivos.Count > 0) return DiasAtivos;

            return SomenteDiasUteis
                ? new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                : Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
        }

        /// <summary>True se "agora" está dentro do horário (e dias) de atuação.</summary>
        public bool DentroDoHorario(DateTime agora)
        {
            if (!DiasEfetivos().Contains(agora.DayOfWeek)) return false;

            if (!TimeSpan.TryParse(HoraInicio, out var inicio)) inicio = TimeSpan.FromHours(9);
            if (!TimeSpan.TryParse(HoraFim, out var fim)) fim = TimeSpan.FromHours(18);

            var hora = agora.TimeOfDay;
            return hora >= inicio && hora <= fim;
        }
    }
}
