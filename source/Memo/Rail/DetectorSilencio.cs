using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Memo.Service.Rail;

namespace Memo.Rail
{
    /// <summary>
    /// Decide se o Rail está num "momento de silêncio" (não perturbe): app em tela
    /// cheia / modo apresentação, ou algum app configurado aberto. Lê estado do
    /// Windows em memória; nada é gravado.
    /// </summary>
    internal static class DetectorSilencio
    {
        public static bool EmSilencio(RailConfig cfg)
        {
            if (cfg == null) return false;
            if (cfg.PausarEmTelaCheia && TelaCheiaOuApresentacao()) return true;
            if (AlgumAppAberto(cfg.AppsQuePausam)) return true;
            return false;
        }

        /// <summary>
        /// True quando há um app em tela cheia (jogo D3D), o Windows está em modo
        /// apresentação (projetando) ou "ocupado" — os estados em que o próprio
        /// Windows suprime notificações.
        /// </summary>
        private static bool TelaCheiaOuApresentacao()
        {
            try
            {
                if (SHQueryUserNotificationState(out var estado) != 0) return false;
                return estado == QUNS_BUSY
                    || estado == QUNS_RUNNING_D3D_FULL_SCREEN
                    || estado == QUNS_PRESENTATION_MODE;
            }
            catch
            {
                return false;
            }
        }

        // Enumerar processos é mais pesado que o resto; checa no máximo a cada ~2s.
        private static DateTime _proximaChecagemApps = DateTime.MinValue;
        private static bool _appsAbertoCache;

        private static bool AlgumAppAberto(List<string> termos)
        {
            if (termos == null || termos.Count == 0) return false;

            var agora = DateTime.UtcNow;
            if (agora >= _proximaChecagemApps)
            {
                _appsAbertoCache = RailService.AlgumAppAberto(termos);
                _proximaChecagemApps = agora.AddSeconds(2);
            }
            return _appsAbertoCache;
        }

        // ----------------- Win32 -----------------

        private const int QUNS_BUSY = 2;
        private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        private const int QUNS_PRESENTATION_MODE = 4;

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);
    }
}
