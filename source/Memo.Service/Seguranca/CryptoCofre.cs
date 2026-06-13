using System;
using System.Security.Cryptography;
using System.Text;

namespace Memo.Service.Seguranca
{
    /// <summary>
    /// Primitivas de criptografia do cofre.
    /// Formato novo: AES-256-GCM (cifra autenticada) com chave derivada por PBKDF2-SHA256.
    /// Também sabe ler o formato legado (chave fixa AES-128-CBC) apenas para
    /// permitir a migração transparente dos documentos antigos na primeira leitura.
    /// </summary>
    public static class CryptoCofre
    {
        public const byte Versao = 0x02;
        public const int TamanhoChave = 32;  // AES-256
        public const int TamanhoSalt = 16;
        private const int TamanhoNonce = 12; // 96 bits, recomendado para GCM
        private const int TamanhoTag = 16;   // 128 bits

        public static byte[] GerarAleatorio(int tamanho)
        {
            var buffer = new byte[tamanho];
            RandomNumberGenerator.Fill(buffer);
            return buffer;
        }

        public static byte[] DerivarChave(string senha, byte[] salt, int iteracoes)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(senha ?? string.Empty),
                salt,
                iteracoes,
                HashAlgorithmName.SHA256,
                TamanhoChave);
        }

        public static string Cifrar(string textoPlano, byte[] chave)
        {
            var nonce = GerarAleatorio(TamanhoNonce);
            var plano = Encoding.UTF8.GetBytes(textoPlano ?? string.Empty);
            var cifra = new byte[plano.Length];
            var tag = new byte[TamanhoTag];

            using (var gcm = new AesGcm(chave, TamanhoTag))
                gcm.Encrypt(nonce, plano, cifra, tag);

            var saida = new byte[1 + TamanhoNonce + TamanhoTag + cifra.Length];
            saida[0] = Versao;
            Buffer.BlockCopy(nonce, 0, saida, 1, TamanhoNonce);
            Buffer.BlockCopy(tag, 0, saida, 1 + TamanhoNonce, TamanhoTag);
            Buffer.BlockCopy(cifra, 0, saida, 1 + TamanhoNonce + TamanhoTag, cifra.Length);
            return Convert.ToBase64String(saida);
        }

        public static bool TentarDecifrar(string base64, byte[] chave, out string textoPlano)
        {
            textoPlano = null;

            byte[] dados;
            try { dados = Convert.FromBase64String(base64); }
            catch { return false; }

            if (dados.Length < 1 + TamanhoNonce + TamanhoTag || dados[0] != Versao)
                return false;

            var nonce = new byte[TamanhoNonce];
            var tag = new byte[TamanhoTag];
            var cifra = new byte[dados.Length - 1 - TamanhoNonce - TamanhoTag];
            Buffer.BlockCopy(dados, 1, nonce, 0, TamanhoNonce);
            Buffer.BlockCopy(dados, 1 + TamanhoNonce, tag, 0, TamanhoTag);
            Buffer.BlockCopy(dados, 1 + TamanhoNonce + TamanhoTag, cifra, 0, cifra.Length);

            var plano = new byte[cifra.Length];
            try
            {
                using (var gcm = new AesGcm(chave, TamanhoTag))
                    gcm.Decrypt(nonce, cifra, tag, plano);
            }
            catch (CryptographicException)
            {
                return false; // senha incorreta ou conteúdo adulterado
            }

            textoPlano = Encoding.UTF8.GetString(plano);
            return true;
        }

        // ----- Formato legado (somente leitura, para migração transparente) -----

        public static bool EhFormatoLegado(string base64)
        {
            try
            {
                var dados = Convert.FromBase64String(base64);
                return dados.Length == 0 || dados[0] != Versao;
            }
            catch { return false; }
        }

        public static bool TentarDecifrarLegado(string base64, out string textoPlano)
        {
            textoPlano = null;
            try
            {
                // Chave e IV fixos do esquema antigo. Mantidos apenas para
                // conseguir abrir e reescrever os documentos existentes.
                var chave = Encoding.UTF8.GetBytes("9784612435679864");
                var iv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
                var entrada = Convert.FromBase64String(base64);

                using (var aes = Aes.Create())
                {
                    aes.Key = chave;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (var dec = aes.CreateDecryptor())
                    {
                        var saida = dec.TransformFinalBlock(entrada, 0, entrada.Length);
                        textoPlano = Encoding.UTF8.GetString(saida);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
