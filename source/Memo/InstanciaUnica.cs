using System;
using System.Threading;

namespace Memo
{
    /// <summary>
    /// Garante uma única instância do app de bandeja (senão teríamos dois
    /// agendadores e lembretes duplicados). Uma segunda execução sinaliza a
    /// primeira para abrir a janela, e então encerra.
    /// </summary>
    public class InstanciaUnica : IDisposable
    {
        private const string NomeMutex = "Memo.Tray.Mutex.v1";
        private const string NomeEvento = "Memo.Tray.Show.v1";

        private Mutex _mutex;
        private EventWaitHandle _evento;
        private Thread _ouvinte;
        private volatile bool _ativo = true;

        /// <summary>Disparado (em thread de fundo) quando outra instância pede para abrir.</summary>
        public event Action MostrarSolicitado;

        /// <summary>Tenta virar a instância principal. False = já existe outra.</summary>
        public bool Adquirir()
        {
            _mutex = new Mutex(true, NomeMutex, out var criadoNovo);
            _evento = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEvento);

            if (!criadoNovo) return false;

            _ouvinte = new Thread(Ouvir) { IsBackground = true };
            _ouvinte.Start();
            return true;
        }

        /// <summary>Pede para a instância principal trazer a janela à frente.</summary>
        public void SinalizarMostrar()
        {
            try { EventWaitHandle.OpenExisting(NomeEvento).Set(); }
            catch { /* nenhuma instância ouvindo */ }
        }

        private void Ouvir()
        {
            while (_ativo)
            {
                if (_evento.WaitOne(500))
                    MostrarSolicitado?.Invoke();
            }
        }

        public void Dispose()
        {
            _ativo = false;
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
            _evento?.Dispose();
        }
    }
}
