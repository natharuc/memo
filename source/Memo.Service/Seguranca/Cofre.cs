using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Memo.Service.Seguranca
{
    /// <summary>
    /// Ciclo de vida do cofre: criação da senha-mestra, destravamento, cache de
    /// sessão (protegido por DPAPI) e cifragem/decifragem dos documentos.
    /// A chave-mestra nunca é gravada em texto puro nem fica no código.
    /// </summary>
    public class Cofre
    {
        private const string MarcadorVerificacao = "memo-cofre-ok";
        private const int IteracoesPadrao = 200_000;

        private readonly string _arquivoConfig;  // vault.json (junto dos documentos, viaja no OneDrive)
        private readonly string _arquivoSessao;   // session.bin (local, protegido por DPAPI)
        private byte[] _chave;
        private DateTime _expiraEm;                // quando a sessão expira (UTC)

        public Cofre(string diretorioDocumentos)
        {
            Directory.CreateDirectory(diretorioDocumentos);
            _arquivoConfig = Path.Combine(diretorioDocumentos, "vault.json");

            var dirLocal = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memo");
            Directory.CreateDirectory(dirLocal);
            // Sessão isolada por diretório de documentos: cofres diferentes (ou
            // testes) nunca compartilham nem sobrescrevem a sessão um do outro.
            _arquivoSessao = Path.Combine(dirLocal, $"session-{IdDiretorio(diretorioDocumentos)}.bin");
        }

        private static string IdDiretorio(string diretorio)
        {
            var normalizado = Path.GetFullPath(diretorio).TrimEnd('\\', '/').ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizado));
                return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        public bool Inicializado => File.Exists(_arquivoConfig);
        public bool Destrancado => _chave != null;

        /// <summary>Momento (UTC) em que a sessão expira, ou null se trancado.</summary>
        public DateTime? ExpiraEmUtc => _chave != null ? _expiraEm : (DateTime?)null;

        /// <summary>Tempo até pedir a senha de novo, ou null se trancado (pode ser negativo).</summary>
        public TimeSpan? TempoRestante => _chave != null ? (TimeSpan?)(_expiraEm - DateTime.UtcNow) : null;

        /// <summary>
        /// Expiração da sessão lida do DISCO (UTC), ou null se não há sessão.
        /// É o estado compartilhado entre processos: se o CLI faz "lock" (apaga o
        /// arquivo de sessão), isto vira null e a GUI percebe na hora.
        /// </summary>
        public DateTime? ExpiraEmDiscoUtc()
        {
            try
            {
                if (!File.Exists(_arquivoSessao)) return null;
                var protegido = File.ReadAllBytes(_arquivoSessao);
                var dados = ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser);
                return new DateTime(BitConverter.ToInt64(dados, 0), DateTimeKind.Utc);
            }
            catch
            {
                return null;
            }
        }

        private class Config
        {
            public int Versao { get; set; } = 1;
            public string Salt { get; set; }
            public int Iteracoes { get; set; }
            public string Verificador { get; set; }
        }

        /// <summary>Cria o cofre com uma nova senha-mestra.</summary>
        public void Inicializar(string senha)
        {
            var salt = CryptoCofre.GerarAleatorio(CryptoCofre.TamanhoSalt);
            var chave = CryptoCofre.DerivarChave(senha, salt, IteracoesPadrao);

            var cfg = new Config
            {
                Salt = Convert.ToBase64String(salt),
                Iteracoes = IteracoesPadrao,
                Verificador = CryptoCofre.Cifrar(MarcadorVerificacao, chave)
            };

            File.WriteAllText(_arquivoConfig, JsonConvert.SerializeObject(cfg, Formatting.Indented));
            _chave = chave;
            SalvarSessao();
        }

        /// <summary>Tenta destrancar com a senha. Retorna false se a senha estiver errada.</summary>
        public bool Destrancar(string senha)
        {
            if (!Inicializado) return false;

            var cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_arquivoConfig));
            var salt = Convert.FromBase64String(cfg.Salt);
            var chave = CryptoCofre.DerivarChave(senha, salt, cfg.Iteracoes);

            if (!CryptoCofre.TentarDecifrar(cfg.Verificador, chave, out var marca) || marca != MarcadorVerificacao)
                return false;

            _chave = chave;
            SalvarSessao();
            return true;
        }

        /// <summary>Reaproveita a sessão recente (sem pedir senha) se ainda válida.</summary>
        public bool TentarDestrancarPelaSessao()
        {
            try
            {
                if (!File.Exists(_arquivoSessao)) return false;

                var protegido = File.ReadAllBytes(_arquivoSessao);
                var dados = ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser);

                var expira = new DateTime(BitConverter.ToInt64(dados, 0), DateTimeKind.Utc);
                if (DateTime.UtcNow > expira)
                {
                    Trancar();
                    return false;
                }

                var chave = new byte[CryptoCofre.TamanhoChave];
                Buffer.BlockCopy(dados, 8, chave, 0, CryptoCofre.TamanhoChave);

                // Só confia na chave da sessão se ela realmente abrir o cofre atual.
                // Isso impede que uma sessão antiga/estranha (de outro vault.json)
                // seja usada e acabe re-cifrando documentos com a chave errada.
                if (!ChaveAbreCofre(chave))
                {
                    Trancar();
                    return false;
                }

                // Expiração ABSOLUTA: a sessão conta a partir da última vez que a
                // senha foi digitada e NÃO é renovada a cada uso. Assim o prazo
                // configurado realmente vence (antes, o uso renovava sem parar).
                _chave = chave;
                _expiraEm = expira;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ChaveAbreCofre(byte[] chave)
        {
            if (!Inicializado) return false;

            try
            {
                var cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_arquivoConfig));
                return CryptoCofre.TentarDecifrar(cfg.Verificador, chave, out var marca)
                       && marca == MarcadorVerificacao;
            }
            catch
            {
                return false;
            }
        }

        public void Trancar()
        {
            _chave = null;
            try { if (File.Exists(_arquivoSessao)) File.Delete(_arquivoSessao); }
            catch { /* ignora */ }
        }

        /// <summary>Re-ancora o prazo da sessão em "agora + duração atual" (se destrancado).</summary>
        public void RenovarSessao()
        {
            if (_chave != null) SalvarSessao();
        }

        public string Cifrar(string texto) => CryptoCofre.Cifrar(texto, ChaveObrigatoria());

        public string Decifrar(string base64)
        {
            if (TentarDecifrar(base64, out var plano, out _))
                return plano;

            throw new CryptographicException("Não foi possível decifrar o documento.");
        }

        /// <summary>
        /// Tenta decifrar sem lançar exceção. Cobre o formato novo, o legado e o
        /// caso de cifragem em camadas (gerado pelo antigo "--rebuild").
        /// </summary>
        public bool TentarDecifrar(string base64, out string plano, out bool eraLegado)
        {
            plano = null;
            eraLegado = false;

            if (CryptoCofre.TentarDecifrar(base64, ChaveObrigatoria(), out plano))
                return true;

            eraLegado = true;
            var atual = base64;
            for (var camada = 0; camada < 4; camada++)
            {
                if (!CryptoCofre.TentarDecifrarLegado(atual, out var saida))
                    break;

                if (PareceJson(saida))
                {
                    plano = saida;
                    return true;
                }

                atual = saida; // pode haver outra camada de cifragem por cima
            }

            return false;
        }

        private static bool PareceJson(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return false;
            var t = texto.TrimStart();
            return t.StartsWith("{") || t.StartsWith("[");
        }

        private void SalvarSessao()
        {
            if (_chave == null) return;

            _expiraEm = DateTime.UtcNow.Add(Configuracoes.Atual.DuracaoSessao);

            var dados = new byte[8 + CryptoCofre.TamanhoChave];
            Buffer.BlockCopy(BitConverter.GetBytes(_expiraEm.Ticks), 0, dados, 0, 8);
            Buffer.BlockCopy(_chave, 0, dados, 8, CryptoCofre.TamanhoChave);

            var protegido = ProtectedData.Protect(dados, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_arquivoSessao, protegido);
        }

        private byte[] ChaveObrigatoria()
            => _chave ?? throw new InvalidOperationException("O cofre está trancado.");
    }
}
