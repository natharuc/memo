using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Memo.Service.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Memo.Service.Tests
{
    public class EnteAuthApiTests
    {
        // Vetor público do secretbox usado em CriptoEnteTests; nunca lê uma sessão real.
        private const string CaixaChave = "KHwRN+RzvTu+jC7mCdkMsqnTPSLvevtZILmcR2OYFbIRPqDyjAl+m8KxD9B5fiEo";
        private const string NonceChave = "jgfPDOsQh2VdIHWJVSBicMPF2sQW3HIY";
        private const string ChaveAuth = "O1ObUBMv+SCE1qWHD7+WViEIZcAeTp18Y+m9eMlDE1Y=";
        private const string UriTeste = "otpauth://totp/Exemplo:conta?secret=JBSWY3DPEHPK3PXP&issuer=Exemplo&codeDisplay=%7B%22note%22%3A%22nota%22%7D";
        private const string IdUm = "11111111-1111-1111-1111-111111111111";
        private const string IdDois = "22222222-2222-2222-2222-222222222222";

        [Fact]
        public void Listar_preserva_id_e_uri_completa_mesmo_com_segredos_iguais()
        {
            using var transporte = new TransporteSimulado(req =>
            {
                if (EhChave(req)) return RespostaChave();
                Assert.Equal(HttpMethod.Get, req.Method);
                Assert.Equal("/authenticator/entity/diff?sinceTime=0&limit=500", req.RequestUri.PathAndQuery);
                return Json(new JObject { ["diff"] = new JArray(Entidade(IdUm, UriTeste, 10), Entidade(IdDois, UriTeste, 11)) });
            });
            using var http = new HttpClient(transporte);
            var lista = new EnteAuthApi(http).Listar(SessaoTeste());

            Assert.Equal(2, lista.Count);
            Assert.Equal(IdUm, lista[0].Id);
            Assert.Equal(IdDois, lista[1].Id);
            Assert.Equal(UriTeste, lista[0].UriOtpauth);
            Assert.Equal(10, lista[0].AtualizadoEm);
        }

        [Fact]
        public void Listar_ignora_tombstones_sem_tentar_decifrar()
        {
            using var transporte = new TransporteSimulado(req => EhChave(req) ? RespostaChave() : Json(new JObject
            {
                ["diff"] = new JArray(new JObject
                {
                    ["id"] = IdUm, ["isDeleted"] = true, ["updatedAt"] = 10,
                    ["encryptedData"] = null, ["header"] = null
                })
            }));
            using var http = new HttpClient(transporte);
            Assert.Empty(new EnteAuthApi(http).Listar(SessaoTeste()));
        }

        [Fact]
        public void Listar_pagina_sem_perder_timestamp_de_fronteira()
        {
            var chamadasDiff = 0;
            var molde = Entidade(IdUm, UriTeste, 1);
            JObject ComIndice(int i)
            {
                var copia = (JObject)molde.DeepClone();
                copia["id"] = new Guid(i, 0, 0, new byte[8]).ToString();
                copia["updatedAt"] = 1000 + i;
                return copia;
            }
            using var transporte = new TransporteSimulado(req =>
            {
                if (EhChave(req)) return RespostaChave();
                chamadasDiff++;
                if (chamadasDiff == 1)
                    return Json(new JObject { ["diff"] = new JArray(Enumerable.Range(1, 500).Select(ComIndice)) });
                Assert.Contains("sinceTime=1499", req.RequestUri.Query);
                return Json(new JObject { ["diff"] = new JArray(ComIndice(500), ComIndice(501)) });
            });
            using var http = new HttpClient(transporte);
            Assert.Equal(501, new EnteAuthApi(http).Listar(SessaoTeste()).Count);
            Assert.Equal(2, chamadasDiff);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"diff\":null}")]
        [InlineData("não-json-com-conteúdo-sensível")]
        public void Listar_rejeita_resposta_incompleta_sem_retornar_lista_parcial(string json)
        {
            using var transporte = new TransporteSimulado(req => EhChave(req) ? RespostaChave() : JsonCru(json));
            using var http = new HttpClient(transporte);
            var erro = Assert.Throws<InvalidOperationException>(() => new EnteAuthApi(http).Listar(SessaoTeste()));
            Assert.DoesNotContain("conteúdo-sensível", erro.Message);
        }

        [Fact]
        public void Atualizar_usa_put_mesmo_id_e_payload_cifrado_com_metadados()
        {
            using var transporte = new TransporteSimulado(req =>
            {
                if (EhChave(req)) return RespostaChave();
                Assert.Equal(HttpMethod.Put, req.Method);
                Assert.Equal("/authenticator/entity", req.RequestUri.AbsolutePath);
                var texto = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.DoesNotContain("JBSWY3DPEHPK3PXP", texto);
                var corpo = JObject.Parse(texto);
                Assert.Equal(IdUm, (string)corpo["id"]);
                var plano = CriptoEnte.DecifrarFluxo(
                    Convert.FromBase64String((string)corpo["encryptedData"]), Convert.FromBase64String(ChaveAuth),
                    Convert.FromBase64String((string)corpo["header"]));
                Assert.Equal(UriTeste, JsonConvert.DeserializeObject<string>(Encoding.UTF8.GetString(plano)));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            using var http = new HttpClient(transporte);
            new EnteAuthApi(http).Atualizar(SessaoTeste(), IdUm, UriTeste);
            Assert.Equal(2, transporte.Chamadas);
        }

        [Fact]
        public void Remover_usa_delete_por_id_sem_buscar_ou_enviar_segredo()
        {
            using var transporte = new TransporteSimulado(req =>
            {
                Assert.Equal(HttpMethod.Delete, req.Method);
                Assert.Equal("/authenticator/entity?id=" + IdUm, req.RequestUri.PathAndQuery);
                Assert.Null(req.Content);
                Assert.Equal("io.ente.auth", req.Headers.GetValues("X-Client-Package").Single());
                Assert.Single(req.Headers.GetValues("X-Auth-Token"));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            using var http = new HttpClient(transporte);
            new EnteAuthApi(http).Remover(SessaoTeste(), IdUm);
            Assert.Equal(1, transporte.Chamadas);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("../../outra-chave")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void Remover_rejeita_id_invalido_antes_de_chamar_rede(string id)
        {
            using var transporte = new TransporteSimulado(_ => throw new Exception("Não deveria chamar a rede."));
            using var http = new HttpClient(transporte);
            Assert.Throws<InvalidOperationException>(() => new EnteAuthApi(http).Remover(SessaoTeste(), id));
            Assert.Equal(0, transporte.Chamadas);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public void Erros_http_nao_expoem_corpo_da_resposta(HttpStatusCode status)
        {
            using var transporte = new TransporteSimulado(_ => new HttpResponseMessage(status)
            { Content = new StringContent("segredo-da-resposta") });
            using var http = new HttpClient(transporte);
            var erro = Assert.Throws<InvalidOperationException>(() => new EnteAuthApi(http).Remover(SessaoTeste(), IdUm));
            Assert.DoesNotContain("segredo-da-resposta", erro.ToString());
        }

        private static bool EhChave(HttpRequestMessage req) => req.RequestUri.AbsolutePath == "/authenticator/key";

        private static HttpResponseMessage RespostaChave() => Json(new JObject
        { ["userID"] = 42, ["encryptedKey"] = CaixaChave, ["header"] = NonceChave });

        private static JObject Entidade(string id, string uri, long atualizado)
        {
            CriptoEnte.CifrarFluxo(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(uri)),
                Convert.FromBase64String(ChaveAuth), out var cifra, out var cabecalho);
            return new JObject
            {
                ["id"] = id, ["userID"] = 42, ["updatedAt"] = atualizado, ["isDeleted"] = false,
                ["encryptedData"] = Convert.ToBase64String(cifra), ["header"] = Convert.ToBase64String(cabecalho)
            };
        }

        private static HttpResponseMessage Json(JObject valor) => JsonCru(valor.ToString(Formatting.None));
        private static HttpResponseMessage JsonCru(string json) => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private static SessaoEnteCli SessaoTeste()
        {
            var sessao = new SessaoEnteCli();
            void Definir(string propriedade, object valor) => typeof(SessaoEnteCli).GetProperty(propriedade).SetValue(sessao, valor);
            Definir("UserId", 42L);
            Definir("Token", new byte[] { 1, 2, 3 });
            Definir("MasterKey", Convert.FromBase64String("kercNpvGufMTTHmDwAhz26DgCAvznd1+/buBqKEkWr4="));
            Definir("ApiBase", "https://ente.invalid");
            return sessao;
        }

        private sealed class TransporteSimulado : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public int Chamadas { get; private set; }
            public TransporteSimulado(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Chamadas++;
                return _responder(request);
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(Send(request, cancellationToken));
        }
    }
}
