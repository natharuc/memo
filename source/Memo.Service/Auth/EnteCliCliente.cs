using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Invoca o <c>ente.exe</c> (Ente CLI oficial) para exportar os códigos Auth.
    /// O plaintext <c>ente_auth.txt</c> só vive num diretório temporário e é apagado.
    /// </summary>
    public class EnteCliCliente
    {
        public const string UrlRelease = "https://github.com/ente/ente/releases?q=tag%3Acli-v0";
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

        public string CaminhoExe { get; }

        public EnteCliCliente(string caminhoExe = null)
        {
            CaminhoExe = string.IsNullOrWhiteSpace(caminhoExe) ? AcharExe() : caminhoExe.Trim();
        }

        public bool Encontrada => !string.IsNullOrEmpty(CaminhoExe) && File.Exists(CaminhoExe);

        public static string MensagemSemCli =>
            "Ente CLI não encontrada. Abra o Memo → Configurações → Ente e clique em Instalar.";

        public static string MensagemSemConta =>
            "Nenhuma conta 'auth' na Ente CLI. Em Configurações → Ente, clique em Conectar conta (app = auth).";

        /// <summary>
        /// Exporta os códigos TOTP (linhas otpauth://). Restaura o diretório de
        /// export original da conta auth. Apaga o temp mesmo se falhar.
        /// </summary>
        public List<EntradaAuth> Exportar()
        {
            if (!Encontrada)
                throw new InvalidOperationException(MensagemSemCli);

            var contas = ListarContas();
            ContaAuth conta = null;
            foreach (var c in contas)
            {
                if (c.EhAuth) { conta = c; break; }
            }
            if (conta == null)
                throw new InvalidOperationException(MensagemSemConta);

            var temp = Path.Combine(Path.GetTempPath(), "memo-ente-auth-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var original = conta.ExportDir;
            try
            {
                AtualizarDir(conta.Email, temp);
                var r = Rodar("export");
                if (r.Codigo != 0)
                    throw new InvalidOperationException(MensagemExport(r));

                var arquivo = Path.Combine(temp, "ente_auth.txt");
                if (!File.Exists(arquivo))
                    throw new InvalidOperationException(
                        "A Ente CLI não gerou ente_auth.txt. Confira se a conta auth está logada ('ente account list').");

                var texto = File.ReadAllText(arquivo, Encoding.UTF8);
                var entradas = ParserOtpAuth.ParsearExport(texto);
                if (entradas.Count == 0)
                    throw new InvalidOperationException("Nenhum código TOTP no export da Ente CLI.");
                return entradas;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(original))
                        AtualizarDir(conta.Email, original);
                }
                catch { /* best-effort: o temp some de qualquer jeito */ }

                try
                {
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, recursive: true);
                }
                catch { }
            }
        }

        public List<ContaAuth> ListarContas()
        {
            var r = Rodar("account", "list");
            if (r.Codigo != 0)
                throw new InvalidOperationException(
                    "Falha ao listar contas da Ente CLI: " + (r.Erro ?? r.Saida ?? ("exit " + r.Codigo)));
            return ContaAuth.Parsear(r.Saida + Environment.NewLine + r.Erro);
        }

        public static string PastaInstalacao => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "ente");

        public static string ExeInstalacao => Path.Combine(PastaInstalacao, "ente.exe");

        public string Versao()
        {
            var r = Rodar("version");
            var t = (r.Saida ?? string.Empty).Trim();
            if (t.Length == 0) t = (r.Erro ?? string.Empty).Trim();
            return t.Length == 0 ? null : t.Replace("\r", " ").Replace("\n", " ");
        }

        public static string AcharExe()
        {
            var cfg = Configuracoes.Atual != null ? Configuracoes.Atual.EnteCliCaminho : null;
            if (ArquivoOk(cfg)) return Path.GetFullPath(cfg.Trim());

            var env = Environment.GetEnvironmentVariable("ENTE_CLI");
            if (ArquivoOk(env)) return Path.GetFullPath(env.Trim());

            if (ArquivoOk(ExeInstalacao)) return ExeInstalacao;

            foreach (var nome in new[] { "ente.exe", "ente-cli.exe" })
            {
                var noPath = NoPath(nome);
                if (noPath != null) return noPath;
            }

            var locais = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ente", "ente.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ente", "ente.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ente", "ente.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "ente-cli", "current", "ente.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "ente.exe"),
            };
            foreach (var p in locais)
                if (ArquivoOk(p)) return p;

            return null;
        }

        private void AtualizarDir(string email, string dir)
        {
            var r = Rodar("account", "update", "--app", "auth", "--email", email, "--dir", dir);
            if (r.Codigo != 0)
                throw new InvalidOperationException(
                    "Não consegui apontar o export da Ente CLI: " + (r.Erro ?? r.Saida ?? ("exit " + r.Codigo)));
        }

        private ResultadoProc Rodar(params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = CaminhoExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using (var p = Process.Start(psi))
            {
                if (p == null)
                    throw new InvalidOperationException("Não foi possível iniciar a Ente CLI: " + CaminhoExe);

                var saidaTask = p.StandardOutput.ReadToEndAsync();
                var erroTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit((int)Timeout.TotalMilliseconds))
                {
                    try { p.Kill(); } catch { }
                    try { p.WaitForExit(3000); } catch { }
                    throw new InvalidOperationException(
                        "A Ente CLI demorou demais no export. Cadastre só a conta auth (não photos) com 'ente account add'.");
                }
                return new ResultadoProc
                {
                    Codigo = p.ExitCode,
                    Saida = saidaTask.GetAwaiter().GetResult(),
                    Erro = erroTask.GetAwaiter().GetResult()
                };
            }
        }

        private static string MensagemExport(ResultadoProc r)
        {
            var detalhe = (r.Erro ?? string.Empty).Trim();
            if (detalhe.Length == 0) detalhe = (r.Saida ?? string.Empty).Trim();
            if (detalhe.Length > 400) detalhe = detalhe.Substring(0, 400);
            return detalhe.Length > 0
                ? "Falha no 'ente export': " + detalhe
                : "Falha no 'ente export' (exit " + r.Codigo + ").";
        }

        private static bool ArquivoOk(string caminho) =>
            !string.IsNullOrWhiteSpace(caminho) && File.Exists(caminho.Trim());

        private static string NoPath(string nome)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in path.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var p = Path.Combine(dir.Trim().Trim('"'), nome);
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        private sealed class ResultadoProc
        {
            public int Codigo;
            public string Saida;
            public string Erro;
        }
    }

    public sealed class ContaAuth
    {
        public string Email { get; set; }
        public string App { get; set; }
        public string ExportDir { get; set; }

        public bool EhAuth =>
            !string.IsNullOrEmpty(App) &&
            App.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0;

        public static List<ContaAuth> Parsear(string texto)
        {
            var contas = new List<ContaAuth>();
            if (string.IsNullOrEmpty(texto)) return contas;

            ContaAuth atual = null;
            foreach (var bruta in texto.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                var linha = bruta.Trim();
                if (linha.StartsWith("===="))
                {
                    if (atual != null && !string.IsNullOrEmpty(atual.Email))
                        contas.Add(atual);
                    atual = new ContaAuth();
                    continue;
                }
                if (atual == null) continue;

                if (linha.StartsWith("Email:", StringComparison.OrdinalIgnoreCase))
                    atual.Email = linha.Substring("Email:".Length).Trim();
                else if (linha.StartsWith("App:", StringComparison.OrdinalIgnoreCase))
                    atual.App = linha.Substring("App:".Length).Trim();
                else if (linha.StartsWith("ExportDir:", StringComparison.OrdinalIgnoreCase))
                    atual.ExportDir = linha.Substring("ExportDir:".Length).Trim();
            }
            if (atual != null && !string.IsNullOrEmpty(atual.Email))
                contas.Add(atual);
            return contas;
        }
    }
}
