using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Memo.Service.Auth
{
    public class StatusEnteCli
    {
        public bool Instalada { get; set; }
        public bool ContaAuth { get; set; }
        public string Caminho { get; set; }
        public string Versao { get; set; }
        public string Email { get; set; }
        public string Mensagem { get; set; }
    }

    /// <summary>
    /// Baixa a Ente CLI oficial (GitHub release <c>cli-v*</c>), instala em
    /// <c>%LOCALAPPDATA%\Memo\ente</c>, grava o caminho na config e coloca a pasta
    /// no PATH do usuário Windows.
    /// </summary>
    public class EnteCliInstalador
    {
        private const string ApiRefs = "https://api.github.com/repos/ente-io/ente/git/matching-refs/tags/cli-v";
        private const string ApiRelease = "https://api.github.com/repos/ente-io/ente/releases/tags/";

        private static readonly HttpClient Http = CriarHttp();

        private static HttpClient CriarHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Memo-EnteCli");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return http;
        }

        public static StatusEnteCli Consultar()
        {
            var exe = EnteCliCliente.AcharExe();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                return new StatusEnteCli { Instalada = false, Mensagem = "Ente CLI não instalada." };

            var cli = new EnteCliCliente(exe);
            string versao = null;
            try { versao = cli.Versao(); } catch { }

            ContaAuth auth = null;
            try
            {
                var contas = cli.ListarContas();
                foreach (var c in contas)
                    if (c.EhAuth) { auth = c; break; }
            }
            catch { }

            if (auth == null)
            {
                return new StatusEnteCli
                {
                    Instalada = true,
                    ContaAuth = false,
                    Caminho = exe,
                    Versao = versao,
                    Mensagem = "CLI instalada. Falta conectar a conta Auth (app = auth, mesmo e-mail do Ente Auth)."
                };
            }

            return new StatusEnteCli
            {
                Instalada = true,
                ContaAuth = true,
                Caminho = exe,
                Versao = versao,
                Email = auth.Email,
                Mensagem = "Pronto. Conta " + auth.Email + " conectada."
            };
        }

        /// <summary>Baixa, extrai, aponta a config e coloca no PATH do usuário. Devolve o caminho do exe.</summary>
        public async Task<string> InstalarAsync(IProgress<double> progresso = null, CancellationToken ct = default)
        {
            progresso?.Report(0.02);
            var tag = await DescobrirTagAsync(ct).ConfigureAwait(false);
            var (urlZip, nomeZip, urlSums) = await DescobrirAssetAsync(tag, ct).ConfigureAwait(false);

            var temp = Path.Combine(Path.GetTempPath(), "memo-ente-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var zip = Path.Combine(temp, nomeZip);
            try
            {
                await BaixarAsync(urlZip, zip, progresso, 0.05, 0.75, ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(urlSums))
                {
                    var esperado = await HashEsperadoAsync(urlSums, nomeZip, ct).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(esperado))
                    {
                        var atual = CalcularSha256(zip);
                        if (!string.Equals(atual, esperado, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("A verificação de integridade (SHA256) da Ente CLI falhou.");
                    }
                }

                progresso?.Report(0.80);
                var extraido = Path.Combine(temp, "extraido");
                Directory.CreateDirectory(extraido);
                ZipFile.ExtractToDirectory(zip, extraido, overwriteFiles: true);

                var origem = Directory.GetFiles(extraido, "ente.exe", SearchOption.AllDirectories).FirstOrDefault()
                             ?? Directory.GetFiles(extraido, "ente-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (origem == null)
                    throw new InvalidOperationException("O pacote da Ente CLI não contém ente.exe.");

                var pasta = EnteCliCliente.PastaInstalacao;
                Directory.CreateDirectory(pasta);
                var destino = EnteCliCliente.ExeInstalacao;
                File.Copy(origem, destino, overwrite: true);

                progresso?.Report(0.92);
                var cfg = Configuracoes.Atual;
                cfg.EnteCliCaminho = destino;
                cfg.Salvar();
                GarantirNoPathDoUsuario(pasta);

                progresso?.Report(1);
                return destino;
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { }
            }
        }

        public static void AbrirCadastroDeConta(string exe)
        {
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                throw new InvalidOperationException("Ente CLI não encontrada.");

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k echo Quando perguntar o app, digite: auth & echo Use o mesmo e-mail do Ente Auth. & echo. & \"" + exe + "\" account add",
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = true
            });
        }

        public static void GarantirNoPathDoUsuario(string pasta)
        {
            if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta)) return;
            pasta = Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar);

            var user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
            if (!ContemPasta(user, pasta))
            {
                var novo = string.IsNullOrWhiteSpace(user) ? pasta : user.TrimEnd(';') + ";" + pasta;
                Environment.SetEnvironmentVariable("Path", novo, EnvironmentVariableTarget.User);
            }

            var proc = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!ContemPasta(proc, pasta))
                Environment.SetEnvironmentVariable("PATH",
                    string.IsNullOrWhiteSpace(proc) ? pasta : proc.TrimEnd(';') + ";" + pasta,
                    EnvironmentVariableTarget.Process);
        }

        private static bool ContemPasta(string path, string pasta)
        {
            foreach (var p in (path ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    if (string.Equals(Path.GetFullPath(p.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar),
                            pasta, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static async Task<string> DescobrirTagAsync(CancellationToken ct)
        {
            var json = await Http.GetStringAsync(ApiRefs, ct).ConfigureAwait(false);
            var arr = JArray.Parse(json);
            string melhorTag = null;
            Version melhor = null;
            foreach (var item in arr)
            {
                var reff = (string)item["ref"];
                if (string.IsNullOrEmpty(reff)) continue;
                var tag = reff.StartsWith("refs/tags/", StringComparison.Ordinal) ? reff.Substring("refs/tags/".Length) : reff;
                if (!tag.StartsWith("cli-v", StringComparison.OrdinalIgnoreCase)) continue;
                var verTxt = tag.Substring("cli-v".Length);
                if (!Version.TryParse(verTxt, out var ver)) continue;
                if (melhor == null || ver > melhor) { melhor = ver; melhorTag = tag; }
            }
            if (melhorTag == null)
                throw new InvalidOperationException("Não achei uma release cli-v* da Ente CLI no GitHub.");
            return melhorTag;
        }

        private static async Task<(string urlZip, string nomeZip, string urlSums)> DescobrirAssetAsync(string tag, CancellationToken ct)
        {
            var json = await Http.GetStringAsync(ApiRelease + Uri.EscapeDataString(tag), ct).ConfigureAwait(false);
            var release = JObject.Parse(json);
            var assets = release["assets"] as JArray ?? new JArray();
            var sufixo = SufixoArquitetura();
            JToken zip = null;
            JToken sums = null;
            foreach (var a in assets)
            {
                var nome = (string)a["name"] ?? string.Empty;
                if (nome.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase)) sums = a;
                else if (nome.IndexOf(sufixo, StringComparison.OrdinalIgnoreCase) >= 0 &&
                         nome.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    zip = a;
            }
            if (zip == null)
                throw new InvalidOperationException("A release " + tag + " não tem o zip " + sufixo + ".");
            return ((string)zip["browser_download_url"], (string)zip["name"],
                sums == null ? null : (string)sums["browser_download_url"]);
        }

        private static string SufixoArquitetura()
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.Arm64: return "windows-arm64";
                case Architecture.X86: return "windows-386";
                default: return "windows-amd64";
            }
        }

        private static async Task BaixarAsync(string url, string destino, IProgress<double> progresso,
            double inicio, double fim, CancellationToken ct)
        {
            using (var resposta = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
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
                        if (total > 0 && progresso != null)
                            progresso.Report(inicio + (fim - inicio) * ((double)lido / total));
                    }
                }
            }
        }

        private static async Task<string> HashEsperadoAsync(string urlSums, string nomeZip, CancellationToken ct)
        {
            try
            {
                var texto = await Http.GetStringAsync(urlSums, ct).ConfigureAwait(false);
                foreach (var linha in texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (linha.IndexOf(nomeZip, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var token = linha.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (token.Length > 0 && token[0].Length >= 32) return token[0];
                }
            }
            catch { }
            return null;
        }

        private static string CalcularSha256(string caminho)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(caminho))
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
