using System;
using System.Text.RegularExpressions;

namespace Memo.Service.Lembretes
{
    public class LembreteParseado
    {
        public bool Ok { get; set; }
        public string Erro { get; set; }
        public string Texto { get; set; }
        public DateTime Proximo { get; set; }
        public int RepetirMinutos { get; set; }
    }

    /// <summary>
    /// Interpreta uma frase de lembrete em linguagem natural (PT/EN). Exemplos:
    ///   "ver tarefa 477987 10:00 tomorrow"  -> amanhã 10:00
    ///   "ver tarefa 477987 10:00"           -> hoje 10:00 (ou amanhã, se já passou)
    ///   "ver tarefa 477987 22h"             -> hoje 22:00
    ///   "beber agua every 30 minutes"       -> a cada 30 min
    ///   "ligar joão in 15 minutes"          -> daqui a 15 min
    /// </summary>
    public static class ParserLembrete
    {
        public static LembreteParseado Analisar(string entrada, DateTime agora)
        {
            var r = new LembreteParseado();
            if (string.IsNullOrWhiteSpace(entrada))
            {
                r.Erro = "Diga o que lembrar e quando.";
                return r;
            }

            var s = entrada.Trim();

            // 1) Recorrência: "every 30 minutes", "a cada 2 horas"
            var rec = Regex.Match(s, @"\b(?:every|a\s+cada|cada)\s+(\d+)\s*(min\w*|h\w*)\b", RegexOptions.IgnoreCase);
            if (rec.Success)
            {
                var minutos = ParaMinutos(rec.Groups[1].Value, rec.Groups[2].Value);
                if (minutos < 1) { r.Erro = "Intervalo inválido."; return r; }

                var texto = Limpar(s.Remove(rec.Index, rec.Length));
                if (string.IsNullOrEmpty(texto)) { r.Erro = "Diga do que lembrar."; return r; }

                r.Ok = true;
                r.Texto = texto;
                r.RepetirMinutos = minutos;
                r.Proximo = agora.AddMinutes(minutos);
                return r;
            }

            // 2) Relativo: "in 10 minutes", "daqui 15 min", "em 2 horas"
            var rel = Regex.Match(s, @"\b(?:in|daqui(?:\s+a)?|em)\s+(\d+)\s*(min\w*|h\w*)\b", RegexOptions.IgnoreCase);
            if (rel.Success)
            {
                var minutos = ParaMinutos(rel.Groups[1].Value, rel.Groups[2].Value);
                if (minutos < 1) { r.Erro = "Intervalo inválido."; return r; }

                var texto = Limpar(s.Remove(rel.Index, rel.Length));
                if (string.IsNullOrEmpty(texto)) { r.Erro = "Diga do que lembrar."; return r; }

                r.Ok = true;
                r.Texto = texto;
                r.Proximo = agora.AddMinutes(minutos);
                return r;
            }

            // 3) Dia (opcional) + hora: "10:00 tomorrow", "22h", "amanhã 9:30"
            var diaOffset = 0;
            var diaExplicito = false;
            var dia = Regex.Match(s, @"\b(tomorrow|amanh[ãa]|hoje|today)\b", RegexOptions.IgnoreCase);
            if (dia.Success)
            {
                var d = dia.Groups[1].Value.ToLowerInvariant();
                diaOffset = (d == "tomorrow" || d.StartsWith("amanh")) ? 1 : 0;
                diaExplicito = true;
                s = s.Remove(dia.Index, dia.Length);
            }

            var hm = Regex.Match(s, @"\b(\d{1,2})(?::(\d{2})|h(\d{2})?)\b", RegexOptions.IgnoreCase);
            if (!hm.Success)
            {
                r.Erro = "Não entendi o horário. Use algo como 10:00, 22h, \"amanhã 9:30\" ou \"every 30 minutes\".";
                return r;
            }

            var hora = int.Parse(hm.Groups[1].Value);
            var minuto = hm.Groups[2].Success ? int.Parse(hm.Groups[2].Value)
                       : hm.Groups[3].Success ? int.Parse(hm.Groups[3].Value)
                       : 0;
            if (hora > 23 || minuto > 59) { r.Erro = "Horário inválido."; return r; }
            s = s.Remove(hm.Index, hm.Length);

            var textoFinal = Limpar(s);
            if (string.IsNullOrEmpty(textoFinal)) { r.Erro = "Diga do que lembrar."; return r; }

            var quando = agora.Date.AddDays(diaOffset).AddHours(hora).AddMinutes(minuto);
            // Sem dia explícito e horário já passou hoje → joga para amanhã.
            if (!diaExplicito && quando <= agora) quando = quando.AddDays(1);

            r.Ok = true;
            r.Texto = textoFinal;
            r.Proximo = quando;
            return r;
        }

        private static int ParaMinutos(string numero, string unidade)
        {
            var n = int.Parse(numero);
            var horas = unidade.StartsWith("h", StringComparison.OrdinalIgnoreCase);
            return horas ? n * 60 : n;
        }

        private static string Limpar(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();
    }
}
