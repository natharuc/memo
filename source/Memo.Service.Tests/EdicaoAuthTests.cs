using System;
using Memo.Service.Auth;
using Xunit;

namespace Memo.Service.Tests
{
    public class EdicaoAuthTests
    {
        private const string Original = "otpauth://totp/Servico:conta?secret=JBSWY3DPEHPK3PXP&issuer=Servico&codeDisplay=pinned&custom=1%2B2&issuer=Servico";

        [Fact]
        public void Edicao_troca_segredo_e_parametros_sem_perder_metadados_extras()
        {
            var nova = Entrada();
            nova.Secret = "hxdm vjec-jjwsrb3hwizr4ifugftmxboz";
            nova.Algoritmo = "sha512";
            nova.Periodo = 45;
            nova.Digitos = 7;
            nova.Issuer = "Serviço & novo";
            nova.Account = "nome+conta@example.com";

            var uri = EdicaoAuth.MontarUri(Original, nova);

            Assert.True(ParserOtpAuth.Tentar(uri, out var volta));
            Assert.Equal("HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ", volta.Secret);
            Assert.Equal("SHA512", volta.Algoritmo);
            Assert.Equal(45, volta.Periodo);
            Assert.Equal(7, volta.Digitos);
            Assert.Equal("Serviço & novo", volta.Issuer);
            Assert.Equal("nome+conta@example.com", volta.Account);
            Assert.Contains("codeDisplay=pinned", uri);
            Assert.Contains("custom=1%2B2", uri);
            Assert.True(GeradorTotp.Tentar(volta, out _, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("!invalido!")]
        [InlineData("ABC019")]
        [InlineData("====")]
        public void Segredo_invalido_e_rejeitado(string secret)
        {
            var nova = Entrada();
            nova.Secret = secret;

            Assert.Throws<InvalidOperationException>(() => EdicaoAuth.MontarUri(Original, nova));
        }

        [Theory]
        [InlineData(0, 6, "SHA1")]
        [InlineData(86401, 6, "SHA1")]
        [InlineData(30, 5, "SHA1")]
        [InlineData(30, 9, "SHA1")]
        [InlineData(30, 6, "MD5")]
        [InlineData(30, 6, null)]
        public void Parametros_invalidos_sao_rejeitados(int periodo, int digitos, string algoritmo)
        {
            var nova = Entrada();
            nova.Periodo = periodo;
            nova.Digitos = digitos;
            nova.Algoritmo = algoritmo;

            Assert.Throws<InvalidOperationException>(() => EdicaoAuth.MontarUri(Original, nova));
        }

        [Fact]
        public void Servico_e_conta_vazios_sao_rejeitados()
        {
            var nova = Entrada();
            nova.Issuer = " ";
            nova.Account = null;

            Assert.Throws<InvalidOperationException>(() => EdicaoAuth.MontarUri(Original, nova));
        }

        [Fact]
        public void Uri_original_invalido_e_rejeitado()
        {
            Assert.Throws<InvalidOperationException>(() => EdicaoAuth.MontarUri("https://example.com", Entrada()));
        }

        [Fact]
        public void Comparacao_distingue_contas_com_o_mesmo_segredo()
        {
            var a = Entrada();
            var b = EdicaoAuth.Copiar(a);
            b.Account = "outra conta";

            Assert.False(EdicaoAuth.MesmaEntrada(a, b));
        }

        [Fact]
        public void Comparacao_normaliza_o_segredo_e_preserva_os_outros_campos()
        {
            var a = Entrada();
            var b = EdicaoAuth.Copiar(a);
            b.Secret = "jbsw y3dp-ehpk3pxp";
            b.Algoritmo = "sha1";

            Assert.True(EdicaoAuth.MesmaEntrada(a, b));

            b.Periodo = 60;
            Assert.False(EdicaoAuth.MesmaEntrada(a, b));
        }

        [Theory]
        [InlineData("Corp:IT", "alice")]
        [InlineData("Corp:IT", "equipe:joão+tag/conta@example.com")]
        [InlineData("Corp:IT", "")]
        [InlineData("Serviço", "")]
        [InlineData("", "conta@example.com")]
        [InlineData("", "equipe:joão+tag/conta@example.com")]
        public void Rotulos_com_separadores_unicode_ou_um_campo_vazio_sobrevivem_ao_roundtrip(string issuer, string conta)
        {
            var nova = Entrada();
            nova.Issuer = issuer;
            nova.Account = conta;

            var uri = EdicaoAuth.MontarUri(Original, nova);

            Assert.True(ParserOtpAuth.Tentar(uri, out var volta));
            Assert.Equal(issuer, volta.Issuer);
            Assert.Equal(conta, volta.Account);
            Assert.Equal(nova.Secret, volta.Secret);
        }

        [Fact]
        public void Parser_remove_o_prefixo_completo_de_um_servico_com_dois_pontos()
        {
            var uri = "otpauth://totp/Corp:IT:alice?secret=JBSWY3DPEHPK3PXP&issuer=Corp%3AIT";

            Assert.True(ParserOtpAuth.Tentar(uri, out var entrada));

            Assert.Equal("Corp:IT", entrada.Issuer);
            Assert.Equal("alice", entrada.Account);
        }

        [Fact]
        public void Renomeacoes_sucessivas_preservam_query_desconhecida_apos_parse()
        {
            var nova = Entrada();
            nova.Issuer = "Corp:IT";
            nova.Account = "joão+equipe/conta@example.com";
            var primeira = EdicaoAuth.MontarUri(Original, nova);
            Assert.True(ParserOtpAuth.Tentar(primeira, out var parseada));
            parseada.Account = "segunda:conta/á+tag@example.com";

            var segunda = EdicaoAuth.MontarUri(primeira, parseada);

            Assert.True(ParserOtpAuth.Tentar(segunda, out var volta));
            Assert.Equal("Corp:IT", volta.Issuer);
            Assert.Equal(parseada.Account, volta.Account);
            Assert.Contains("codeDisplay=pinned", segunda);
            Assert.Contains("custom=1%2B2", segunda);
            Assert.DoesNotContain("issuer=Servico", segunda);
            Assert.Equal(segunda, EdicaoAuth.MontarUri(segunda, volta));
        }

        private static EntradaAuth Entrada()
        {
            Assert.True(ParserOtpAuth.Tentar(Original, out var entrada));
            return entrada;
        }
    }
}
