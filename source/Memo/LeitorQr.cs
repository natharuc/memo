using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Memo.Service.Auth;
using ZXing;
using ZXing.Common;

namespace Memo
{
    /// <summary>Acha <c>otpauth://totp</c> numa imagem (QR) ou num texto.</summary>
    public static class LeitorQr
    {
        public static List<string> DeImagem(BitmapSource imagem)
        {
            if (imagem == null) return new List<string>();
            var convertido = new FormatConvertedBitmap(imagem, PixelFormats.Bgra32, null, 0);
            var w = convertido.PixelWidth;
            var h = convertido.PixelHeight;
            var stride = w * 4;
            var pixels = new byte[stride * h];
            convertido.CopyPixels(pixels, stride, 0);
            return DePixels(pixels, w, h, RGBLuminanceSource.BitmapFormat.BGRA32);
        }

        public static List<string> DePixels(byte[] pixels, int largura, int altura, RGBLuminanceSource.BitmapFormat formato)
        {
            var achados = new List<string>();
            if (pixels == null || largura <= 0 || altura <= 0) return achados;

            var fonte = new RGBLuminanceSource(pixels, largura, altura, formato);
            var leitor = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            Result[] varios = null;
            try { varios = leitor.DecodeMultiple(fonte); }
            catch { /* uma leitura só */ }

            if (varios != null && varios.Length > 0)
            {
                foreach (var r in varios) Juntar(achados, r?.Text);
                if (achados.Count > 0) return achados;
            }

            try
            {
                var um = leitor.Decode(fonte);
                Juntar(achados, um?.Text);
            }
            catch { }
            return achados;
        }

        public static List<string> DeTexto(string texto) =>
            Uris(ParserOtpAuth.ParsearExport(texto));

        private static void Juntar(List<string> achados, string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            foreach (var uri in DeTexto(texto))
            {
                if (!achados.Exists(a => string.Equals(a, uri, StringComparison.OrdinalIgnoreCase)))
                    achados.Add(uri);
            }
        }

        private static List<string> Uris(List<EntradaAuth> entradas)
        {
            var lista = new List<string>();
            if (entradas == null) return lista;
            foreach (var e in entradas)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Secret)) continue;
                lista.Add(Montar(e));
            }
            return lista;
        }

        /// <summary>Remonta um otpauth estável a partir do que o parser guardou.</summary>
        public static string Montar(EntradaAuth e)
        {
            var issuer = e.Issuer ?? string.Empty;
            var conta = e.Account ?? string.Empty;
            var rotulo = Uri.EscapeDataString(issuer);
            if (conta.Length > 0) rotulo = rotulo + ":" + Uri.EscapeDataString(conta);
            var algo = string.IsNullOrWhiteSpace(e.Algoritmo) ? "SHA1" : e.Algoritmo;
            return "otpauth://totp/" + rotulo
                + "?secret=" + Uri.EscapeDataString(e.Secret)
                + "&issuer=" + Uri.EscapeDataString(issuer)
                + "&algorithm=" + Uri.EscapeDataString(algo)
                + "&digits=" + (e.Digitos > 0 ? e.Digitos : 6)
                + "&period=" + (e.Periodo > 0 ? e.Periodo : 30);
        }
    }
}
