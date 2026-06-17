using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Memo.Service
{
    /// <summary>Opções de geração de senha.</summary>
    public class OpcoesSenha
    {
        public int Comprimento { get; set; } = 16;
        public bool Maiusculas { get; set; } = true;
        public bool Minusculas { get; set; } = true;
        public bool Numeros { get; set; } = true;
        public bool Simbolos { get; set; } = true;
    }

    public enum NivelForca { Fraca, Media, Forte }

    /// <summary>Avaliação de força de uma senha (para a barra e o rótulo da UI).</summary>
    public class ForcaSenha
    {
        public NivelForca Nivel { get; set; }
        public string Rotulo { get; set; }

        /// <summary>Fração de 0 a 1 para preencher a barra de força.</summary>
        public double Fracao { get; set; }
    }

    /// <summary>
    /// Gera senhas aleatórias com um RNG criptográfico e estima sua força.
    /// Sem dependência de WPF.
    /// </summary>
    public static class GeradorSenha
    {
        private const string Minusculas = "abcdefghijklmnopqrstuvwxyz";
        private const string Maiusculas = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Numeros = "0123456789";
        private const string Simbolos = "!@#$%&*()-_=+[]{}:;,.?";

        /// <summary>Gera uma senha segundo as opções. Garante ao menos um de cada conjunto escolhido.</summary>
        public static string Gerar(OpcoesSenha opcoes)
        {
            var conjuntos = ConjuntosSelecionados(opcoes);
            if (conjuntos.Count == 0) return string.Empty;

            var pool = string.Concat(conjuntos);
            var comprimento = Math.Max(1, opcoes.Comprimento);
            var chars = new char[comprimento];
            var i = 0;

            // Garante representatividade: um caractere de cada conjunto, se couber.
            if (comprimento >= conjuntos.Count)
                foreach (var conjunto in conjuntos)
                    chars[i++] = conjunto[Indice(conjunto.Length)];

            for (; i < comprimento; i++)
                chars[i] = pool[Indice(pool.Length)];

            Embaralhar(chars);
            return new string(chars);
        }

        /// <summary>Estima a força com base no comprimento e no tamanho do alfabeto.</summary>
        public static ForcaSenha Avaliar(OpcoesSenha opcoes)
        {
            var pool = 0;
            foreach (var c in ConjuntosSelecionados(opcoes)) pool += c.Length;

            if (pool == 0 || opcoes.Comprimento == 0)
                return new ForcaSenha { Nivel = NivelForca.Fraca, Rotulo = "Fraca", Fracao = 0 };

            // Entropia em bits ≈ comprimento * log2(tamanho do alfabeto).
            var bits = opcoes.Comprimento * Math.Log(pool, 2);

            NivelForca nivel;
            string rotulo;
            if (bits < 45) { nivel = NivelForca.Fraca; rotulo = "Fraca"; }
            else if (bits < 70) { nivel = NivelForca.Media; rotulo = "Média"; }
            else { nivel = NivelForca.Forte; rotulo = "Forte"; }

            return new ForcaSenha
            {
                Nivel = nivel,
                Rotulo = rotulo,
                Fracao = Math.Min(1.0, Math.Max(0.08, bits / 90.0))
            };
        }

        private static List<string> ConjuntosSelecionados(OpcoesSenha o)
        {
            var lista = new List<string>();
            if (o.Minusculas) lista.Add(Minusculas);
            if (o.Maiusculas) lista.Add(Maiusculas);
            if (o.Numeros) lista.Add(Numeros);
            if (o.Simbolos) lista.Add(Simbolos);
            return lista;
        }

        private static int Indice(int limite) => RandomNumberGenerator.GetInt32(limite);

        private static void Embaralhar(char[] v)
        {
            for (var i = v.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (v[i], v[j]) = (v[j], v[i]);
            }
        }
    }
}
