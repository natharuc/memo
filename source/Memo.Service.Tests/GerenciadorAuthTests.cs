using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Service.Auth;
using Xunit;

namespace Memo.Service.Tests
{
    public class GerenciadorAuthTests
    {
        private const string UriPrincipal = "otpauth://totp/Servico:conta%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=Servico&algorithm=SHA256&digits=8&period=60&codeDisplay=pinned&custom=preservar%2Bisto";
        private const string UriOutraConta = "otpauth://totp/Servico:outra%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=Servico&algorithm=SHA256&digits=8&period=60";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("incorreta")]
        public void Remover_exige_senha_mesmo_com_chave_preparada_e_nao_consulta_ente_sem_autorizar(string senha)
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            contexto.Api.ZerarContadores();
            contexto.SessoesAbertas = 0;

            var resultado = contexto.Service.Remover(original, senha);

            Assert.False(resultado.Sucesso);
            Assert.Equal(0, contexto.Api.Listagens);
            Assert.Empty(contexto.Api.Removidos);
            Assert.Empty(contexto.Api.Atualizacoes);
            Assert.Equal(0, contexto.SessoesAbertas);
            Assert.Equal(0, contexto.GravacoesCache);
            Assert.Single(contexto.Api.Entidades);
        }

        [Fact]
        public void Remover_pede_validacao_de_senha_em_cada_operacao()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();

            Assert.True(contexto.Service.Remover(original, "correta").Sucesso);
            contexto.Api.ZerarContadores();

            Assert.False(contexto.Service.Remover(original, "incorreta").Sucesso);
            Assert.Equal(new[] { "correta", "incorreta" }, contexto.SenhasValidadas);
            Assert.Empty(contexto.Api.Removidos);
            Assert.Equal(0, contexto.Api.Listagens);
        }

        [Fact]
        public void Remover_somente_o_id_selecionado_e_preserva_outra_conta_com_mesmo_segredo()
        {
            var contexto = new Contexto();
            contexto.Api.Entidades.Add(Entidade("outra", UriOutraConta));
            var original = contexto.Preparar();

            var resultado = contexto.Service.Remover(original, "correta");

            Assert.True(resultado.Sucesso);
            Assert.Equal(new[] { "principal" }, contexto.Api.Removidos);
            Assert.Equal("outra", Assert.Single(contexto.Api.Entidades).Id);
            Assert.Equal("outra", Assert.Single(contexto.Cache).IdEntidade);
            Assert.Equal("outra@example.com", Assert.Single(resultado.Entradas).Account);
            Assert.Equal(1, contexto.GravacoesCache);
        }

        [Fact]
        public void Preparar_sem_id_rejeita_duas_chaves_identicas()
        {
            var contexto = new Contexto();
            contexto.Api.Entidades.Add(Entidade("duplicada", UriPrincipal));

            Assert.Throws<InvalidOperationException>(() => contexto.Preparar());

            Assert.Empty(contexto.Api.Removidos);
            Assert.Empty(contexto.Api.Atualizacoes);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Id_conhecido_distingue_chaves_identicas()
        {
            var contexto = new Contexto();
            contexto.Api.Entidades.Add(Entidade("duplicada", UriPrincipal));
            var selecionada = Entrada(UriPrincipal);
            selecionada.IdEntidade = "duplicada";
            var original = contexto.Service.Preparar(selecionada);

            Assert.True(contexto.Service.Remover(original, "correta").Sucesso);

            Assert.Equal(new[] { "duplicada" }, contexto.Api.Removidos);
            Assert.Equal("principal", Assert.Single(contexto.Api.Entidades).Id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Mudanca_remota_de_uri_ou_data_bloqueia_exclusao_sem_alterar_cache(bool mudarUri)
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            if (mudarUri) contexto.Api.Entidades[0].UriOtpauth = UriOutraConta;
            else contexto.Api.Entidades[0].AtualizadoEm++;

            var resultado = contexto.Service.Remover(original, "correta");

            Assert.False(resultado.Sucesso);
            Assert.Empty(contexto.Api.Removidos);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Mudanca_remota_bloqueia_edicao()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            contexto.Api.Entidades[0].AtualizadoEm++;
            var nova = original.Entrada;
            nova.Account = "nova@example.com";

            var resultado = contexto.Service.Salvar(original, nova, somenteRenomear: false);

            Assert.False(resultado.Sucesso);
            Assert.Empty(contexto.Api.Atualizacoes);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Falha_remota_de_exclusao_nao_reescreve_cache()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            contexto.Api.FalharRemocao = true;

            var resultado = contexto.Service.Remover(original, "correta");

            Assert.False(resultado.Sucesso);
            Assert.Single(contexto.Api.Entidades);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Falha_remota_de_edicao_nao_reescreve_cache()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            contexto.Api.FalharAtualizacao = true;
            var nova = original.Entrada;
            nova.Issuer = "Novo nome";

            var resultado = contexto.Service.Salvar(original, nova, somenteRenomear: true);

            Assert.False(resultado.Sucesso);
            Assert.Equal(UriPrincipal, contexto.Api.Entidades[0].UriOtpauth);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Renomear_preserva_segredo_parametros_e_metadados_desconhecidos()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            var nova = new EntradaAuth
            {
                Issuer = "Novo serviço",
                Account = "nova@example.com",
                Secret = "NAO DEVE SER USADO",
                Digitos = 1,
                Periodo = -1,
                Algoritmo = "invalido"
            };

            var resultado = contexto.Service.Salvar(original, nova, somenteRenomear: true);

            Assert.True(resultado.Sucesso);
            var gravada = Assert.Single(contexto.Api.Atualizacoes);
            Assert.Equal("principal", gravada.Id);
            Assert.Contains("codeDisplay=pinned", gravada.Uri);
            Assert.Contains("custom=preservar%2Bisto", gravada.Uri);
            var entrada = Entrada(gravada.Uri);
            Assert.Equal("Novo serviço", entrada.Issuer);
            Assert.Equal("nova@example.com", entrada.Account);
            Assert.Equal("JBSWY3DPEHPK3PXP", entrada.Secret);
            Assert.Equal("SHA256", entrada.Algoritmo);
            Assert.Equal(8, entrada.Digitos);
            Assert.Equal(60, entrada.Periodo);
            Assert.Equal("principal", Assert.Single(contexto.Cache).IdEntidade);
            Assert.Equal(1, contexto.GravacoesCache);
        }

        [Fact]
        public void Editar_valida_os_dados_antes_de_consultar_ente()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            contexto.Api.ZerarContadores();
            var nova = original.Entrada;
            nova.Secret = "segredo invalido!";

            var resultado = contexto.Service.Salvar(original, nova, somenteRenomear: false);

            Assert.False(resultado.Sucesso);
            Assert.Equal(0, contexto.Api.Listagens);
            Assert.Empty(contexto.Api.Atualizacoes);
            Assert.Equal(0, contexto.GravacoesCache);
        }

        [Fact]
        public void Snapshot_nao_muda_ao_alterar_a_copia_de_entrada()
        {
            var contexto = new Contexto();
            var original = contexto.Preparar();
            var copia = original.Entrada;
            copia.Secret = "OUTROSEGREDO";

            Assert.Equal("JBSWY3DPEHPK3PXP", original.Entrada.Secret);
            Assert.Equal(UriPrincipal, original.UriOriginal);
        }

        [Fact]
        public void Entradas_visiveis_preservam_ids_e_duplicatas_identicas()
        {
            var entidades = new[]
            {
                Entidade("principal", UriPrincipal),
                Entidade("duplicada", UriPrincipal),
                Entidade("outra", UriOutraConta)
            };

            var entradas = GerenciadorAuthService.EntradasVisiveis(entidades);

            Assert.Equal(new[] { "principal", "duplicada", "outra" }, entradas.Select(e => e.IdEntidade));
            Assert.Equal(3, entradas.Count);
            Assert.True(EdicaoAuth.MesmaEntrada(entradas[0], entradas[1]));
            Assert.False(EdicaoAuth.MesmaEntrada(entradas[0], entradas[2]));
        }

        [Fact]
        public void Entradas_visiveis_ignoram_uris_que_nao_sao_totp()
        {
            var entidades = new[]
            {
                Entidade("totp", UriPrincipal),
                Entidade("hotp", "otpauth://hotp/Servico:conta?secret=JBSWY3DPEHPK3PXP&counter=1"),
                Entidade("sem-segredo", "otpauth://totp/Servico:conta?issuer=Servico"),
                Entidade("url", "https://example.com"),
                Entidade("invalida", "nao e uri")
            };

            var entrada = Assert.Single(GerenciadorAuthService.EntradasVisiveis(entidades));

            Assert.Equal("totp", entrada.IdEntidade);
            Assert.Equal("Servico", entrada.Issuer);
            Assert.Equal("conta@example.com", entrada.Account);
        }

        private static EntidadeAuth Entidade(string id, string uri) =>
            new EntidadeAuth { Id = id, UriOtpauth = uri, AtualizadoEm = 123456789 };

        private static EntradaAuth Entrada(string uri)
        {
            Assert.True(ParserOtpAuth.Tentar(uri, out var entrada));
            return entrada;
        }

        private sealed class Contexto
        {
            public ApiFalsa Api { get; } = new ApiFalsa();
            public GerenciadorAuthService Service { get; }
            public List<EntradaAuth> Cache { get; private set; }
            public int GravacoesCache { get; private set; }
            public int SessoesAbertas { get; set; }
            public List<string> SenhasValidadas { get; } = new List<string>();

            public Contexto()
            {
                Api.Entidades.Add(Entidade("principal", UriPrincipal));
                Service = new GerenciadorAuthService(Api,
                    () => { SessoesAbertas++; return null; },
                    senha => { SenhasValidadas.Add(senha); return senha == "correta"; },
                    entradas => { GravacoesCache++; Cache = entradas; });
            }

            public ChaveAuthEditavel Preparar() => Service.Preparar(Entrada(UriPrincipal));
        }

        private sealed class ApiFalsa : IEnteAuthApi
        {
            public List<EntidadeAuth> Entidades { get; } = new List<EntidadeAuth>();
            public int Listagens { get; private set; }
            public List<string> Removidos { get; } = new List<string>();
            public List<(string Id, string Uri)> Atualizacoes { get; } = new List<(string Id, string Uri)>();
            public bool FalharRemocao { get; set; }
            public bool FalharAtualizacao { get; set; }

            public List<EntidadeAuth> Listar(SessaoEnteCli sessao)
            {
                Listagens++;
                return Entidades.Select(e => Entidade(e.Id, e.UriOtpauth)).Select((e, i) =>
                {
                    e.AtualizadoEm = Entidades[i].AtualizadoEm;
                    return e;
                }).ToList();
            }

            public void Enviar(SessaoEnteCli sessao, string uriOtpauth) => throw new NotSupportedException();

            public void Atualizar(SessaoEnteCli sessao, string id, string uriOtpauth)
            {
                if (FalharAtualizacao) throw new InvalidOperationException("falha remota simulada");
                Atualizacoes.Add((id, uriOtpauth));
                Entidades.Single(e => e.Id == id).UriOtpauth = uriOtpauth;
            }

            public void Remover(SessaoEnteCli sessao, string id)
            {
                if (FalharRemocao) throw new InvalidOperationException("falha remota simulada");
                Removidos.Add(id);
                Entidades.RemoveAll(e => e.Id == id);
            }

            public void ZerarContadores()
            {
                Listagens = 0;
                Removidos.Clear();
                Atualizacoes.Clear();
            }
        }
    }
}
