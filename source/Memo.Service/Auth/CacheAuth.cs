using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Cache dos parâmetros TOTP (incluindo o secret) em DPAPI CurrentUser,
    /// em <c>%LOCALAPPDATA%\Memo\ente-auth.bin</c>. Nunca logar o conteúdo.
    /// </summary>
    public class CacheAuth
    {
        public static readonly TimeSpan Validade = TimeSpan.FromHours(6);

        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "ente-auth.bin");

        private static readonly object Trava = new object();

        public DateTime AtualizadoEmUtc { get; set; }
        public List<EntradaAuth> Entradas { get; set; } = new List<EntradaAuth>();

        [JsonIgnore]
        public bool Vencido => DateTime.UtcNow - AtualizadoEmUtc > Validade;

        [JsonIgnore]
        public bool Vazio => Entradas == null || Entradas.Count == 0;

        public static CacheAuth Carregar()
        {
            lock (Trava)
            {
                try
                {
                    if (File.Exists(Caminho))
                    {
                        var protegido = File.ReadAllBytes(Caminho);
                        var json = Encoding.UTF8.GetString(
                            ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser));
                        return JsonConvert.DeserializeObject<CacheAuth>(json) ?? new CacheAuth();
                    }
                }
                catch
                {
                    // Ausente / de outra conta / corrompido: começa vazio.
                }
                return new CacheAuth();
            }
        }

        public void Salvar()
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
                AtualizadoEmUtc = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(this);
                var protegido = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(Caminho, protegido);
            }
        }
    }
}
