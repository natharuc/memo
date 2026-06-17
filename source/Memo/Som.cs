using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text;

namespace Memo
{
    /// <summary>
    /// Toca um "chime" curto e agradável, sintetizado em memória (sem precisar de
    /// arquivo .wav): um arpejo C–E–G–C com decaimento suave, tipo sino.
    /// </summary>
    public static class Som
    {
        public static void TocarLembrete()
        {
            try
            {
                var wav = GerarChime();
                var player = new SoundPlayer(new MemoryStream(wav));
                player.Play(); // assíncrono; nunca bloqueia a UI
            }
            catch
            {
                // Som é enfeite — jamais pode quebrar o app.
            }
        }

        private static byte[] GerarChime()
        {
            const int sr = 44100;                                   // taxa de amostragem
            double[] notas = { 523.25, 659.25, 783.99, 1046.50 };   // C5, E5, G5, C6
            const double durNota = 0.13;                            // duração de cada nota (s)
            const double cauda = 0.35;                              // decaimento extra da última

            var amostras = new List<short>();
            for (int n = 0; n < notas.Length; n++)
            {
                double f = notas[n];
                double dur = durNota + (n == notas.Length - 1 ? cauda : 0);
                int total = (int)(sr * dur);

                for (int i = 0; i < total; i++)
                {
                    double t = i / (double)sr;
                    double env = Math.Exp(-4.5 * t);          // decaimento (sino)
                    if (t < 0.005) env *= t / 0.005;          // ataque rápido (evita "clique")

                    double onda = Math.Sin(2 * Math.PI * f * t)
                                + 0.25 * Math.Sin(2 * Math.PI * 2 * f * t); // 2º harmônico
                    double s = 0.38 * env * onda;
                    if (s > 1) s = 1; else if (s < -1) s = -1;
                    amostras.Add((short)(s * short.MaxValue));
                }
            }

            int dataLen = amostras.Count * 2;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataLen);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);            // tamanho do bloco fmt
                bw.Write((short)1);      // PCM
                bw.Write((short)1);      // mono
                bw.Write(sr);            // sample rate
                bw.Write(sr * 2);        // byte rate (sr * canais * bytes/amostra)
                bw.Write((short)2);      // block align
                bw.Write((short)16);     // bits por amostra
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(dataLen);
                foreach (var a in amostras) bw.Write(a);
                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}
