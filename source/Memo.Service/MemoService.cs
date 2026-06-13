using System;
using System.Collections.Generic;
using System.Linq;
using Memo.Service.Classes;
using Memo.Service.Repositorio;
using Memo.Service.Seguranca;
using TextCopy;

namespace Memo.Services
{
    public class MemoService
    {
        public static string DiretorioPadrao =>
            @"C:\Users\natha\OneDrive\Atalhos\memoApplication\documents";

        private readonly Cofre _cofre;
        private readonly DocumentoRepository _repositorio;
        private readonly Clipboard _clipboard = new Clipboard();

        public MemoService() : this(DiretorioPadrao)
        {
        }

        public MemoService(string diretorio)
        {
            _cofre = new Cofre(diretorio);
            _repositorio = new DocumentoRepository(_cofre, diretorio);
        }

        public Cofre Cofre => _cofre;

        public List<Documento> GetDocumentos() => _repositorio.CarregarTodos();

        public Documento Carregar(string key) => _repositorio.Carregar(key);

        public void Salvar(Documento documento) => _repositorio.Salvar(documento);

        public void Deletar(Documento documento) => _repositorio.Deletar(documento.Key);

        /// <summary>Recifra todos os documentos no formato novo e põe em quarentena o que não abrir.</summary>
        public MigracaoResultado Migrar() => _repositorio.Migrar();

        public void CopiarValor(Documento documento)
        {
            _clipboard.SetText(documento.Value ?? string.Empty);
        }

        // ----------------- Modo linha de comando -----------------

        public ResultadoCli ProcessarGet(string[] args)
        {
            var key = string.Join(" ", args.Skip(1)).Trim();
            var documento = _repositorio.Carregar(key);

            if (documento == null)
                return ResultadoCli.Falha($"\"{key}\" não encontrado");

            CopiarValor(documento);
            return ResultadoCli.Ok($"\"{key}\" copiado para a área de transferência");
        }

        public ResultadoCli ProcessarSet(string[] args)
        {
            // Aceita "memo set <chave> = <valor>" ou "memo set <chave>".
            var resto = string.Join(" ", args.Skip(1));
            var idx = resto.IndexOf('=');

            string key, value;
            if (idx >= 0)
            {
                key = resto.Substring(0, idx).Trim();
                value = resto.Substring(idx + 1).Trim();
            }
            else
            {
                key = resto.Trim();
                value = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(key))
                return ResultadoCli.Falha("Informe a chave do documento");

            _repositorio.Salvar(new Documento(key, value));
            return ResultadoCli.Ok($"\"{key}\" salvo");
        }
    }

    public class ResultadoCli
    {
        public bool Sucesso { get; private set; }
        public string Mensagem { get; private set; }

        public static ResultadoCli Ok(string mensagem) =>
            new ResultadoCli { Sucesso = true, Mensagem = mensagem };

        public static ResultadoCli Falha(string mensagem) =>
            new ResultadoCli { Sucesso = false, Mensagem = mensagem };
    }
}
