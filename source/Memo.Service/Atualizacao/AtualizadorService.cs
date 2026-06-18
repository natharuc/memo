using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Memo.Service.Atualizacao
{
    /// <summary>Dados da release mais recente quando ela é mais nova que a versão atual.</summary>
    public class InfoAtualizacao
    {
        public Version Versao { get; set; }
        public string Tag { get; set; }
        public string UrlExe { get; set; }
        public string Sha256 { get; set; }
        public string Notas { get; set; }
    }

    /// <summary>
    /// Consulta as releases do GitHub, baixa o novo executável (validando o SHA256) e
    /// faz a troca do .exe em execução. Não toca no vault nem na sessão.
    /// </summary>
    public class AtualizadorService
    {
        private const string ApiUrl =
            "https://api.github.com/repos/natharuc/memo/releases/latest";

        private static readonly HttpClient Http = CriarHttp();

        public Version VersaoAtual { get; }

        public AtualizadorService(Version versaoAtual)
        {
            VersaoAtual = Normalizar(versaoAtual);
        }

        private static HttpClient CriarHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // A API do GitHub exige um User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Memo-Updater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return http;
        }

        /// <summary>Devolve a release mais recente se for mais nova; senão, null. Nunca lança.</summary>
        public async Task<InfoAtualizacao> VerificarAsync(CancellationToken ct = default)
        {
            try
            {
                var json = await Http.GetStringAsync(ApiUrl, ct).ConfigureAwait(false);
                var release = JObject.Parse(json);

                var tag = (string)release["tag_name"];
                if (string.IsNullOrWhiteSpace(tag)) return null;

                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var versao)) return null;
                versao = Normalizar(versao);
                if (versao <= VersaoAtual) return null;

                var assets = release["assets"] as JArray ?? new JArray();
                var urlExe = UrlDoAsset(assets, "Memo.exe");
                if (urlExe == null) return null;

                return new InfoAtualizacao
                {
                    Versao = versao,
                    Tag = tag,
                    UrlExe = urlExe,
                    Sha256 = await LerSha256Async(assets, ct).ConfigureAwait(false),
                    Notas = (string)release["body"]
                };
            }
            catch
            {
                // Offline, rate-limit, JSON inesperado: trata como "sem atualização".
                return null;
            }
        }

        /// <summary>Baixa o novo .exe para a pasta temporária e valida o SHA256. Retorna o caminho.</summary>
        public async Task<string> BaixarAsync(InfoAtualizacao info, IProgress<double> progresso = null,
            CancellationToken ct = default)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Memo", "update");
            Directory.CreateDirectory(dir);
            var destino = Path.Combine(dir, "Memo-new.exe");

            using (var resposta = await Http.GetAsync(info.UrlExe,
                       HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resposta.EnsureSuccessStatusCode();
                var total = resposta.Content.Headers.ContentLength ?? -1L;

                using (var origem = await resposta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                using (var arquivo = File.Create(destino))
                {
                    var buffer = new byte[81920];
                    long lido = 0;
                    int n;
                    while ((n = await origem.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await arquivo.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                        lido += n;
                        if (total > 0) progresso?.Report((double)lido / total);
                    }
                }
            }

            if (!string.IsNullOrEmpty(info.Sha256))
            {
                var hash = CalcularSha256(destino);
                if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(destino);
                    throw new InvalidOperationException(
                        "A verificação de integridade (SHA256) do arquivo baixado falhou.");
                }
            }

            return destino;
        }

        /// <summary>
        /// Renomeia o .exe atual para .old, coloca o novo no lugar e inicia o novo processo.
        /// O chamador deve encerrar o app logo em seguida.
        /// </summary>
        public void AplicarEReiniciar(string novoExe)
        {
            var atual = Environment.ProcessPath;
            if (string.IsNullOrEmpty(atual))
                throw new InvalidOperationException("Não foi possível localizar o executável atual.");

            var antigo = atual + ".old";
            if (File.Exists(antigo)) File.Delete(antigo);

            // O Windows permite renomear um .exe em uso (mas não sobrescrevê-lo).
            File.Move(atual, antigo);
            File.Move(novoExe, atual);

            // Passa o PID atual para o novo processo esperar este sair antes de
            // assumir a instância única (senão ele se acha "2ª instância" e fecha).
            Process.Start(new ProcessStartInfo(atual)
            {
                UseShellExecute = true,
                Arguments = $"--apos-atualizacao {Environment.ProcessId}"
            });
        }

        /// <summary>Apaga resíduos (*.old) deixados por uma atualização anterior. Nunca lança.</summary>
        public static void LimparResiduos()
        {
            try
            {
                var dir = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrEmpty(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*.old"))
                {
                    try { File.Delete(f); } catch { /* ainda em uso? ignora */ }
                }
            }
            catch
            {
                // Limpeza é best-effort.
            }
        }

        private static string UrlDoAsset(JArray assets, string nome)
        {
            var asset = assets.FirstOrDefault(a =>
                string.Equals((string)a["name"], nome, StringComparison.OrdinalIgnoreCase));
            return (string)asset?["browser_download_url"];
        }

        private async Task<string> LerSha256Async(JArray assets, CancellationToken ct)
        {
            var url = UrlDoAsset(assets, "Memo.exe.sha256");
            if (url == null) return null;
            try
            {
                var texto = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
                // Formato "<hash>  Memo.exe" — pega o primeiro token.
                return texto.Trim().Split(new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string CalcularSha256(string caminho)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(caminho))
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static Version Normalizar(Version v) =>
            new Version(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));
    }
}
