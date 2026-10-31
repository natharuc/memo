using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Memo.Service.Seguranca;
using Newtonsoft.Json;
using Xunit;

namespace Memo.Service.Tests
{
    public class CofreValidacaoTests : IDisposable
    {
        private const string Senha = "senha de teste isolada";
        private readonly string _diretorio;
        private readonly string _arquivoConfig;
        private readonly string _arquivoSessao;
        private readonly Cofre _cofre;

        public CofreValidacaoTests()
        {
            _diretorio = Path.Combine(Path.GetTempPath(), "memo-cofre-validacao-" + Guid.NewGuid().ToString("N"));
            _arquivoConfig = Path.Combine(_diretorio, "vault.json");
            _cofre = new Cofre(_diretorio);
            var normalizado = Path.GetFullPath(_diretorio).TrimEnd('\\', '/').ToLowerInvariant();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizado));
            var id = Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
            _arquivoSessao = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memo", "session-" + id + ".bin");
        }

        [Fact]
        public void Senha_correta_valida_sem_destrancar_criar_sessao_ou_modificar_cofre()
        {
            CriarVerificador();
            var original = File.ReadAllBytes(_arquivoConfig);

            Assert.True(_cofre.ValidarSenha(Senha));

            Assert.False(_cofre.Destrancado);
            Assert.Null(_cofre.ExpiraEmUtc);
            Assert.False(File.Exists(_arquivoSessao));
            Assert.Equal(original, File.ReadAllBytes(_arquivoConfig));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("senha errada")]
        public void Senha_invalida_nao_autoriza_nem_cria_sessao(string senha)
        {
            CriarVerificador();

            Assert.False(_cofre.ValidarSenha(senha));

            Assert.False(_cofre.Destrancado);
            Assert.False(File.Exists(_arquivoSessao));
        }

        [Fact]
        public void Cofre_ausente_nao_e_criado_pela_validacao()
        {
            Assert.False(_cofre.ValidarSenha(Senha));
            Assert.False(_cofre.Inicializado);
            Assert.False(File.Exists(_arquivoConfig));
            Assert.False(File.Exists(_arquivoSessao));
        }

        [Theory]
        [InlineData("nao e json")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("{\"Versao\":2,\"Salt\":\"AAAA\",\"Iteracoes\":1000,\"Verificador\":\"invalido\"}")]
        [InlineData("{\"Salt\":\"!invalido\",\"Iteracoes\":1000,\"Verificador\":\"invalido\"}")]
        [InlineData("{\"Salt\":\"AAAA\",\"Iteracoes\":1000,\"Verificador\":\"invalido\"}")]
        [InlineData("{\"Salt\":\"AAAAAAAAAAAAAAAAAAAAAA==\",\"Iteracoes\":0,\"Verificador\":\"invalido\"}")]
        public void Cofre_corrompido_falha_fechado_sem_alterar_arquivo(string json)
        {
            File.WriteAllText(_arquivoConfig, json);

            Assert.False(_cofre.ValidarSenha(Senha));

            Assert.Equal(json, File.ReadAllText(_arquivoConfig));
            Assert.False(File.Exists(_arquivoSessao));
        }

        [Fact]
        public void Sessao_existente_nao_substitui_a_senha_e_permanece_intacta()
        {
            CriarVerificador();
            // Caminho único derivado do diretório temporário: nunca é a sessão real.
            var sentinela = new byte[] { 7, 19, 23, 41 };
            File.WriteAllBytes(_arquivoSessao, sentinela);
            var dataOriginal = File.GetLastWriteTimeUtc(_arquivoSessao);

            Assert.False(_cofre.ValidarSenha("incorreta"));
            Assert.True(_cofre.ValidarSenha(Senha));

            Assert.Equal(sentinela, File.ReadAllBytes(_arquivoSessao));
            Assert.Equal(dataOriginal, File.GetLastWriteTimeUtc(_arquivoSessao));
            Assert.False(_cofre.Destrancado);
            Assert.Null(_cofre.ExpiraEmUtc);
        }

        [Fact]
        public void Validar_nao_renova_nem_tranca_uma_sessao_aberta()
        {
            if (!OperatingSystem.IsWindows()) return;
            CriarVerificador();
            Assert.True(_cofre.Destrancar(Senha));
            var expiraEm = _cofre.ExpiraEmUtc;
            var sessao = File.ReadAllBytes(_arquivoSessao);
            var documento = _cofre.Cifrar("documento de teste");

            Assert.False(_cofre.ValidarSenha("incorreta"));
            Assert.True(_cofre.ValidarSenha(Senha));

            Assert.True(_cofre.Destrancado);
            Assert.Equal(expiraEm, _cofre.ExpiraEmUtc);
            Assert.Equal(sessao, File.ReadAllBytes(_arquivoSessao));
            Assert.Equal("documento de teste", _cofre.Decifrar(documento));
        }

        private void CriarVerificador()
        {
            var salt = CryptoCofre.GerarAleatorio(CryptoCofre.TamanhoSalt);
            var chave = CryptoCofre.DerivarChave(Senha, salt, 1000);
            try
            {
                File.WriteAllText(_arquivoConfig, JsonConvert.SerializeObject(new
                {
                    Versao = 1,
                    Salt = Convert.ToBase64String(salt),
                    Iteracoes = 1000,
                    Verificador = CryptoCofre.Cifrar("memo-cofre-ok", chave)
                }));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(chave);
            }
        }

        public void Dispose()
        {
            if (File.Exists(_arquivoSessao)) File.Delete(_arquivoSessao);
            if (Directory.Exists(_diretorio)) Directory.Delete(_diretorio, recursive: true);
        }
    }
}
