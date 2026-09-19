using System.Diagnostics;

namespace Memo.Service.Auth
{
    /// <summary>Um código TOTP vindo do export da Ente CLI (sem o URI completo).</summary>
    [DebuggerDisplay("{Rotulo}")]
    public class EntradaAuth
    {
        public string Issuer { get; set; }
        public string Account { get; set; }
        public string Label { get; set; }
        public string Secret { get; set; }
        public int Periodo { get; set; } = 30;
        public int Digitos { get; set; } = 6;
        public string Algoritmo { get; set; } = "SHA1";

        /// <summary>Texto curto para toast/lista — nunca inclui o secret.</summary>
        public string Rotulo
        {
            get
            {
                var issuer = (Issuer ?? string.Empty).Trim();
                var account = (Account ?? string.Empty).Trim();
                if (issuer.Length > 0 && account.Length > 0) return issuer + " · " + account;
                if (issuer.Length > 0) return issuer;
                if (account.Length > 0) return account;
                return (Label ?? string.Empty).Trim();
            }
        }
    }
}
