using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Memo.Service.Auth
{
    /// <summary>
    /// Leitura só de leitura de um bbolt (o <c>ente-cli.db</c>). Não grava no arquivo.
    /// </summary>
    public static class LeitorBolt
    {
        private const uint Magia = 0xED0CDAED;
        private const ushort Folha = 0x02;
        private const ushort Ramo = 0x01;
        private const uint FlagBalde = 0x01;

        public static List<KeyValuePair<string, byte[]>> LerBalde(string caminho, string nome)
        {
            using (var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return LerBalde(fs, nome);
        }

        public static List<KeyValuePair<string, byte[]>> LerBalde(Stream entrada, string nome)
        {
            var arquivo = new Arquivo(entrada);
            foreach (var par in arquivo.Pares(arquivo.Raiz))
            {
                if (!string.Equals(par.Chave, nome, StringComparison.Ordinal)) continue;
                if ((par.Flags & FlagBalde) == 0)
                    throw new InvalidDataException("A entrada \"" + nome + "\" não é um balde.");
                var saida = new List<KeyValuePair<string, byte[]>>();
                foreach (var item in arquivo.ParesDoValor(par.Valor))
                    saida.Add(new KeyValuePair<string, byte[]>(item.Chave, item.Valor));
                return saida;
            }
            return new List<KeyValuePair<string, byte[]>>();
        }

        private sealed class Item
        {
            public string Chave;
            public byte[] Valor;
            public uint Flags;
        }

        private sealed class Arquivo
        {
            private readonly Stream _s;
            private int _pagina;
            private readonly ulong _raiz;

            public ulong Raiz => _raiz;

            public Arquivo(Stream s)
            {
                _s = s;
                var meta0 = LerMeta(0);
                if (meta0 == null) throw new InvalidDataException("ente-cli.db não é um bbolt.");
                _pagina = meta0.PageSize;
                var meta1 = _pagina > 0 && _s.Length >= _pagina * 2L ? LerMeta(_pagina) : null;
                var meta = meta1 != null && meta1.Txid > meta0.Txid ? meta1 : meta0;
                _pagina = meta.PageSize;
                _raiz = meta.Root;
                if (_pagina < 512 || _pagina > 1024 * 1024)
                    throw new InvalidDataException("Página bbolt inválida.");
            }

            public List<Item> Pares(ulong pgid) => Coletar(LerPagina(pgid), 0);

            public List<Item> ParesDoValor(byte[] valor)
            {
                if (valor == null || valor.Length < 16)
                    throw new InvalidDataException("Balde bbolt curto.");
                var root = BitConverter.ToUInt64(valor, 0);
                if (root == 0)
                {
                    if (valor.Length < 32) return new List<Item>();
                    return Coletar(valor, 16);
                }
                return Pares(root);
            }

            private List<Item> Coletar(byte[] buf, int off)
            {
                var saida = new List<Item>();
                if (buf.Length < off + 16) return saida;
                var flags = BitConverter.ToUInt16(buf, off + 8);
                var count = BitConverter.ToUInt16(buf, off + 10);
                if ((flags & Folha) != 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var el = off + 16 + i * 16;
                        if (el + 16 > buf.Length) break;
                        var f = BitConverter.ToUInt32(buf, el);
                        var pos = BitConverter.ToUInt32(buf, el + 4);
                        var ks = BitConverter.ToUInt32(buf, el + 8);
                        var vs = BitConverter.ToUInt32(buf, el + 12);
                        var inicio = (long)el + pos;
                        if (inicio < 0 || inicio + ks + vs > buf.Length) continue;
                        var chave = new byte[ks];
                        var val = new byte[vs];
                        Buffer.BlockCopy(buf, (int)inicio, chave, 0, (int)ks);
                        Buffer.BlockCopy(buf, (int)inicio + (int)ks, val, 0, (int)vs);
                        saida.Add(new Item
                        {
                            Chave = Encoding.UTF8.GetString(chave),
                            Valor = val,
                            Flags = f
                        });
                    }
                    return saida;
                }

                if ((flags & Ramo) != 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var el = off + 16 + i * 16;
                        if (el + 16 > buf.Length) break;
                        var pgid = BitConverter.ToUInt64(buf, el + 8);
                        saida.AddRange(Pares(pgid));
                    }
                }
                return saida;
            }

            private byte[] LerPagina(ulong pgid)
            {
                var pos = (long)pgid * _pagina;
                _s.Position = pos;
                var cab = new byte[16];
                if (LerExato(cab) != 16) throw new InvalidDataException("Página bbolt truncada.");
                var overflow = BitConverter.ToUInt32(cab, 12);
                var tamanho = (long)_pagina * (overflow + 1);
                if (tamanho > 32L * 1024 * 1024) throw new InvalidDataException("Página bbolt grande demais.");
                var buf = new byte[tamanho];
                Buffer.BlockCopy(cab, 0, buf, 0, 16);
                if (LerExato(buf, 16, (int)tamanho - 16) != tamanho - 16)
                    throw new InvalidDataException("Página bbolt truncada.");
                return buf;
            }

            private Meta LerMeta(long pos)
            {
                _s.Position = pos;
                var buf = new byte[80];
                if (LerExato(buf) != 80) return null;
                var flags = BitConverter.ToUInt16(buf, 8);
                if ((flags & 0x04) == 0) return null;
                if (BitConverter.ToUInt32(buf, 16) != Magia) return null;
                return new Meta
                {
                    PageSize = (int)BitConverter.ToUInt32(buf, 24),
                    Root = BitConverter.ToUInt64(buf, 32),
                    Txid = BitConverter.ToUInt64(buf, 64)
                };
            }

            private int LerExato(byte[] buf) => LerExato(buf, 0, buf.Length);

            private int LerExato(byte[] buf, int off, int n)
            {
                var lido = 0;
                while (lido < n)
                {
                    var r = _s.Read(buf, off + lido, n - lido);
                    if (r == 0) break;
                    lido += r;
                }
                return lido;
            }

            private sealed class Meta
            {
                public int PageSize;
                public ulong Root;
                public ulong Txid;
            }
        }
    }
}
