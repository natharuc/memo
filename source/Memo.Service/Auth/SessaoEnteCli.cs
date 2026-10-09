using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Sessão auth já gravada pela Ente CLI: device key no Credential Manager
    /// e conta cifrada em <c>%USERPROFILE%\.ente\ente-cli.db</c>.
    /// </summary>
    public sealed class SessaoEnteCli
    {
        public string Email { get; private set; }
        public long UserId { get; private set; }
        public byte[] Token { get; private set; }
        public byte[] MasterKey { get; private set; }
        public string ApiBase { get; private set; }

        public string TokenHttp =>
            Convert.ToBase64String(Token).Replace('+', '-').Replace('/', '_');

        public static SessaoEnteCli Carregar()
        {
            var device = ChaveDispositivo();
            try
            {
                var db = CaminhoDb();
                if (!File.Exists(db))
                    throw new InvalidOperationException(
                        "Não achei o ente-cli.db. Em Configurações → Ente, conecte a conta Auth.");

                var pares = LeitorBolt.LerBalde(db, "accounts");
                foreach (var par in pares)
                {
                    ContaJson conta;
                    try { conta = JsonConvert.DeserializeObject<ContaJson>(Encoding.UTF8.GetString(par.Value)); }
                    catch { continue; }
                    if (conta == null || conta.App == null ||
                        conta.App.IndexOf("auth", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (conta.MasterKey == null || conta.Token == null)
                        continue;

                    var master = Decifrar(conta.MasterKey, device);
                    var token = Decifrar(conta.Token, device);
                    if (master == null || token == null || master.Length != 32)
                        throw new InvalidOperationException(
                            "Não consegui abrir a sessão auth da Ente CLI. Conecte a conta de novo.");

                    return new SessaoEnteCli
                    {
                        Email = conta.Email,
                        UserId = conta.UserId,
                        Token = token,
                        MasterKey = master,
                        ApiBase = LerApi()
                    };
                }

                throw new InvalidOperationException(
                    "Nenhuma conta auth no ente-cli.db. Em Configurações → Ente, conecte a conta (app = auth).");
            }
            finally
            {
                if (device != null) Array.Clear(device, 0, device.Length);
            }
        }

        public void Limpar()
        {
            if (Token != null) Array.Clear(Token, 0, Token.Length);
            if (MasterKey != null) Array.Clear(MasterKey, 0, MasterKey.Length);
            Token = null;
            MasterKey = null;
        }

        public override string ToString() => Email ?? "auth";

        private static byte[] Decifrar(EncJson enc, byte[] device)
        {
            if (enc == null || string.IsNullOrEmpty(enc.CipherText) || string.IsNullOrEmpty(enc.Nonce))
                return null;
            try
            {
                return CriptoEnte.DecifrarFluxo(
                    Convert.FromBase64String(enc.CipherText),
                    device,
                    Convert.FromBase64String(enc.Nonce));
            }
            catch
            {
                return null;
            }
        }

        public static string PastaCli()
        {
            var dir = Environment.GetEnvironmentVariable("ENTE_CLI_CONFIG_DIR");
            if (string.IsNullOrWhiteSpace(dir))
                dir = Environment.GetEnvironmentVariable("ENTE_CLI_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(dir))
                return dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ente");
        }

        private static string CaminhoDb() => Path.Combine(PastaCli(), "ente-cli.db");

        private static string LerApi()
        {
            var padrao = "https://api.ente.com";
            var yaml = Path.Combine(PastaCli(), "config.yaml");
            if (!File.Exists(yaml)) return padrao;
            try
            {
                foreach (var bruta in File.ReadAllLines(yaml))
                {
                    var linha = bruta.Trim();
                    if (!linha.StartsWith("api:", StringComparison.OrdinalIgnoreCase)) continue;
                    var v = linha.Substring(4).Trim().Trim('"', '\'');
                    if (v.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return v.TrimEnd('/');
                }
            }
            catch { }
            return padrao;
        }

        private static byte[] ChaveDispositivo()
        {
            var arquivo = Environment.GetEnvironmentVariable("ENTE_CLI_SECRETS_PATH");
            if (!string.IsNullOrWhiteSpace(arquivo) && File.Exists(arquivo))
            {
                var cru = File.ReadAllBytes(arquivo);
                if (cru.Length == 32) return cru;
                throw new InvalidOperationException("ENTE_CLI_SECRETS_PATH não tem 32 bytes.");
            }

            var blob = LerCredencial("ente:ente-cli-user") ?? LerCredencial("ente");
            if (blob == null || blob.Length == 0)
                throw new InvalidOperationException(
                    "Não achei a chave da Ente CLI no Windows. Conecte a conta em Configurações → Ente.");

            if (blob.Length == 32) return blob;

            var texto = Encoding.UTF8.GetString(blob).Trim();
            if (texto.IndexOf('\0') >= 0)
                texto = Encoding.Unicode.GetString(blob).Trim('\0', ' ', '\r', '\n');
            try
            {
                var chave = Convert.FromBase64String(texto);
                if (chave.Length == 32) return chave;
            }
            catch { }

            throw new InvalidOperationException(
                "A chave da Ente CLI no Windows não tem o formato esperado.");
        }

        private static byte[] LerCredencial(string alvo)
        {
            IntPtr cred;
            if (!CredRead(alvo, 1, 0, out cred)) return null;
            try
            {
                var c = Marshal.PtrToStructure<CREDENTIAL>(cred);
                if (c.CredentialBlobSize <= 0 || c.CredentialBlob == IntPtr.Zero) return null;
                var blob = new byte[c.CredentialBlobSize];
                Marshal.Copy(c.CredentialBlob, blob, 0, blob.Length);
                return blob;
            }
            finally
            {
                CredFree(cred);
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr credential);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        private sealed class ContaJson
        {
            [JsonProperty("email")] public string Email { get; set; }
            [JsonProperty("userID")] public long UserId { get; set; }
            [JsonProperty("app")] public string App { get; set; }
            [JsonProperty("masterKey")] public EncJson MasterKey { get; set; }
            [JsonProperty("token")] public EncJson Token { get; set; }
        }

        private sealed class EncJson
        {
            [JsonProperty("cipherText")] public string CipherText { get; set; }
            [JsonProperty("nonce")] public string Nonce { get; set; }
        }
    }
}
