using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Service.Seguranca;
using Memo.Services;

namespace Memo.Service.Auth
{
    /// <summary>Snapshot obtido do servidor ao abrir a segunda tela.</summary>
    public sealed class ChaveAuthEditavel
    {
        public string Id { get; }
        public string UriOriginal { get; }
        public long AtualizadoEm { get; }
        public EntradaAuth Entrada => EdicaoAuth.Copiar(_entrada);
        private readonly EntradaAuth _entrada;

        public ChaveAuthEditavel(EntidadeAuth entidade, EntradaAuth entrada)
        {
            Id = entidade.Id;
            UriOriginal = entidade.UriOtpauth;
            AtualizadoEm = entidade.AtualizadoEm;
            _entrada = EdicaoAuth.Copiar(entrada);
            _entrada.IdEntidade = Id;
        }
    }

    /// <summary>Gerencia chaves no Ente; a exclusão exige senha digitada em cada operação.</summary>
    public sealed class GerenciadorAuthService
    {
        private readonly IEnteAuthApi _api;
        private readonly Func<SessaoEnteCli> _abrirSessao;
        private readonly Func<string, bool> _validarSenha;
        private readonly Action<List<EntradaAuth>> _salvarCache;

        public GerenciadorAuthService() : this(new EnteAuthApi(), SessaoEnteCli.Carregar,
            ValidarSenhaCofre, entradas => new CacheAuth { Entradas = entradas }.Salvar()) { }

        public GerenciadorAuthService(IEnteAuthApi api, Func<SessaoEnteCli> abrirSessao,
            Func<string, bool> validarSenha, Action<List<EntradaAuth>> salvarCache)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _abrirSessao = abrirSessao ?? throw new ArgumentNullException(nameof(abrirSessao));
            _validarSenha = validarSenha ?? throw new ArgumentNullException(nameof(validarSenha));
            _salvarCache = salvarCache ?? throw new ArgumentNullException(nameof(salvarCache));
        }

        private static bool ValidarSenhaCofre(string senha)
        {
            var diretorio = MemoService.DiretorioConfigurado;
            return !string.IsNullOrWhiteSpace(diretorio) && new Cofre(diretorio).ValidarSenha(senha);
        }

        public static List<EntradaAuth> EntradasVisiveis(IEnumerable<EntidadeAuth> entidades)
        {
            var entradas = new List<EntradaAuth>();
            foreach (var entidade in entidades)
            {
                if (!ParserOtpAuth.Tentar(entidade.UriOtpauth, out var entrada)) continue;
                entrada.IdEntidade = entidade.Id;
                entradas.Add(entrada);
            }
            return entradas;
        }

        public ChaveAuthEditavel Preparar(EntradaAuth selecionada)
        {
            if (selecionada == null) throw new InvalidOperationException("Selecione uma chave.");
            var sessao = _abrirSessao();
            try
            {
                var entidades = _api.Listar(sessao);
                var candidatas = entidades.Where(e => ParserOtpAuth.Tentar(e.UriOtpauth, out var entrada)
                    && (string.IsNullOrEmpty(selecionada.IdEntidade)
                        ? EdicaoAuth.MesmaEntrada(entrada, selecionada)
                        : e.Id == selecionada.IdEntidade)).ToList();
                if (candidatas.Count != 1)
                    throw new InvalidOperationException(candidatas.Count == 0
                        ? "A chave mudou ou não existe mais no Ente. Atualize a lista e tente novamente."
                        : "Há chaves idênticas no Ente. A operação foi bloqueada para não alterar a chave errada.");
                ParserOtpAuth.Tentar(candidatas[0].UriOtpauth, out var atual);
                return new ChaveAuthEditavel(candidatas[0], atual);
            }
            finally { sessao?.Limpar(); }
        }

        public ResultadoAuth Salvar(ChaveAuthEditavel original, EntradaAuth nova, bool somenteRenomear)
        {
            try
            {
                if (original == null) return ResultadoAuth.Falha("Abra a chave novamente.");
                if (nova == null) return ResultadoAuth.Falha("Informe os dados da chave.");
                var alterada = somenteRenomear ? original.Entrada : EdicaoAuth.Copiar(nova);
                alterada.Issuer = nova.Issuer;
                alterada.Account = nova.Account;
                var uri = EdicaoAuth.MontarUri(original.UriOriginal, alterada);
                return Executar(original, uri, remover: false);
            }
            catch (Exception) { return ResultadoAuth.Falha("Não foi possível salvar. Verifique os dados da chave e tente novamente."); }
        }

        public ResultadoAuth Remover(ChaveAuthEditavel original, string senha)
        {
            try
            {
                if (original == null) return ResultadoAuth.Falha("Abra a chave novamente.");
                // Não usa a sessão do cofre e não a renova. Sem senha válida, nem consulta o Ente.
                if (string.IsNullOrEmpty(senha) || !_validarSenha(senha))
                    return ResultadoAuth.Falha("Senha-mestra incorreta ou cofre indisponível. A chave não foi removida.");
                return Executar(original, null, remover: true);
            }
            catch (Exception) { return ResultadoAuth.Falha("Não foi possível remover a chave. Atualize a lista antes de tentar novamente."); }
        }

        private ResultadoAuth Executar(ChaveAuthEditavel original, string uri, bool remover)
        {
            var sessao = _abrirSessao();
            try
            {
                var entidades = _api.Listar(sessao);
                var atual = entidades.SingleOrDefault(e => e.Id == original.Id);
                if (atual == null || atual.UriOtpauth != original.UriOriginal || atual.AtualizadoEm != original.AtualizadoEm)
                    return ResultadoAuth.Falha("A chave mudou no Ente. Feche esta tela, atualize a lista e tente novamente.");
                if (remover) _api.Remover(sessao, original.Id);
                else _api.Atualizar(sessao, original.Id, uri);

                // Só atualiza o cache após o servidor aceitar a alteração.
                var restantes = new List<EntidadeAuth>();
                foreach (var entidade in entidades)
                {
                    if (remover && entidade.Id == original.Id) continue;
                    restantes.Add(new EntidadeAuth { Id = entidade.Id,
                        UriOtpauth = entidade.Id == original.Id ? uri : entidade.UriOtpauth,
                        AtualizadoEm = entidade.AtualizadoEm });
                }
                var entradas = EntradasVisiveis(restantes);
                var mensagem = remover ? "Chave removida do Ente." : "Chave atualizada no Ente.";
                try { _salvarCache(entradas); }
                catch { mensagem += " Não consegui gravar o cache local. Use Atualizar para sincronizar."; }
                return ResultadoAuth.Ok(mensagem, entradas);
            }
            finally { sessao?.Limpar(); }
        }
    }
}
