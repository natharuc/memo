using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Memo.Service.Lembretes
{
    /// <summary>
    /// Persistência simples de lembretes em <c>%LOCALAPPDATA%\Memo\lembretes.json</c>.
    /// Não é segredo (não passa pelo cofre). Recarrega do disco a cada operação,
    /// então mudanças feitas por outro processo (ex.: CLI) são vistas no próximo poll.
    /// </summary>
    public class LembreteService
    {
        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "lembretes.json");

        private static readonly object Trava = new object();

        public List<Lembrete> Listar()
        {
            lock (Trava)
            {
                try
                {
                    if (File.Exists(Caminho))
                        return JsonConvert.DeserializeObject<List<Lembrete>>(File.ReadAllText(Caminho))
                               ?? new List<Lembrete>();
                }
                catch
                {
                    // arquivo corrompido: começa do zero em vez de quebrar.
                }
                return new List<Lembrete>();
            }
        }

        private void Gravar(List<Lembrete> lista)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
            File.WriteAllText(Caminho, JsonConvert.SerializeObject(lista, Formatting.Indented));
        }

        public Lembrete Adicionar(string texto, DateTime proximo, int repetirMinutos = 0)
        {
            lock (Trava)
            {
                var lista = Listar();
                var lembrete = new Lembrete
                {
                    Texto = texto?.Trim(),
                    Proximo = proximo,
                    RepetirMinutos = Math.Max(0, repetirMinutos)
                };
                lista.Add(lembrete);
                Gravar(lista);
                return lembrete;
            }
        }

        public void Remover(string id)
        {
            lock (Trava)
            {
                var lista = Listar();
                lista.RemoveAll(x => x.Id == id);
                Gravar(lista);
            }
        }

        /// <summary>Concluir: se recorrente, agenda a próxima; se único, sai da lista.</summary>
        public void Concluir(string id)
        {
            lock (Trava)
            {
                var lista = Listar();
                var lembrete = lista.FirstOrDefault(x => x.Id == id);
                if (lembrete == null) return;

                if (lembrete.RepetirMinutos > 0)
                    lembrete.Proximo = DateTime.Now.AddMinutes(lembrete.RepetirMinutos);
                else
                    lista.RemoveAll(x => x.Id == id);

                Gravar(lista);
            }
        }

        /// <summary>Adiar (soneca): joga o próximo disparo para daqui a N minutos.</summary>
        public void Adiar(string id, int minutos)
        {
            lock (Trava)
            {
                var lista = Listar();
                var lembrete = lista.FirstOrDefault(x => x.Id == id);
                if (lembrete == null) return;

                lembrete.Proximo = DateTime.Now.AddMinutes(minutos);
                Gravar(lista);
            }
        }

        /// <summary>Lembretes cujo horário já chegou (inclui atrasados).</summary>
        public List<Lembrete> Devidos(DateTime agora)
        {
            return Listar()
                .Where(l => !l.Concluido && l.Proximo <= agora)
                .OrderBy(l => l.Proximo)
                .ToList();
        }
    }
}
