using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        /// <summary>URL do pacote .zip da release.</summary>
        public string UrlPacote { get; set; }
        /// <summary>SHA256 do pacote .zip (para validar o download).</summary>
        public string Sha256 { get; set; }
        public string Notas { get; set; }
    }

    /// <summary>
    /// Consulta as releases do GitHub, baixa o pacote .zip (validando o SHA256),
    /// extrai e troca o .exe em execução. Não toca no vault nem na sessão.
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
                var urlZip = UrlDoPacote(assets);
                if (urlZip == null) return null;

                return new InfoAtualizacao
                {
                    Versao = versao,
                    Tag = tag,
                    UrlPacote = urlZip,
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

        /// <summary>
        /// Baixa o pacote .zip para a pasta temporária, valida o SHA256 e o extrai.
        /// Retorna o caminho do <c>Memo.exe</c> extraído.
        /// </summary>
        public async Task<string> BaixarAsync(InfoAtualizacao info, IProgress<double> progresso = null,
            CancellationToken ct = default)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Memo", "update");

            // Começa limpo (restos de uma atualização anterior podem estar travados).
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
            Directory.CreateDirectory(dir);

            var zip = Path.Combine(dir, "Memo-update.zip");

            using (var resposta = await Http.GetAsync(info.UrlPacote,
                       HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resposta.EnsureSuccessStatusCode();
                var total = resposta.Content.Headers.ContentLength ?? -1L;

                using (var origem = await resposta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                using (var arquivo = File.Create(zip))
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
                var hash = CalcularSha256(zip);
                if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(zip);
                    throw new InvalidOperationException(
                        "A verificação de integridade (SHA256) do pacote baixado falhou.");
                }
            }

            var extraido = Path.Combine(dir, "extraido");
            Directory.CreateDirectory(extraido);
            ZipFile.ExtractToDirectory(zip, extraido, overwriteFiles: true);

            var exe = Directory.GetFiles(extraido, "Memo.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe == null)
                throw new InvalidOperationException("O pacote de atualização não contém Memo.exe.");

            return exe;
        }

        /// <summary>
        /// Aplica o pacote extraído (pasta de <paramref name="novoExe"/>): sobrescreve
        /// os arquivos auxiliares, troca o .exe em execução (renomeia o atual para
        /// .old) e reinicia. O chamador deve encerrar o app logo em seguida.
        /// </summary>
        public void AplicarEReiniciar(string novoExe)
        {
            var atual = Environment.ProcessPath;
            if (string.IsNullOrEmpty(atual))
                throw new InvalidOperationException("Não foi possível localizar o executável atual.");

            var dirAtual = Path.GetDirectoryName(atual);
            var dirNovo = Path.GetDirectoryName(novoExe);
            var nomeExe = Path.GetFileName(atual);

            // 1) Arquivos auxiliares do pacote (ex.: memo-cli.exe) — não estão em uso.
            //    Best-effort: um que esteja travado fica para a próxima atualização.
            foreach (var origem in Directory.GetFiles(dirNovo))
            {
                var nome = Path.GetFileName(origem);
                if (string.Equals(nome, nomeExe, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Copy(origem, Path.Combine(dirAtual, nome), overwrite: true); }
                catch { /* opcional/em uso */ }
            }

            // 2) O .exe em execução não pode ser sobrescrito: renomeia para .old e põe o novo.
            var antigo = atual + ".old";
            if (File.Exists(antigo)) File.Delete(antigo);
            File.Move(atual, antigo);
            File.Copy(novoExe, atual, overwrite: false);

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

        /// <summary>URL do primeiro asset <c>.zip</c> da release (ignora os .sha256).</summary>
        private static string UrlDoPacote(JArray assets)
        {
            var asset = assets.FirstOrDefault(a =>
            {
                var nome = (string)a["name"];
                return nome != null && nome.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            });
            return (string)asset?["browser_download_url"];
        }

        private async Task<string> LerSha256Async(JArray assets, CancellationToken ct)
        {
            string Url(Func<string, bool> ok) => (string)assets.FirstOrDefault(a =>
            {
                var nome = (string)a["name"];
                return nome != null && ok(nome);
            })?["browser_download_url"];

            var url = Url(n => n.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase))
                      ?? Url(n => n.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
            if (url == null) return null;
            try
            {
                var texto = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
                // Formato "<hash>  <arquivo>" — pega o primeiro token.
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
