using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Memo.Service.Classes;
using Memo.Service.Seguranca;
using Newtonsoft.Json;

namespace Memo.Service.Repositorio
{
    /// <summary>
    /// Persistência dos documentos: um arquivo cifrado por chave, no diretório do
    /// cofre. Sanitiza a chave (evita path traversal), tolera arquivos que não
    /// abrem (failover) e migra o formato antigo para o novo.
    /// </summary>
    public class DocumentoRepository
    {
        private const string ArquivoConfig = "vault.json";
        private const string PastaFalhas = "falhas";

        private readonly Cofre _cofre;
        private readonly string _diretorio;
        private readonly string _diretorioFalhas;

        public DocumentoRepository(Cofre cofre, string diretorio)
        {
            _cofre = cofre;
            _diretorio = diretorio;
            _diretorioFalhas = Path.Combine(diretorio, PastaFalhas);
            Directory.CreateDirectory(_diretorio);
        }

        public static string SanitizarChave(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Chave inválida.");

            var limpa = key.Trim();
            // Bloqueia separadores de caminho e nomes como "..".
            if (Path.GetFileName(limpa) != limpa)
                throw new ArgumentException("Chave inválida: não pode conter caminho.");

            return limpa;
        }

        private string CaminhoDe(string key) => Path.Combine(_diretorio, SanitizarChave(key));

        private IEnumerable<string> ArquivosDocumentos()
        {
            // Apenas arquivos do diretório raiz (a subpasta "falhas" fica de fora),
            // ignorando o vault.json.
            return Directory.GetFiles(_diretorio)
                .Where(a => !string.Equals(Path.GetFileName(a), ArquivoConfig, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Carrega todos os documentos legíveis. Arquivos com problema são ignorados.</summary>
        public List<Documento> CarregarTodos()
        {
            var lista = new List<Documento>();

            foreach (var arquivo in ArquivosDocumentos())
            {
                if (TentarCarregar(arquivo, out var doc, out _))
                    lista.Add(doc);
            }

            return lista.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public Documento Carregar(string key)
        {
            var caminho = CaminhoDe(key);
            return TentarCarregar(caminho, out var doc, out _) ? doc : null;
        }

        private bool TentarCarregar(string caminho, out Documento documento, out bool eraLegado)
        {
            documento = null;
            eraLegado = false;

            try
            {
                if (!File.Exists(caminho)) return false;

                var conteudo = File.ReadAllText(caminho);
                if (!_cofre.TentarDecifrar(conteudo, out var json, out eraLegado))
                    return false;

                documento = JsonConvert.DeserializeObject<Documento>(json);
                return documento != null && !string.IsNullOrWhiteSpace(documento.Key);
            }
            catch
            {
                return false;
            }
        }

        public void Salvar(Documento documento)
        {
            var caminho = CaminhoDe(documento.Key);
            var json = JsonConvert.SerializeObject(documento);
            File.WriteAllText(caminho, _cofre.Cifrar(json));
        }

        /// <summary>
        /// Exclusão DEFINITIVA: remove o arquivo de vez (não há lixeira). A
        /// confirmação na UI deixa explícito que a ação é irreversível.
        /// </summary>
        public void Deletar(string key)
        {
            var caminho = CaminhoDe(key);
            if (!File.Exists(caminho)) return;

            File.Delete(caminho);
        }

        /// <summary>
        /// Recifra todos os documentos no formato novo. O que não conseguir abrir
        /// (mesmo pelos esquemas legados) é movido para a pasta "falhas" — failover,
        /// para o app continuar funcionando sem perder o arquivo.
        /// </summary>
        public MigracaoResultado Migrar()
        {
            var resultado = new MigracaoResultado();

            foreach (var arquivo in ArquivosDocumentos().ToList())
            {
                var nome = Path.GetFileName(arquivo);

                if (TentarCarregar(arquivo, out var doc, out var eraLegado))
                {
                    if (eraLegado)
                    {
                        Salvar(doc); // reescreve no formato novo (chave-mestra atual)
                        resultado.Migrados.Add(doc.Key);
                    }
                    else
                    {
                        resultado.JaAtualizados++;
                    }
                }
                else
                {
                    Directory.CreateDirectory(_diretorioFalhas);
                    MoverSobrescrevendo(arquivo, Path.Combine(_diretorioFalhas, nome));
                    resultado.Falhas.Add(nome);
                }
            }

            resultado.PastaFalhas = _diretorioFalhas;
            return resultado;
        }

        private static void MoverSobrescrevendo(string origem, string destino)
        {
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(origem, destino);
        }
    }
}
