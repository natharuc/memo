using System;
using System.Linq;

namespace Memo.Service.Auth
{
    /// <summary>Valida a edição e preserva metadados desconhecidos do URI do Ente.</summary>
    public static class EdicaoAuth
    {
        public static EntradaAuth Copiar(EntradaAuth entrada) => new EntradaAuth
        {
            IdEntidade = entrada.IdEntidade,
            Issuer = entrada.Issuer,
            Account = entrada.Account,
            Label = entrada.Label,
            Secret = entrada.Secret,
            Periodo = entrada.Periodo,
            Digitos = entrada.Digitos,
            Algoritmo = entrada.Algoritmo
        };

        public static bool MesmaEntrada(EntradaAuth a, EntradaAuth b) => a != null && b != null
            && string.Equals(a.Issuer ?? "", b.Issuer ?? "", StringComparison.Ordinal)
            && string.Equals(a.Account ?? "", b.Account ?? "", StringComparison.Ordinal)
            && string.Equals(a.Label ?? "", b.Label ?? "", StringComparison.Ordinal)
            && string.Equals(NormalizarSecret(a.Secret), NormalizarSecret(b.Secret), StringComparison.Ordinal)
            && a.Periodo == b.Periodo && a.Digitos == b.Digitos
            && string.Equals(a.Algoritmo, b.Algoritmo, StringComparison.OrdinalIgnoreCase);

        private static string NormalizarSecret(string secret) =>
            new string((secret ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

        public static string MontarUri(string original, EntradaAuth nova)
        {
            if (!ParserOtpAuth.Tentar(original, out _))
                throw new InvalidOperationException("A chave original não é um TOTP válido.");
            if (nova == null || (string.IsNullOrWhiteSpace(nova.Issuer) && string.IsNullOrWhiteSpace(nova.Account)))
                throw new InvalidOperationException("Informe o serviço ou a conta da chave.");
            var secret = NormalizarSecret(nova.Secret);
            if (secret.Length == 0 || secret.TrimEnd('=').Any(c => !(c >= 'A' && c <= 'Z') && !(c >= '2' && c <= '7')))
                throw new InvalidOperationException("A chave secreta precisa estar em Base32.");
            if (nova.Periodo < 1 || nova.Periodo > 86400)
                throw new InvalidOperationException("O período precisa estar entre 1 e 86400 segundos.");
            if (nova.Digitos < 6 || nova.Digitos > 8)
                throw new InvalidOperationException("Escolha de 6 a 8 dígitos.");
            var algoritmo = (nova.Algoritmo ?? "").ToUpperInvariant();
            if (algoritmo != "SHA1" && algoritmo != "SHA256" && algoritmo != "SHA512")
                throw new InvalidOperationException("Escolha SHA1, SHA256 ou SHA512.");
            var validada = Copiar(nova);
            validada.Secret = secret;
            if (!GeradorTotp.Tentar(validada, out _, out _))
                throw new InvalidOperationException("A chave secreta não gera um TOTP válido.");

            var uri = new Uri(original);
            var substituidos = new[] { "issuer", "secret", "algorithm", "digits", "period" };
            var extras = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !substituidos.Contains(Uri.UnescapeDataString(p.Split('=')[0].Replace('+', ' ')),
                    StringComparer.OrdinalIgnoreCase)).ToList();
            var issuer = nova.Issuer?.Trim() ?? "";
            var conta = nova.Account?.Trim() ?? "";
            var rotulo = issuer.Length == 0 ? Uri.EscapeDataString(conta)
                : Uri.EscapeDataString(issuer) + (conta.Length == 0 ? "" : ":" + Uri.EscapeDataString(conta));
            extras.Add("issuer=" + Uri.EscapeDataString(issuer));
            extras.Add("secret=" + Uri.EscapeDataString(secret));
            extras.Add("algorithm=" + algoritmo);
            extras.Add("digits=" + nova.Digitos);
            extras.Add("period=" + nova.Periodo);
            return "otpauth://totp/" + rotulo + "?" + string.Join("&", extras);
        }
    }
}
