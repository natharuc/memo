using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Cria uma entidade no Ente Auth (<c>POST /authenticator/entity</c>).
    /// O segredo sai cifrado; a resposta de erro não é logada.
    /// </summary>
    public sealed class EnteAuthApi
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public void Enviar(SessaoEnteCli sessao, string uriOtpauth)
        {
            if (sessao == null) throw new ArgumentNullException(nameof(sessao));
            if (string.IsNullOrWhiteSpace(uriOtpauth))
                throw new InvalidOperationException("URI otpauth vazia.");

            var authKey = ObterAuthKey(sessao);
            try
            {
                var json = JsonConvert.SerializeObject(uriOtpauth.Trim());
                CriptoEnte.CifrarFluxo(Encoding.UTF8.GetBytes(json), authKey, out var dados, out var cabecalho);
                Postar(sessao, Convert.ToBase64String(dados), Convert.ToBase64String(cabecalho));
            }
            finally
            {
                Array.Clear(authKey, 0, authKey.Length);
            }
        }

        private static byte[] ObterAuthKey(SessaoEnteCli sessao)
        {
            var json = Pedir(sessao, HttpMethod.Get, "/authenticator/key", null);
            var obj = JObject.Parse(json);
            var userId = obj.Value<long?>("userID") ?? 0;
            if (userId != 0 && userId != sessao.UserId)
                throw new InvalidOperationException("A chave do Auth não é desta conta.");

            var enc = (string)obj["encryptedKey"];
            var header = (string)obj["header"];
            if (string.IsNullOrEmpty(enc) || string.IsNullOrEmpty(header))
                throw new InvalidOperationException("O Ente não devolveu a chave do Auth.");

            var chave = CriptoEnte.AbrirCaixa(
                Convert.FromBase64String(enc),
                Convert.FromBase64String(header),
                sessao.MasterKey);
            if (chave.Length != 32)
                throw new InvalidOperationException("Chave do Auth com tamanho inesperado.");
            return chave;
        }

        private static void Postar(SessaoEnteCli sessao, string dados, string cabecalho)
        {
            var corpo = JsonConvert.SerializeObject(new { encryptedData = dados, header = cabecalho });
            Pedir(sessao, HttpMethod.Post, "/authenticator/entity", corpo);
        }

        private static string Pedir(SessaoEnteCli sessao, HttpMethod metodo, string caminho, string corpo)
        {
            var url = sessao.ApiBase.TrimEnd('/') + caminho;
            using (var req = new HttpRequestMessage(metodo, url))
            {
                req.Headers.TryAddWithoutValidation("X-Auth-Token", sessao.TokenHttp);
                req.Headers.TryAddWithoutValidation("X-Client-Package", "io.ente.auth");
                if (corpo != null)
                    req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");

                HttpResponseMessage resp;
                try
                {
                    resp = Http.Send(req, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Não consegui falar com o Ente: " + ex.Message);
                }

                using (resp)
                {
                    var texto = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if ((int)resp.StatusCode == 401)
                        throw new InvalidOperationException(
                            "A sessão da Ente CLI expirou. Em Configurações → Ente, conecte a conta de novo.");
                    if ((int)resp.StatusCode == 404 && caminho.EndsWith("/key", StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Essa conta ainda não tem chave no Ente Auth. Abra o app uma vez e tente de novo.");
                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            "O Ente recusou o cadastro (HTTP " + (int)resp.StatusCode + ").");
                    return texto ?? string.Empty;
                }
            }
        }
    }
}
