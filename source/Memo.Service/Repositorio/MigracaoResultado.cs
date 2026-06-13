using System.Collections.Generic;

namespace Memo.Service.Repositorio
{
    /// <summary>Resumo de uma passada de migração/recifragem dos documentos.</summary>
    public class MigracaoResultado
    {
        /// <summary>Chaves recifradas do formato antigo para o novo.</summary>
        public List<string> Migrados { get; } = new List<string>();

        /// <summary>Documentos que já estavam no formato novo.</summary>
        public int JaAtualizados { get; set; }

        /// <summary>Arquivos que não abriram e foram para a quarentena.</summary>
        public List<string> Falhas { get; } = new List<string>();

        /// <summary>Caminho da pasta de quarentena.</summary>
        public string PastaFalhas { get; set; }

        public bool HouveMudanca => Migrados.Count > 0 || Falhas.Count > 0;
    }
}
