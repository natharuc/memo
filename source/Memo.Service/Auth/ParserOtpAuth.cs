using System;
using System.Collections.Generic;
using System.Globalization;

namespace Memo.Service.Auth
{
    /// <summary>Parse de linhas <c>otpauth://totp/...</c> do export da Ente CLI.</summary>
    public static class ParserOtpAuth
    {
        public static List<EntradaAuth> ParsearExport(string texto)
        {
            var lista = new List<EntradaAuth>();
            if (string.IsNullOrWhiteSpace(texto)) return lista;

            foreach (var bruta in texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var pedaco in DividirLinha(bruta))
                {
                    if (Tentar(pedaco, out var entrada))
                        lista.Add(entrada);
                }
            }
            return lista;
        }

        public static bool Tentar(string uri, out EntradaAuth entrada)
        {
            entrada = null;
            if (string.IsNullOrWhiteSpace(uri)) return false;

            var s = uri.Trim().Trim('"');
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return false;
            if (!u.Scheme.Equals("otpauth", StringComparison.OrdinalIgnoreCase)) return false;
            if (!u.Host.Equals("totp", StringComparison.OrdinalIgnoreCase)) return false;

            var query = ParsearQuery(u.Query);
            if (!query.TryGetValue("secret", out var secret) || string.IsNullOrWhiteSpace(secret))
                return false;

            secret = secret.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

            var label = Uri.UnescapeDataString((u.AbsolutePath ?? string.Empty).TrimStart('/'));
            string issuer = null;
            string account = label;

            if (query.TryGetValue("issuer", out var issuerQ) && !string.IsNullOrWhiteSpace(issuerQ))
                issuer = issuerQ.Trim();

            var doisPontos = label.IndexOf(':');
            if (doisPontos > 0)
            {
                var esquerda = label.Substring(0, doisPontos).Trim();
                var direita = label.Substring(doisPontos + 1).Trim();
                if (string.IsNullOrEmpty(issuer)) issuer = esquerda;
                if (!string.IsNullOrEmpty(direita)) account = direita;
            }

            if (query.TryGetValue("steam", out _)) return false;

            var periodo = 30;
            if (query.TryGetValue("period", out var p) && int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv) && pv > 0)
                periodo = pv;

            var digitos = 6;
            if (query.TryGetValue("digits", out var d) && int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dv) && dv > 0)
                digitos = dv;

            var algo = "SHA1";
            if (query.TryGetValue("algorithm", out var a) && !string.IsNullOrWhiteSpace(a))
                algo = a.Trim().ToUpperInvariant();

            entrada = new EntradaAuth
            {
                Issuer = issuer ?? string.Empty,
                Account = account ?? string.Empty,
                Label = label,
                Secret = secret,
                Periodo = periodo,
                Digitos = digitos,
                Algoritmo = algo
            };
            return true;
        }

        private static IEnumerable<string> DividirLinha(string linha)
        {
            var t = (linha ?? string.Empty).Trim();
            if (t.Length == 0) yield break;

            if (t.IndexOf("otpauth://", StringComparison.OrdinalIgnoreCase) < 0)
            {
                yield return t;
                yield break;
            }

            if (t.IndexOf(',') < 0)
            {
                yield return t;
                yield break;
            }

            foreach (var pedaco in t.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = pedaco.Trim();
                if (p.Length > 0) yield return p;
            }
        }

        private static Dictionary<string, string> ParsearQuery(string query)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return d;

            var q = query[0] == '?' ? query.Substring(1) : query;
            foreach (var part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                string chave, valor;
                if (eq < 0) { chave = part; valor = string.Empty; }
                else
                {
                    chave = part.Substring(0, eq);
                    valor = part.Substring(eq + 1);
                }
                chave = Uri.UnescapeDataString(chave.Replace('+', ' '));
                valor = Uri.UnescapeDataString(valor.Replace('+', ' '));
                d[chave] = valor;
            }
            return d;
        }
    }
}
