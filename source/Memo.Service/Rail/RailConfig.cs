using System;
using System.Collections.Generic;

namespace Memo.Service.Rail
{
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

        /// <summary>Minutos contínuos em distração antes do aviso de desvio.</summary>
        public int DesvioMinutos { get; set; } = 5;

        /// <summary>Silêncio mínimo entre aparições do cerebrinho, em minutos.</summary>
        public int CooldownMinutos { get; set; } = 10;

        /// <summary>Janela de atuação (formato HH:mm).</summary>
        public string HoraInicio { get; set; } = "09:00";
        public string HoraFim { get; set; } = "18:00";

        /// <summary>Só age de segunda a sexta.</summary>
        public bool SomenteDiasUteis { get; set; } = true;

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

        /// <summary>True se "agora" está dentro do horário (e dias) de atuação.</summary>
        public bool DentroDoHorario(DateTime agora)
        {
            if (SomenteDiasUteis &&
                (agora.DayOfWeek == DayOfWeek.Saturday || agora.DayOfWeek == DayOfWeek.Sunday))
                return false;

            if (!TimeSpan.TryParse(HoraInicio, out var inicio)) inicio = TimeSpan.FromHours(9);
            if (!TimeSpan.TryParse(HoraFim, out var fim)) fim = TimeSpan.FromHours(18);

            var hora = agora.TimeOfDay;
            return hora >= inicio && hora <= fim;
        }
    }
}
