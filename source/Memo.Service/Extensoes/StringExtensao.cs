using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Memo.Service.Extensoes
{
    public static class StringUtil
    {
        public static bool ContainsInsensitive(this string source, string search)
        {
            return (new CultureInfo("pt-BR").CompareInfo).IndexOf(source, search, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        public static bool EqualsInsensitive(this string source, string search)
        {
            return (new CultureInfo("pt-BR").CompareInfo).IndexOf(source, search, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == source.Length;
        }

        public static string ApenasLetra(this string str)
        {
            var apenasDigitos = new Regex(@"[^\a-zA-Z]");
            return apenasDigitos?.Replace(str, "") ?? null;
        }

        public static string RemoverCaracteresEspeciais(this string str)
        {
            try
            {
                if (string.IsNullOrEmpty(str)) return null;
                /** Troca os caracteres acentuados por não acentuados **/
                string[] acentos = new string[] { "ç", "Ç", "á", "é", "í", "ó", "ú", "ý", "Á", "É", "Í", "Ó", "Ú", "Ý", "à", "è", "ì", "ò", "ù", "À", "È", "Ì", "Ò", "Ù", "ã", "õ", "ñ", "ä", "ë", "ï", "ö", "ü", "ÿ", "Ä", "Ë", "Ï", "Ö", "Ü", "Ã", "Õ", "Ñ", "â", "ê", "î", "ô", "û", "Â", "Ê", "Î", "Ô", "Û" };
                string[] semAcento = new string[] { "c", "C", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "Y", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U", "a", "o", "n", "a", "e", "i", "o", "u", "y", "A", "E", "I", "O", "U", "A", "O", "N", "a", "e", "i", "o", "u", "A", "E", "I", "O", "U" };
                for (int i = 0; i < acentos.Length; i++)
                {
                    str = str.Replace(acentos[i], semAcento[i]);
                }
                /** Troca os caracteres especiais da string por "" **/
                string[] caracteresEspeciais = { "\\.", ",", "-", ":", "\\(", "\\)", "ª", "\\|", "\\\\", "°" };
                for (int i = 0; i < caracteresEspeciais.Length; i++)
                {
                    str = str.Replace(caracteresEspeciais[i], "");
                }
                /** Troca os espaços no início por "" **/
                str = str.Replace("^\\s+", "");
                /** Troca os espaços no início por "" **/
                str = str.Replace("\\s+$", "");
                /** Troca os espaços duplicados, tabulações e etc por  " " **/
                str = str.Replace("\\s+", " ");
                return str;
            }
            catch (Exception)
            {
                throw;
            }

        }

        public static bool ValidarEAN13(this string CodigoEAN13)
        {
            bool result = (CodigoEAN13?.Length == 13);

            if (result)
            {
                const string checkSum = "131313131313";

                int digito = int.Parse(CodigoEAN13[CodigoEAN13.Length - 1].ToString());
                string ean = CodigoEAN13.Substring(0, CodigoEAN13.Length - 1);

                int sum = 0;
                for (int i = 0; i <= ean.Length - 1; i++)
                {
                    sum += int.Parse(ean[i].ToString()) * int.Parse(checkSum[i].ToString());
                }
                int calculo = 10 - (sum % 10);
                result = (digito == calculo);
            }
            return result;
        }

        public static string ApenasNumero(this string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return null;

            var apenasDigitos = new Regex(@"[^\d]");
            return apenasDigitos?.Replace(str, "") ?? null;
        }

        public static string ApenasCaracteresNumerais(this string str)
        {
            var apenasDigitos = new Regex(@"[^0-9\,\.]");
            return apenasDigitos?.Replace(str, "") ?? null;
        }

        public static string Compacta(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            MemoryStream ms = new MemoryStream();
            using (GZipStream zip = new GZipStream(ms, CompressionMode.Compress, true))
            {
                zip.Write(buffer, 0, buffer.Length);
            }

            ms.Position = 0;
            MemoryStream outStream = new MemoryStream();

            byte[] compressed = new byte[ms.Length];
            ms.Read(compressed, 0, compressed.Length);

            byte[] gzBuffer = new byte[compressed.Length + 4];
            System.Buffer.BlockCopy(compressed, 0, gzBuffer, 4, compressed.Length);
            System.Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gzBuffer, 0, 4);
            return Convert.ToBase64String(gzBuffer);
        }

        public static string Descompacta(string compressedText)
        {
            try
            {
                byte[] gzBuffer = Convert.FromBase64String(compressedText);
                using (MemoryStream ms = new MemoryStream())
                {
                    int msgLength = BitConverter.ToInt32(gzBuffer, 0);
                    ms.Write(gzBuffer, 4, gzBuffer.Length - 4);

                    byte[] buffer = new byte[msgLength];

                    ms.Position = 0;
                    using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        zip.Read(buffer, 0, buffer.Length);
                    }
                    return Encoding.UTF8.GetString(buffer);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static string ToBase64(this string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string FromBase64(this string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static Stream ToStream(this string str)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(str);
                    writer.Flush();
                    stream.Position = 0;
                    return stream;
                }

            }

        }

        public static string Normalizar(this string text)
        {
            string formD = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char ch in formD)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string NormalizarTrim(this string text)
        {
            return Normalizar(text?.Trim());
        }


        public static bool MesmaCoisa(this string text, string compare)
        {
            text = text?.ToLower();
            compare = compare?.ToLower();

            return Normalizar(text?.Trim()) == Normalizar(compare);
        }

        public static string ToUpperFirstChar(this string input)
        {
            switch (input)
            {
                case null: throw new ArgumentNullException(nameof(input));
                case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default: return input[0].ToString().ToUpper() + input.Substring(1);
            }
        }


    }
}