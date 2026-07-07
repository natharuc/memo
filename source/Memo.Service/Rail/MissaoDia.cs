using System;
using System.Collections.Generic;
using System.Linq;

namespace Memo.Service.Rail
{
    /// <summary>Uma tarefa da missão. Não é segredo — não passa pelo cofre.</summary>
    public class ItemMissao
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Texto com formatação leve: **negrito**, *itálico* e quebras de linha.</summary>
        public string Texto { get; set; }

        public bool Concluido { get; set; }
        public DateTime? ConcluidoEm { get; set; }

        /// <summary>
        /// Ação da tarefa (opcional): um link que ajuda a executá-la — conversa do
        /// WhatsApp, ticket, documento etc. Aberto pelo botão 🔗 e pelo cerebrinho.
        /// </summary>
        public string Link { get; set; }

        /// <summary>Dia para o qual a tarefa foi lançada (yyyy-MM-dd).</summary>
        public string Data { get; set; }

        /// <summary>Pendente com data anterior a hoje.</summary>
        public bool Atrasada(string hoje) =>
            !Concluido && string.Compare(Data, hoje, StringComparison.Ordinal) < 0;
    }

    /// <summary>
    /// Formato v2 do rail.json: um pool único de tarefas com data. Pendências de
    /// dias anteriores continuam na missão como atrasadas até serem concluídas.
    /// </summary>
    public class RailDados
    {
        public int Versao { get; set; } = 2;

        /// <summary>Último check-in mostrado/respondido (controla o intervalo).</summary>
        public DateTime? UltimoCheckIn { get; set; }

        public List<ItemMissao> Itens { get; set; } = new List<ItemMissao>();
    }

    /// <summary>
    /// A "missão visível": atrasadas + tarefas de hoje (+ futuras, para listagem).
    /// Montada por <see cref="RailService.MissaoVisivel"/> — a numeração exibida
    /// na UI e na CLI segue exatamente <see cref="Lista"/>.
    /// </summary>
    public class MissaoVisivel
    {
        public List<ItemMissao> Atrasadas { get; set; } = new List<ItemMissao>();
        public List<ItemMissao> DeHoje { get; set; } = new List<ItemMissao>();
        public List<ItemMissao> Futuras { get; set; } = new List<ItemMissao>();

        /// <summary>Ordem canônica: atrasadas → hoje → futuras.</summary>
        public List<ItemMissao> Lista =>
            Atrasadas.Concat(DeHoje).Concat(Futuras).ToList();

        /// <summary>Tarefas que contam para o dia (atrasadas + hoje).</summary>
        public List<ItemMissao> Ativas => Atrasadas.Concat(DeHoje).ToList();

        public int Pendentes => Ativas.Count(i => !i.Concluido);
        public int Concluidos => Ativas.Count(i => i.Concluido);

        /// <summary>Tarefa "atual": primeira pendente (atrasada tem prioridade).</summary>
        public ItemMissao ProximaPendente() => Ativas.FirstOrDefault(i => !i.Concluido);
    }
}
