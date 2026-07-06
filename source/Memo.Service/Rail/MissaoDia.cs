using System;
using System.Collections.Generic;
using System.Linq;

namespace Memo.Service.Rail
{
    /// <summary>Um item do checklist da missão do dia.</summary>
    public class ItemMissao
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Texto { get; set; }
        public bool Concluido { get; set; }
        public DateTime? ConcluidoEm { get; set; }

        /// <summary>
        /// Ação da tarefa (opcional): um link que ajuda a executá-la — conversa do
        /// WhatsApp, ticket, documento etc. Aberto pelo botão 🔗 e pelo cerebrinho.
        /// </summary>
        public string Link { get; set; }
    }

    /// <summary>A missão (checklist) de um dia. Não é segredo — não passa pelo cofre.</summary>
    public class MissaoDia
    {
        /// <summary>Data no formato yyyy-MM-dd (chave do dia).</summary>
        public string Data { get; set; }

        public List<ItemMissao> Itens { get; set; } = new List<ItemMissao>();

        /// <summary>Último check-in mostrado/respondido (controla o intervalo).</summary>
        public DateTime? UltimoCheckIn { get; set; }

        public int Pendentes => Itens.Count(i => !i.Concluido);
        public int Concluidos => Itens.Count(i => i.Concluido);

        /// <summary>Primeira tarefa ainda não concluída (a "atual"), ou null.</summary>
        public ItemMissao ProximaPendente() => Itens.FirstOrDefault(i => !i.Concluido);
    }
}
