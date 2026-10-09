using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Cifra compatível com a Ente CLI: secretstream XChaCha20-Poly1305
    /// (cli/internal/crypto/stream.go) e secretbox XSalsa20-Poly1305 (NaCl).
    /// </summary>
    public static class CriptoEnte
    {
        public const byte TagMensagem = 0x00;
        public const byte TagFinal = 0x03;

        public static void CifrarFluxo(byte[] claro, byte[] chave, out byte[] cifrado, out byte[] cabecalho)
        {
            if (chave == null || chave.Length != 32) throw new ArgumentException("Chave de 32 bytes.");
            claro = claro ?? Array.Empty<byte>();
            cabecalho = new byte[24];
            RandomNumberGenerator.Fill(cabecalho);
            cifrado = EmpurrarComChave(claro, chave, cabecalho, TagFinal);
        }

        public static byte[] DecifrarFluxo(byte[] cifrado, byte[] chave, byte[] cabecalho)
        {
            if (chave == null || chave.Length != 32) throw new ArgumentException("Chave de 32 bytes.");
            if (cabecalho == null || cabecalho.Length != 24) throw new ArgumentException("Cabeçalho de 24 bytes.");
            return Puxar(cifrado, chave, cabecalho, aceitarMensagem: true);
        }

        /// <summary>NaCl secretbox open. O MAC de 16 bytes vem na frente do cifrado.</summary>
        public static byte[] AbrirCaixa(byte[] caixa, byte[] nonce, byte[] chave)
        {
            if (chave == null || chave.Length != 32 || nonce == null || nonce.Length != 24)
                throw new CryptographicException("secretbox: tamanho inválido");
            if (caixa == null || caixa.Length < 16)
                throw new CryptographicException("secretbox: caixa curta");

            var sub = HSalsa20(Cortar(nonce, 0, 16), chave);
            var contador = new byte[16];
            Buffer.BlockCopy(nonce, 16, contador, 0, 8);

            var primeiro = new byte[64];
            SalsaXor(primeiro, 0, primeiro, 0, 64, contador, sub);

            var chavePoly = Cortar(primeiro, 0, 32);
            var mac = Cortar(caixa, 0, 16);
            var corpo = Cortar(caixa, 16, caixa.Length - 16);
            var calculado = Poly1305(corpo, chavePoly);
            if (!Igual(mac, calculado))
                throw new CryptographicException("secretbox: MAC inválido");

            var claro = new byte[corpo.Length];
            var n = Math.Min(32, corpo.Length);
            for (var i = 0; i < n; i++) claro[i] = (byte)(corpo[i] ^ primeiro[32 + i]);
            if (corpo.Length > 32)
            {
                contador[8] = 1;
                SalsaXor(claro, 32, corpo, 32, corpo.Length - 32, contador, sub);
            }
            return claro;
        }

        private static byte[] EmpurrarComChave(byte[] claro, byte[] chave, byte[] cabecalho, byte tag)
        {
            var fluxo = NovoFluxo(chave, cabecalho);
            var blocoChave = new byte[64];
            fluxo.Xor(blocoChave, blocoChave);
            var chavePoly = Cortar(blocoChave, 0, 32);

            var bloco = new byte[64];
            bloco[0] = tag;
            fluxo.Xor(bloco, bloco);

            var cifradoMsg = new byte[claro.Length];
            if (claro.Length > 0) fluxo.Xor(cifradoMsg, claro);

            var macEntrada = MontarMac(bloco, cifradoMsg);
            var mac = Poly1305(macEntrada, chavePoly);

            var saida = new byte[claro.Length + 17];
            saida[0] = bloco[0];
            Buffer.BlockCopy(cifradoMsg, 0, saida, 1, cifradoMsg.Length);
            Buffer.BlockCopy(mac, 0, saida, 1 + cifradoMsg.Length, 16);
            return saida;
        }

        private static byte[] Puxar(byte[] cifrado, byte[] chave, byte[] cabecalho, bool aceitarMensagem)
        {
            if (cifrado == null || cifrado.Length < 17)
                throw new CryptographicException("secretstream: mensagem curta");

            var mlen = cifrado.Length - 17;
            var fluxo = NovoFluxo(chave, cabecalho);
            var blocoChave = new byte[64];
            fluxo.Xor(blocoChave, blocoChave);
            var chavePoly = Cortar(blocoChave, 0, 32);

            var bloco = new byte[64];
            bloco[0] = cifrado[0];
            fluxo.Xor(bloco, bloco);
            var tag = bloco[0];
            bloco[0] = cifrado[0];

            if (tag != TagFinal && !(aceitarMensagem && tag == TagMensagem))
                throw new CryptographicException("secretstream: tag inválida");

            var corpo = new byte[mlen];
            Buffer.BlockCopy(cifrado, 1, corpo, 0, mlen);
            var macEntrada = MontarMac(bloco, corpo);
            var mac = Poly1305(macEntrada, chavePoly);
            var macGuardado = Cortar(cifrado, 1 + mlen, 16);
            if (!Igual(mac, macGuardado))
                throw new CryptographicException("secretstream: MAC inválido");

            var claro = new byte[mlen];
            if (mlen > 0) fluxo.Xor(claro, corpo);
            return claro;
        }

        private static byte[] MontarMac(byte[] bloco64, byte[] cifradoMsg)
        {
            var pad = (0x10 - 64 + cifradoMsg.Length) & 0xf;
            var buf = new byte[64 + cifradoMsg.Length + pad + 16];
            Buffer.BlockCopy(bloco64, 0, buf, 0, 64);
            Buffer.BlockCopy(cifradoMsg, 0, buf, 64, cifradoMsg.Length);
            var off = 64 + cifradoMsg.Length + pad;
            var tam = U64((ulong)(64 + cifradoMsg.Length));
            Buffer.BlockCopy(tam, 0, buf, off + 8, 8);
            return buf;
        }

        private static FluxoChaCha NovoFluxo(byte[] chave, byte[] cabecalho)
        {
            var derivada = HChaCha20(chave, Cortar(cabecalho, 0, 16));
            var nonce = new byte[12];
            nonce[0] = 1;
            Buffer.BlockCopy(cabecalho, 16, nonce, 4, 8);
            return new FluxoChaCha(derivada, nonce);
        }

        // ----- ChaCha20 IETF -----

        private sealed class FluxoChaCha
        {
            private readonly uint[] _chave = new uint[8];
            private readonly uint[] _nonce = new uint[3];
            private uint _contador;
            private readonly byte[] _sobra = new byte[64];
            private int _sobraLen;

            public FluxoChaCha(byte[] chave, byte[] nonce12)
            {
                for (var i = 0; i < 8; i++) _chave[i] = U32(chave, i * 4);
                _nonce[0] = U32(nonce12, 0);
                _nonce[1] = U32(nonce12, 4);
                _nonce[2] = U32(nonce12, 8);
            }

            public void Xor(byte[] dst, byte[] src)
            {
                var tmp = new byte[src.Length];
                var feito = 0;
                if (_sobraLen > 0)
                {
                    var n = Math.Min(_sobraLen, src.Length);
                    for (var i = 0; i < n; i++) tmp[i] = (byte)(src[i] ^ _sobra[64 - _sobraLen + i]);
                    _sobraLen -= n;
                    feito = n;
                }
                while (feito < src.Length)
                {
                    var bloco = Bloco();
                    var resta = src.Length - feito;
                    if (resta >= 64)
                    {
                        for (var i = 0; i < 64; i++) tmp[feito + i] = (byte)(src[feito + i] ^ bloco[i]);
                        feito += 64;
                    }
                    else
                    {
                        for (var i = 0; i < resta; i++) tmp[feito + i] = (byte)(src[feito + i] ^ bloco[i]);
                        _sobraLen = 64 - resta;
                        Buffer.BlockCopy(bloco, resta, _sobra, resta, _sobraLen);
                        // sobra fica no fim do buffer de 64, alinhada como Go (buf[bufSize-len:])
                        var alinhada = new byte[64];
                        Buffer.BlockCopy(bloco, resta, alinhada, 64 - _sobraLen, _sobraLen);
                        Buffer.BlockCopy(alinhada, 0, _sobra, 0, 64);
                        feito += resta;
                    }
                }
                Buffer.BlockCopy(tmp, 0, dst, 0, tmp.Length);
            }

            private byte[] Bloco()
            {
                uint x0 = 0x61707865, x1 = 0x3320646e, x2 = 0x79622d32, x3 = 0x6b206574;
                uint x4 = _chave[0], x5 = _chave[1], x6 = _chave[2], x7 = _chave[3];
                uint x8 = _chave[4], x9 = _chave[5], x10 = _chave[6], x11 = _chave[7];
                uint x12 = _contador, x13 = _nonce[0], x14 = _nonce[1], x15 = _nonce[2];
                uint j0 = x0, j1 = x1, j2 = x2, j3 = x3, j4 = x4, j5 = x5, j6 = x6, j7 = x7;
                uint j8 = x8, j9 = x9, j10 = x10, j11 = x11, j12 = x12, j13 = x13, j14 = x14, j15 = x15;

                for (var i = 0; i < 10; i++)
                {
                    QR(ref x0, ref x4, ref x8, ref x12);
                    QR(ref x1, ref x5, ref x9, ref x13);
                    QR(ref x2, ref x6, ref x10, ref x14);
                    QR(ref x3, ref x7, ref x11, ref x15);
                    QR(ref x0, ref x5, ref x10, ref x15);
                    QR(ref x1, ref x6, ref x11, ref x12);
                    QR(ref x2, ref x7, ref x8, ref x13);
                    QR(ref x3, ref x4, ref x9, ref x14);
                }

                var bloco = new byte[64];
                Escrever(bloco, 0, x0 + j0); Escrever(bloco, 4, x1 + j1);
                Escrever(bloco, 8, x2 + j2); Escrever(bloco, 12, x3 + j3);
                Escrever(bloco, 16, x4 + j4); Escrever(bloco, 20, x5 + j5);
                Escrever(bloco, 24, x6 + j6); Escrever(bloco, 28, x7 + j7);
                Escrever(bloco, 32, x8 + j8); Escrever(bloco, 36, x9 + j9);
                Escrever(bloco, 40, x10 + j10); Escrever(bloco, 44, x11 + j11);
                Escrever(bloco, 48, x12 + j12); Escrever(bloco, 52, x13 + j13);
                Escrever(bloco, 56, x14 + j14); Escrever(bloco, 60, x15 + j15);
                _contador++;
                return bloco;
            }
        }

        private static void QR(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d ^= a; d = Rot(d, 16);
            c += d; b ^= c; b = Rot(b, 12);
            a += b; d ^= a; d = Rot(d, 8);
            c += d; b ^= c; b = Rot(b, 7);
        }

        private static uint Rot(uint x, int n) => (x << n) | (x >> (32 - n));

        private static byte[] HChaCha20(byte[] chave, byte[] nonce16)
        {
            uint x0 = 0x61707865, x1 = 0x3320646e, x2 = 0x79622d32, x3 = 0x6b206574;
            uint x4 = U32(chave, 0), x5 = U32(chave, 4), x6 = U32(chave, 8), x7 = U32(chave, 12);
            uint x8 = U32(chave, 16), x9 = U32(chave, 20), x10 = U32(chave, 24), x11 = U32(chave, 28);
            uint x12 = U32(nonce16, 0), x13 = U32(nonce16, 4), x14 = U32(nonce16, 8), x15 = U32(nonce16, 12);
            for (var i = 0; i < 10; i++)
            {
                QR(ref x0, ref x4, ref x8, ref x12);
                QR(ref x1, ref x5, ref x9, ref x13);
                QR(ref x2, ref x6, ref x10, ref x14);
                QR(ref x3, ref x7, ref x11, ref x15);
                QR(ref x0, ref x5, ref x10, ref x15);
                QR(ref x1, ref x6, ref x11, ref x12);
                QR(ref x2, ref x7, ref x8, ref x13);
                QR(ref x3, ref x4, ref x9, ref x14);
            }
            var saida = new byte[32];
            Escrever(saida, 0, x0); Escrever(saida, 4, x1); Escrever(saida, 8, x2); Escrever(saida, 12, x3);
            Escrever(saida, 16, x12); Escrever(saida, 20, x13); Escrever(saida, 24, x14); Escrever(saida, 28, x15);
            return saida;
        }

        // ----- Poly1305 -----

        private static byte[] Poly1305(byte[] msg, byte[] chave32)
        {
            var rBytes = (byte[])chave32.Clone();
            rBytes = Cortar(rBytes, 0, 16);
            rBytes[3] &= 15; rBytes[7] &= 15; rBytes[11] &= 15; rBytes[15] &= 15;
            rBytes[4] &= 252; rBytes[8] &= 252; rBytes[12] &= 252;
            var r = LerLe(rBytes);
            var s = LerLe(Cortar(chave32, 16, 16));
            var p = (BigInteger.One << 130) - 5;
            BigInteger h = 0;
            var off = 0;
            msg = msg ?? Array.Empty<byte>();
            while (off < msg.Length)
            {
                var n = Math.Min(16, msg.Length - off);
                var bloco = new byte[n + 1];
                Buffer.BlockCopy(msg, off, bloco, 0, n);
                bloco[n] = 1;
                h = (h + LerLe(bloco)) * r % p;
                off += n;
            }
            var soma = h + s;
            var bruto = soma.ToByteArray();
            var tag = new byte[16];
            Buffer.BlockCopy(bruto, 0, tag, 0, Math.Min(16, bruto.Length));
            return tag;
        }

        // ----- Salsa20 / secretbox -----

        private static readonly byte[] Sigma = { (byte)'e', (byte)'x', (byte)'p', (byte)'a', (byte)'n', (byte)'d', (byte)' ', (byte)'3', (byte)'2', (byte)'-', (byte)'b', (byte)'y', (byte)'t', (byte)'e', (byte)' ', (byte)'k' };

        private static byte[] HSalsa20(byte[] entrada16, byte[] chave32)
        {
            uint x0 = U32(Sigma, 0), x5 = U32(Sigma, 4), x10 = U32(Sigma, 8), x15 = U32(Sigma, 12);
            uint x1 = U32(chave32, 0), x2 = U32(chave32, 4), x3 = U32(chave32, 8), x4 = U32(chave32, 12);
            uint x11 = U32(chave32, 16), x12 = U32(chave32, 20), x13 = U32(chave32, 24), x14 = U32(chave32, 28);
            uint x6 = U32(entrada16, 0), x7 = U32(entrada16, 4), x8 = U32(entrada16, 8), x9 = U32(entrada16, 12);
            RodadasSalsa(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15);
            var saida = new byte[32];
            Escrever(saida, 0, x0); Escrever(saida, 4, x5); Escrever(saida, 8, x10); Escrever(saida, 12, x15);
            Escrever(saida, 16, x6); Escrever(saida, 20, x7); Escrever(saida, 24, x8); Escrever(saida, 28, x9);
            return saida;
        }

        private static void SalsaXor(byte[] dst, int dstOff, byte[] src, int srcOff, int len, byte[] contador16, byte[] chave32)
        {
            var contador = (byte[])contador16.Clone();
            var off = 0;
            while (off < len)
            {
                var bloco = SalsaBloco(contador, chave32);
                var n = Math.Min(64, len - off);
                for (var i = 0; i < n; i++) dst[dstOff + off + i] = (byte)(src[srcOff + off + i] ^ bloco[i]);
                uint vai = 1;
                for (var i = 8; i < 16; i++)
                {
                    vai += contador[i];
                    contador[i] = (byte)vai;
                    vai >>= 8;
                }
                off += n;
            }
        }

        private static byte[] SalsaBloco(byte[] entrada16, byte[] chave32)
        {
            uint j0 = U32(Sigma, 0), j5 = U32(Sigma, 4), j10 = U32(Sigma, 8), j15 = U32(Sigma, 12);
            uint j1 = U32(chave32, 0), j2 = U32(chave32, 4), j3 = U32(chave32, 8), j4 = U32(chave32, 12);
            uint j11 = U32(chave32, 16), j12 = U32(chave32, 20), j13 = U32(chave32, 24), j14 = U32(chave32, 28);
            uint j6 = U32(entrada16, 0), j7 = U32(entrada16, 4), j8 = U32(entrada16, 8), j9 = U32(entrada16, 12);
            uint x0 = j0, x1 = j1, x2 = j2, x3 = j3, x4 = j4, x5 = j5, x6 = j6, x7 = j7;
            uint x8 = j8, x9 = j9, x10 = j10, x11 = j11, x12 = j12, x13 = j13, x14 = j14, x15 = j15;
            RodadasSalsa(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15);
            var saida = new byte[64];
            Escrever(saida, 0, x0 + j0); Escrever(saida, 4, x1 + j1); Escrever(saida, 8, x2 + j2); Escrever(saida, 12, x3 + j3);
            Escrever(saida, 16, x4 + j4); Escrever(saida, 20, x5 + j5); Escrever(saida, 24, x6 + j6); Escrever(saida, 28, x7 + j7);
            Escrever(saida, 32, x8 + j8); Escrever(saida, 36, x9 + j9); Escrever(saida, 40, x10 + j10); Escrever(saida, 44, x11 + j11);
            Escrever(saida, 48, x12 + j12); Escrever(saida, 52, x13 + j13); Escrever(saida, 56, x14 + j14); Escrever(saida, 60, x15 + j15);
            return saida;
        }

        private static void RodadasSalsa(
            ref uint x0, ref uint x1, ref uint x2, ref uint x3, ref uint x4, ref uint x5, ref uint x6, ref uint x7,
            ref uint x8, ref uint x9, ref uint x10, ref uint x11, ref uint x12, ref uint x13, ref uint x14, ref uint x15)
        {
            for (var i = 0; i < 20; i += 2)
            {
                uint u;
                u = x0 + x12; x4 ^= Rot(u, 7); u = x4 + x0; x8 ^= Rot(u, 9); u = x8 + x4; x12 ^= Rot(u, 13); u = x12 + x8; x0 ^= Rot(u, 18);
                u = x5 + x1; x9 ^= Rot(u, 7); u = x9 + x5; x13 ^= Rot(u, 9); u = x13 + x9; x1 ^= Rot(u, 13); u = x1 + x13; x5 ^= Rot(u, 18);
                u = x10 + x6; x14 ^= Rot(u, 7); u = x14 + x10; x2 ^= Rot(u, 9); u = x2 + x14; x6 ^= Rot(u, 13); u = x6 + x2; x10 ^= Rot(u, 18);
                u = x15 + x11; x3 ^= Rot(u, 7); u = x3 + x15; x7 ^= Rot(u, 9); u = x7 + x3; x11 ^= Rot(u, 13); u = x11 + x7; x15 ^= Rot(u, 18);
                u = x0 + x3; x1 ^= Rot(u, 7); u = x1 + x0; x2 ^= Rot(u, 9); u = x2 + x1; x3 ^= Rot(u, 13); u = x3 + x2; x0 ^= Rot(u, 18);
                u = x5 + x4; x6 ^= Rot(u, 7); u = x6 + x5; x7 ^= Rot(u, 9); u = x7 + x6; x4 ^= Rot(u, 13); u = x4 + x7; x5 ^= Rot(u, 18);
                u = x10 + x9; x11 ^= Rot(u, 7); u = x11 + x10; x8 ^= Rot(u, 9); u = x8 + x11; x9 ^= Rot(u, 13); u = x9 + x8; x10 ^= Rot(u, 18);
                u = x15 + x14; x12 ^= Rot(u, 7); u = x12 + x15; x13 ^= Rot(u, 9); u = x13 + x12; x14 ^= Rot(u, 13); u = x14 + x13; x15 ^= Rot(u, 18);
            }
        }

        private static uint U32(byte[] b, int o) =>
            (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

        private static void Escrever(byte[] b, int o, uint v)
        {
            b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
        }

        private static byte[] U64(ulong v)
        {
            var b = new byte[8];
            for (var i = 0; i < 8; i++) b[i] = (byte)(v >> (8 * i));
            return b;
        }

        private static byte[] Cortar(byte[] b, int o, int n)
        {
            var d = new byte[n];
            Buffer.BlockCopy(b, o, d, 0, n);
            return d;
        }

        private static BigInteger LerLe(byte[] b)
        {
            var c = new byte[b.Length + 1];
            Buffer.BlockCopy(b, 0, c, 0, b.Length);
            return new BigInteger(c);
        }

        private static bool Igual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var d = 0;
            for (var i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }
    }
}
