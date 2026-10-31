using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Memo.Service.Auth
{
    /// <summary>Transporte autenticado do Ente; a sessão é aberta e limpa pelo chamador.</summary>
    public interface IEnteAuthApi
    {
        List<EntidadeAuth> Listar(SessaoEnteCli sessao);
        void Enviar(SessaoEnteCli sessao, string uriOtpauth);
        void Atualizar(SessaoEnteCli sessao, string id, string uriOtpauth);
        void Remover(SessaoEnteCli sessao, string id);
    }

    /// <summary>Identidade remota e URI original completa (inclui metadados do Ente).</summary>
    public sealed class EntidadeAuth
    {
        public string Id { get; set; }
        public string UriOtpauth { get; set; }
        public long AtualizadoEm { get; set; }
    }

    /// <summary>
    /// CRUD de entidades no Ente Auth. Segredos são cifrados antes do envio;
    /// respostas de erro, tokens e plaintext nunca entram nas mensagens de falha.
    /// </summary>
    public sealed class EnteAuthApi : IEnteAuthApi
    {
        private const int LimitePagina = 500;
        private static readonly HttpClient HttpPadrao = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly HttpClient _http;

        public EnteAuthApi() : this(HttpPadrao) { }

        /// <summary>Permite transporte simulado em testes, sem acesso à conta real.</summary>
        public EnteAuthApi(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        /// <summary>
        /// Obtém o estado remoto completo, paginando o diff desde zero e aplicando
        /// tombstones por ID. Não usa o export, que perde a identidade de cada chave.
        /// </summary>
        public List<EntidadeAuth> Listar(SessaoEnteCli sessao)
        {
            ValidarSessao(sessao);
            var chave = ObterAuthKey(sessao);
            try
            {
                var entidades = new Dictionary<string, EntidadeAuth>(StringComparer.OrdinalIgnoreCase);
                long desde = 0;
                for (var pagina = 0; pagina < 10000; pagina++)
                {
                    var caminho = "/authenticator/entity/diff?sinceTime=" + desde.ToString(CultureInfo.InvariantCulture)
                        + "&limit=" + LimitePagina.ToString(CultureInfo.InvariantCulture);
                    var obj = LerObjeto(Pedir(sessao, HttpMethod.Get, caminho, null));
                    if (!(obj["diff"] is JArray itens) || itens.Count > LimitePagina)
                        throw new InvalidOperationException("O Ente devolveu uma página de chaves inválida.");

                    var maior = desde;
                    foreach (var item in itens)
                    {
                        var id = ValidarId((string)item["id"]);
                        var usuario = LerInteiro(item["userID"], sessao.UserId);
                        if (usuario != sessao.UserId)
                            throw new InvalidOperationException("A chave do Auth não é desta conta.");
                        var atualizado = LerInteiro(item["updatedAt"], 0);
                        if (atualizado <= desde)
                            throw new InvalidOperationException("O Ente devolveu uma paginação de chaves inválida.");
                        maior = Math.Max(maior, atualizado);

                        if (item["isDeleted"]?.Type != JTokenType.Boolean)
                            throw new InvalidOperationException("O Ente devolveu uma chave sem estado de exclusão válido.");
                        if ((bool)item["isDeleted"])
                        {
                            entidades.Remove(id);
                            continue;
                        }
                        entidades[id] = new EntidadeAuth
                        {
                            Id = id,
                            UriOtpauth = AbrirUri(item, chave),
                            AtualizadoEm = atualizado
                        };
                    }

                    if (itens.Count < LimitePagina) return entidades.Values.ToList();
                    // Sobrepõe o último microssegundo: não pula entidades que
                    // tenham o mesmo updatedAt na fronteira entre duas páginas.
                    if (maior <= desde + 1)
                        throw new InvalidOperationException("Não foi possível completar a paginação das chaves do Ente.");
                    desde = maior - 1;
                }
                throw new InvalidOperationException("A listagem das chaves do Ente excedeu o limite de páginas.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(chave);
            }
        }

        public void Enviar(SessaoEnteCli sessao, string uriOtpauth) =>
            Gravar(sessao, HttpMethod.Post, null, uriOtpauth);

        public void Atualizar(SessaoEnteCli sessao, string id, string uriOtpauth) =>
            Gravar(sessao, HttpMethod.Put, ValidarId(id), uriOtpauth);

        public void Remover(SessaoEnteCli sessao, string id)
        {
            ValidarSessao(sessao);
            Pedir(sessao, HttpMethod.Delete, "/authenticator/entity?id=" + ValidarId(id), null);
        }

        private void Gravar(SessaoEnteCli sessao, HttpMethod metodo, string id, string uriOtpauth)
        {
            ValidarSessao(sessao);
            if (string.IsNullOrWhiteSpace(uriOtpauth))
                throw new InvalidOperationException("URI otpauth vazia.");

            var chave = ObterAuthKey(sessao);
            var plano = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(uriOtpauth.Trim()));
            try
            {
                CriptoEnte.CifrarFluxo(plano, chave, out var dados, out var cabecalho);
                var corpo = new JObject
                {
                    ["encryptedData"] = Convert.ToBase64String(dados),
                    ["header"] = Convert.ToBase64String(cabecalho)
                };
                if (id != null) corpo["id"] = id;
                Pedir(sessao, metodo, "/authenticator/entity", corpo.ToString(Formatting.None));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(chave);
                CryptographicOperations.ZeroMemory(plano);
            }
        }

        private byte[] ObterAuthKey(SessaoEnteCli sessao)
        {
            var obj = LerObjeto(Pedir(sessao, HttpMethod.Get, "/authenticator/key", null));
            var userId = LerInteiro(obj["userID"], 0);
            if (userId != 0 && userId != sessao.UserId)
                throw new InvalidOperationException("A chave do Auth não é desta conta.");

            try
            {
                var chave = CriptoEnte.AbrirCaixa(
                    Convert.FromBase64String((string)obj["encryptedKey"] ?? ""),
                    Convert.FromBase64String((string)obj["header"] ?? ""),
                    sessao.MasterKey);
                if (chave.Length == 32) return chave;
                CryptographicOperations.ZeroMemory(chave);
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
            {
                throw new InvalidOperationException("Não foi possível abrir a chave do Ente Auth.");
            }
            throw new InvalidOperationException("Chave do Auth com tamanho inesperado.");
        }

        private static string AbrirUri(JToken item, byte[] chave)
        {
            byte[] plano = null;
            try
            {
                plano = CriptoEnte.DecifrarFluxo(
                    Convert.FromBase64String((string)item["encryptedData"] ?? ""), chave,
                    Convert.FromBase64String((string)item["header"] ?? ""));
                var valor = JToken.Parse(Encoding.UTF8.GetString(plano));
                if (valor.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)valor))
                    throw new InvalidOperationException("A chave do Ente não contém uma URI válida.");
                return (string)valor;
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException
                || ex is JsonException || ex is ArgumentException)
            {
                throw new InvalidOperationException("Não foi possível abrir uma das chaves do Ente Auth.");
            }
            finally
            {
                if (plano != null) CryptographicOperations.ZeroMemory(plano);
            }
        }

        private static JObject LerObjeto(string json)
        {
            try { return JObject.Parse(json); }
            catch (JsonException) { throw new InvalidOperationException("O Ente devolveu uma resposta inválida."); }
        }

        private static long LerInteiro(JToken valor, long padrao)
        {
            if (valor == null || valor.Type == JTokenType.Null) return padrao;
            if (valor.Type == JTokenType.Integer)
            {
                try { return (long)valor; }
                catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException) { }
            }
            throw new InvalidOperationException("O Ente devolveu um identificador ou timestamp inválido.");
        }

        private static string ValidarId(string id)
        {
            if (!Guid.TryParse(id, out var guid) || guid == Guid.Empty)
                throw new InvalidOperationException("Identidade da chave do Ente inválida. Atualize a lista.");
            return guid.ToString("D");
        }

        private static void ValidarSessao(SessaoEnteCli sessao)
        {
            if (sessao == null) throw new ArgumentNullException(nameof(sessao));
        }

        private string Pedir(SessaoEnteCli sessao, HttpMethod metodo, string caminho, string corpo)
        {
            using (var req = new HttpRequestMessage(metodo, sessao.ApiBase.TrimEnd('/') + caminho))
            {
                req.Headers.TryAddWithoutValidation("X-Auth-Token", sessao.TokenHttp);
                req.Headers.TryAddWithoutValidation("X-Client-Package", "io.ente.auth");
                if (corpo != null)
                    req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");

                HttpResponseMessage resp;
                try { resp = _http.Send(req, CancellationToken.None); }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
                {
                    throw new InvalidOperationException("Não consegui falar com o Ente. Confira a conexão e tente novamente.");
                }
                using (resp)
                {
                    if ((int)resp.StatusCode == 401)
                        throw new InvalidOperationException(
                            "A sessão da Ente CLI expirou. Em Configurações → Ente, conecte a conta de novo.");
                    if ((int)resp.StatusCode == 404 && caminho.EndsWith("/key", StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Essa conta ainda não tem chave no Ente Auth. Abra o app uma vez e tente de novo.");
                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException("O Ente recusou a operação (HTTP " + (int)resp.StatusCode + ").");
                    return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
        }
    }
}
