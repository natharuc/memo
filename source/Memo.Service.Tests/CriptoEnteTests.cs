using System;
using System.IO;
using System.Text;
using Memo.Service.Auth;
using Xunit;

namespace Memo.Service.Tests
{
    public class CriptoEnteTests
    {
        [Fact]
        public void Secretstream_decifra_vetor_da_cli()
        {
            var chave = Convert.FromBase64String("vp8d8Nee0BbIML4ab8Cp34uYnyrN77cRwTl920flyT0=");
            var cifrado = Convert.FromBase64String("kBXQ2PuX6y/aje5r22H0AehRPh6sQ0ULoeAO");
            var nonce = Convert.FromBase64String("v7wsI+BFZsRMIjDm3rTxPhmi/CaUdkdJ");

            var claro = CriptoEnte.DecifrarFluxo(cifrado, chave, nonce);
            Assert.Equal("plain_text", Encoding.UTF8.GetString(claro));
        }

        [Fact]
        public void Secretbox_abre_vetor_da_cli()
        {
            var caixa = Convert.FromBase64String("KHwRN+RzvTu+jC7mCdkMsqnTPSLvevtZILmcR2OYFbIRPqDyjAl+m8KxD9B5fiEo");
            var nonce = Convert.FromBase64String("jgfPDOsQh2VdIHWJVSBicMPF2sQW3HIY");
            var chave = Convert.FromBase64String("kercNpvGufMTTHmDwAhz26DgCAvznd1+/buBqKEkWr4=");

            var claro = CriptoEnte.AbrirCaixa(caixa, nonce, chave);
            Assert.Equal("O1ObUBMv+SCE1qWHD7+WViEIZcAeTp18Y+m9eMlDE1Y=", Convert.ToBase64String(claro));
        }

        [Fact]
        public void Sessao_auth_abre_se_a_cli_estiver_no_pc()
        {
            var db = Path.Combine(SessaoEnteCli.PastaCli(), "ente-cli.db");
            if (!File.Exists(db)) return;

            var sessao = SessaoEnteCli.Carregar();
            try
            {
                Assert.Equal(32, sessao.MasterKey.Length);
                Assert.NotEmpty(sessao.Token);
                Assert.Contains("@", sessao.Email);
                Assert.StartsWith("http", sessao.ApiBase);
            }
            finally
            {
                sessao.Limpar();
            }
        }

        [Fact]
        public void Secretstream_roundtrip_de_otpauth_json()
        {
            var uri = "otpauth://totp/Vercel:nathanarrudacamara@gmail.com?secret=JBSWY3DPEHPK3PXP&issuer=Vercel";
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(uri);
            var chave = new byte[32];
            for (var i = 0; i < chave.Length; i++) chave[i] = (byte)(i + 1);

            CriptoEnte.CifrarFluxo(Encoding.UTF8.GetBytes(json), chave, out var cifrado, out var cabecalho);
            var volta = Encoding.UTF8.GetString(CriptoEnte.DecifrarFluxo(cifrado, chave, cabecalho));
            Assert.Equal(json, volta);
            Assert.Equal(uri, Newtonsoft.Json.JsonConvert.DeserializeObject<string>(volta));
        }
    }
}
