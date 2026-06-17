using System;
using Newtonsoft.Json;

namespace Memo.Service.Lembretes
{
    /// <summary>Um lembrete simples: um texto que deve aparecer numa hora.</summary>
    public class Lembrete
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Texto { get; set; }

        /// <summary>Horário local do próximo disparo.</summary>
        public DateTime Proximo { get; set; }

        /// <summary>0 = dispara uma vez; &gt; 0 = repete a cada N minutos.</summary>
        public int RepetirMinutos { get; set; }

        public bool Concluido { get; set; }

        /// <summary>Texto amigável para exibir na lista (data + recorrência).</summary>
        [JsonIgnore]
        public string Resumo => Proximo.ToString("dd/MM/yyyy HH:mm")
            + (RepetirMinutos > 0 ? $"  ·  repete a cada {RepetirMinutos} min" : "");
    }
}
