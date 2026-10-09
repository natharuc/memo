using System;
using System.Text;
using OtpNet;

namespace Memo.Service.Auth
{
    /// <summary>Gera o código TOTP atual a partir do secret Base32 (Otp.NET).</summary>
    public static class GeradorTotp
    {
        public static bool Tentar(EntradaAuth entrada, out string codigo, out int segundosRestantes) =>
            Tentar(entrada, DateTime.UtcNow, out codigo, out segundosRestantes);

        /// <summary>Código do período seguinte (o "próximo" da tela).</summary>
        public static bool Proximo(EntradaAuth entrada, out string codigo)
        {
            var step = entrada != null && entrada.Periodo > 0 ? entrada.Periodo : 30;
            return Tentar(entrada, DateTime.UtcNow.AddSeconds(step), out codigo, out _);
        }

        public static bool Tentar(EntradaAuth entrada, DateTime utc, out string codigo, out int segundosRestantes)
        {
            codigo = null;
            segundosRestantes = 0;
            if (entrada == null || string.IsNullOrWhiteSpace(entrada.Secret)) return false;

            byte[] chave;
            try { chave = Base32Encoding.ToBytes(entrada.Secret); }
            catch { return false; }

            return Tentar(chave, utc, entrada.Periodo, entrada.Digitos, entrada.Algoritmo,
                out codigo, out segundosRestantes);
        }

        /// <summary>Vetor conhecido (RFC 6238) — chave bruta, não Base32.</summary>
        public static bool Tentar(byte[] chave, DateTime utc, int periodo, int digitos, string algoritmo,
            out string codigo, out int segundosRestantes)
        {
            codigo = null;
            segundosRestantes = 0;
            if (chave == null || chave.Length == 0) return false;

            var step = periodo > 0 ? periodo : 30;
            var size = digitos > 0 ? digitos : 6;
            var modo = Modo(algoritmo);

            try
            {
                var totp = new Totp(chave, step: step, mode: modo, totpSize: size);
                var quando = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
                codigo = totp.ComputeTotp(quando);
                segundosRestantes = SegundosRestantes(quando, step);
                return !string.IsNullOrEmpty(codigo);
            }
            catch
            {
                return false;
            }
        }

        public static byte[] ChaveAscii(string texto) => Encoding.ASCII.GetBytes(texto ?? string.Empty);

        internal static int SegundosRestantes(DateTime utc, int periodo)
        {
            var step = periodo > 0 ? periodo : 30;
            var quando = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
            var unix = new DateTimeOffset(quando).ToUnixTimeSeconds();
            var rem = (int)(step - (unix % step));
            return rem == 0 ? step : rem;
        }

        private static OtpHashMode Modo(string algoritmo)
        {
            switch ((algoritmo ?? "SHA1").Trim().ToUpperInvariant())
            {
                case "SHA256": return OtpHashMode.Sha256;
                case "SHA512": return OtpHashMode.Sha512;
                default: return OtpHashMode.Sha1;
            }
        }
    }
}
