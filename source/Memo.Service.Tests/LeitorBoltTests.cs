using System;
using System.IO;
using System.Text;
using Xunit;
using Memo.Service.Auth;

namespace Memo.Service.Tests
{
    public class LeitorBoltTests
    {
        [Fact]
        public void Le_balde_inline_com_um_par()
        {
            var db = Montar();
            using (var ms = new MemoryStream(db))
            {
                var pares = LeitorBolt.LerBalde(ms, "accounts");
                Assert.Single(pares);
                Assert.Equal("auth-1", pares[0].Key);
                Assert.Equal("{\"app\":\"auth\"}", Encoding.UTF8.GetString(pares[0].Value));
            }
        }

        private static byte[] Montar()
        {
            const int pagina = 4096;
            var db = new byte[pagina * 3];

            // Página 2: folha da raiz, uma entrada "accounts" que é um balde inline.
            var valorJson = Encoding.UTF8.GetBytes("{\"app\":\"auth\"}");
            var chaveInterna = Encoding.UTF8.GetBytes("auth-1");
            var paginaInline = Folha(chaveInterna, valorJson, 0);
            var valorBalde = new byte[16 + paginaInline.Length];
            // root = 0 => inline; sequence = 0
            Buffer.BlockCopy(paginaInline, 0, valorBalde, 16, paginaInline.Length);

            var chave = Encoding.UTF8.GetBytes("accounts");
            var folhaRaiz = Folha(chave, valorBalde, 1);
            Buffer.BlockCopy(folhaRaiz, 0, db, pagina * 2, folhaRaiz.Length);

            EscreverMeta(db, 0, pagina, root: 2, txid: 2);
            EscreverMeta(db, pagina, pagina, root: 2, txid: 1);
            return db;
        }

        private static byte[] Folha(byte[] chave, byte[] valor, uint flags)
        {
            // cabeçalho 16 + elemento 16 + dados
            var buf = new byte[16 + 16 + chave.Length + valor.Length];
            // flags folha
            buf[8] = 0x02;
            buf[10] = 1; // count
            var el = 16;
            EscreverU32(buf, el, flags);
            EscreverU32(buf, el + 4, 16); // pos relativo ao elemento
            EscreverU32(buf, el + 8, (uint)chave.Length);
            EscreverU32(buf, el + 12, (uint)valor.Length);
            Buffer.BlockCopy(chave, 0, buf, el + 16, chave.Length);
            Buffer.BlockCopy(valor, 0, buf, el + 16 + chave.Length, valor.Length);
            return buf;
        }

        private static void EscreverMeta(byte[] db, int off, int pagina, ulong root, ulong txid)
        {
            // flags meta
            db[off + 8] = 0x04;
            EscreverU32(db, off + 16, 0xED0CDAED);
            EscreverU32(db, off + 20, 2);
            EscreverU32(db, off + 24, (uint)pagina);
            EscreverU64(db, off + 32, root);
            EscreverU64(db, off + 64, txid);
        }

        private static void EscreverU32(byte[] b, int o, uint v)
        {
            b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
        }

        private static void EscreverU64(byte[] b, int o, ulong v)
        {
            for (var i = 0; i < 8; i++) b[o + i] = (byte)(v >> (8 * i));
        }
    }
}
