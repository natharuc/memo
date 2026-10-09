using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Services;

namespace Memo.Service.Auth
{
    /// <summary>
    /// TOTP do Ente Auth via Ente CLI + cache DPAPI. Não usa o cofre do Memo.
    /// </summary>
    public class EnteAuthService
    {
        private readonly EnteCliCliente _cli;

        public EnteAuthService() : this(new EnteCliCliente()) { }

        public EnteAuthService(EnteCliCliente cli)
        {
            _cli = cli ?? new EnteCliCliente();
        }

        public ResultadoAuth Cadastrar(IList<string> uris)
        {
            var lista = new List<EntradaAuth>();
            foreach (var bruta in uris ?? Array.Empty<string>())
            {
                if (!ParserOtpAuth.Tentar(bruta, out var entrada))
                    return ResultadoAuth.Falha("Não é um otpauth://totp válido.");
                lista.Add(entrada);
            }
            if (lista.Count == 0)
                return ResultadoAuth.Falha("Nenhuma chave para cadastrar.");

            var sessao = (SessaoEnteCli)null;
            try
            {
                sessao = SessaoEnteCli.Carregar();
                var api = new EnteAuthApi();
                foreach (var bruta in uris)
                    api.Enviar(sessao, bruta.Trim());
            }
            catch (Exception ex)
            {
                return ResultadoAuth.Falha(ex.Message);
            }
            finally
            {
                sessao?.Limpar();
            }

            try
            {
                var cache = SyncInterno();
                foreach (var entrada in lista)
                {
                    var achou = false;
                    foreach (var item in cache.Entradas)
                    {
                        if (string.Equals(item.Secret, entrada.Secret, StringComparison.OrdinalIgnoreCase))
                        {
                            achou = true;
                            break;
                        }
                    }
                    if (!achou)
                        return ResultadoAuth.Falha(
                            "Enviei a chave, mas o export da Ente não a devolveu. Confira no app Ente Auth.");
                }

                var msg = lista.Count == 1
                    ? "Chave enviada para o Ente"
                    : lista.Count + " chaves enviadas para o Ente";
                return ResultadoAuth.Ok(msg, cache.Entradas);
            }
            catch (Exception ex)
            {
                return ResultadoAuth.Falha(
                    "A chave pode ter sido enviada, mas não confirmei no export: " + ex.Message);
            }
        }

        public ResultadoAuth Sincronizar()
        {
            try
            {
                var cache = SyncInterno();
                return ResultadoAuth.Ok(
                    cache.Entradas.Count == 1
                        ? "1 código TOTP sincronizado"
                        : cache.Entradas.Count + " códigos TOTP sincronizados",
                    cache.Entradas);
            }
            catch (Exception ex)
            {
                return ResultadoAuth.Falha(ex.Message);
            }
        }

        public ResultadoAuth Listar(bool forcarSync)
        {
            var r = ObterCache(forcarSync);
            if (!r.Sucesso) return r;
            if (r.Entradas == null || r.Entradas.Count == 0)
                return ResultadoAuth.Falha("Nenhum código TOTP em cache. Rode: memo-cli auth sync");

            var linhas = r.Entradas.Select(e => e.Rotulo).ToList();
            return ResultadoAuth.Ok(string.Join(Environment.NewLine, linhas), r.Entradas);
        }

        public ResultadoAuth Buscar(IList<string> tokens, bool forcarSync)
        {
            var toks = (tokens ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();

            if (toks.Count == 0)
                return ResultadoAuth.Uso("Uso: memo auth <issuer> [conta]  |  list  |  sync");

            var r = ObterCache(forcarSync);
            if (!r.Sucesso) return r;

            var hits = CorrespondenciaAuth.Filtrar(r.Entradas, toks);
            if (hits.Count == 0)
                return ResultadoAuth.NaoEncontrado(
                    "Nenhum código TOTP para \"" + string.Join(" ", toks) + "\"",
                    r.Entradas);

            if (hits.Count > 1)
            {
                var nomes = string.Join("; ", hits.Select(e => e.Rotulo));
                return ResultadoAuth.Ambiguo(
                    "Vários códigos batem: " + nomes + ". Seja mais específico.",
                    hits);
            }

            var entrada = hits[0];
            if (!GeradorTotp.Tentar(entrada, out var codigo, out var segundos))
                return ResultadoAuth.Falha("Não consegui gerar o TOTP de " + entrada.Rotulo + ".");

            return ResultadoAuth.CodigoGerado(entrada, codigo, segundos);
        }

        private ResultadoAuth ObterCache(bool forcarSync)
        {
            var cache = CacheAuth.Carregar();
            if (forcarSync || cache.Vazio || cache.Vencido)
            {
                try
                {
                    cache = SyncInterno();
                }
                catch (Exception ex)
                {
                    if (forcarSync || cache.Vazio)
                        return ResultadoAuth.Falha(ex.Message);
                    // cache velho ainda serve para gerar o código desta vez
                }
            }
            return ResultadoAuth.Ok(null, cache.Entradas);
        }

        private CacheAuth SyncInterno()
        {
            var entradas = _cli.Exportar();
            var cache = new CacheAuth { Entradas = entradas };
            cache.Salvar();
            return cache;
        }
    }

    public class ResultadoAuth
    {
        public bool Sucesso { get; private set; }
        public bool EhNaoEncontrado { get; private set; }
        public bool EhAmbiguo { get; private set; }
        public bool UsoIncorreto { get; private set; }
        public string Mensagem { get; private set; }
        public string Codigo { get; private set; }
        public string Issuer { get; private set; }
        public string Account { get; private set; }
        public int SegundosRestantes { get; private set; }
        public List<EntradaAuth> Entradas { get; private set; }

        public ResultadoCli ComoCli() =>
            Sucesso ? ResultadoCli.Ok(Mensagem) : ResultadoCli.Falha(Mensagem);

        public static ResultadoAuth Ok(string mensagem, List<EntradaAuth> entradas = null) =>
            new ResultadoAuth { Sucesso = true, Mensagem = mensagem, Entradas = entradas ?? new List<EntradaAuth>() };

        public static ResultadoAuth Falha(string mensagem) =>
            new ResultadoAuth { Sucesso = false, Mensagem = mensagem, Entradas = new List<EntradaAuth>() };

        public static ResultadoAuth NaoEncontrado(string mensagem, List<EntradaAuth> entradas = null) =>
            new ResultadoAuth
            {
                Sucesso = false,
                EhNaoEncontrado = true,
                Mensagem = mensagem,
                Entradas = entradas ?? new List<EntradaAuth>()
            };

        public static ResultadoAuth Ambiguo(string mensagem, List<EntradaAuth> entradas) =>
            new ResultadoAuth
            {
                Sucesso = false,
                EhAmbiguo = true,
                Mensagem = mensagem,
                Entradas = entradas ?? new List<EntradaAuth>()
            };

        public static ResultadoAuth Uso(string mensagem) =>
            new ResultadoAuth { Sucesso = false, UsoIncorreto = true, Mensagem = mensagem };

        public static ResultadoAuth CodigoGerado(EntradaAuth entrada, string codigo, int segundos) =>
            new ResultadoAuth
            {
                Sucesso = true,
                Mensagem = entrada.Rotulo + " copiado (" + segundos + "s)",
                Codigo = codigo,
                Issuer = entrada.Issuer,
                Account = entrada.Account,
                SegundosRestantes = segundos,
                Entradas = new List<EntradaAuth> { entrada }
            };
    }
}
