using System;
using System.Collections.Generic;
using System.Text;
using Memo.Service.Auth;
using Xunit;

namespace Memo.Service.Tests
{
    public class AuthTests
    {
        private const string UriVercelNathan =
            "otpauth://totp/Vercel:nathanarrudacamara@gmail.com?secret=JBSWY3DPEHPK3PXP&issuer=Vercel&algorithm=SHA1&digits=6&period=30";

        private const string UriVercelOutro =
            "otpauth://totp/Vercel:outro@example.com?secret=HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ&issuer=Vercel";

        private const string UriGithub =
            "otpauth://totp/GitHub:nathanarrudacamara@gmail.com?secret=ONSWG4TFOQ======&issuer=GitHub";

        [Fact]
        public void Parsear_otpauth_totp_preenche_issuer_account_e_secret()
        {
            Assert.True(ParserOtpAuth.Tentar(UriVercelNathan, out var e));
            Assert.Equal("Vercel", e.Issuer);
            Assert.Equal("nathanarrudacamara@gmail.com", e.Account);
            Assert.Equal("JBSWY3DPEHPK3PXP", e.Secret);
            Assert.Equal(30, e.Periodo);
            Assert.Equal(6, e.Digitos);
            Assert.Equal("SHA1", e.Algoritmo);
        }

        [Fact]
        public void Parsear_ignora_hotp()
        {
            Assert.False(ParserOtpAuth.Tentar(
                "otpauth://hotp/Vercel:nathanarrudacamara@gmail.com?secret=JBSWY3DPEHPK3PXP&counter=1",
                out _));
        }

        [Fact]
        public void Correspondencia_and_vercel_e_email()
        {
            var entradas = ParserOtpAuth.ParsearExport(string.Join("\n", UriVercelNathan, UriVercelOutro, UriGithub));
            var hits = CorrespondenciaAuth.Filtrar(entradas, new[] { "vercel", "nathanarrudacamara@gmail.com" });
            Assert.Single(hits);
            Assert.Equal("Vercel", hits[0].Issuer);
            Assert.Equal("nathanarrudacamara@gmail.com", hits[0].Account);
        }

        [Fact]
        public void Correspondencia_vercel_sozinho_e_ambiguo()
        {
            var entradas = ParserOtpAuth.ParsearExport(string.Join("\n", UriVercelNathan, UriVercelOutro, UriGithub));
            var hits = CorrespondenciaAuth.Filtrar(entradas, new[] { "vercel" });
            Assert.Equal(2, hits.Count);
        }

        [Fact]
        public void Totp_rfc6238_sha1_8_digitos_em_t59()
        {
            var chave = Encoding.ASCII.GetBytes("12345678901234567890");
            var utc = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;
            Assert.True(GeradorTotp.Tentar(chave, utc, 30, 8, "SHA1", out var codigo, out _));
            Assert.Equal("94287082", codigo);
        }

        [Fact]
        public void ContaAuth_parseia_account_list_da_cli()
        {
            var texto = @"Configured accounts: 1
====================================
Email:  nathanarrudacamara@gmail.com
ID:  42
App:  auth
ExportDir: C:\tmp\ente
====================================
";
            var contas = ContaAuth.Parsear(texto);
            Assert.Single(contas);
            Assert.True(contas[0].EhAuth);
            Assert.Equal("nathanarrudacamara@gmail.com", contas[0].Email);
            Assert.Equal(@"C:\tmp\ente", contas[0].ExportDir);
        }

        [Fact]
        public void Buscar_sem_tokens_e_uso()
        {
            var r = new EnteAuthService().Buscar(new List<string>(), forcarSync: false);
            Assert.False(r.Sucesso);
            Assert.True(r.UsoIncorreto);
        }
    }
}
