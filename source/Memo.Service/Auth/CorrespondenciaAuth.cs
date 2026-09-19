using System;
using System.Collections.Generic;
using System.Linq;

namespace Memo.Service.Auth
{
    /// <summary>Filtro AND, case-insensitive, em issuer / account / label.</summary>
    public static class CorrespondenciaAuth
    {
        public static List<EntradaAuth> Filtrar(IEnumerable<EntradaAuth> entradas, IEnumerable<string> tokens)
        {
            var toks = (tokens ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();

            var lista = (entradas ?? Array.Empty<EntradaAuth>()).ToList();
            if (toks.Count == 0) return lista;

            return lista.Where(e => toks.All(t => Contem(e, t))).ToList();
        }

        private static bool Contem(EntradaAuth e, string token)
        {
            if (e == null) return false;
            return Contem(e.Issuer, token) || Contem(e.Account, token) || Contem(e.Label, token);
        }

        private static bool Contem(string campo, string token) =>
            !string.IsNullOrEmpty(campo) &&
            campo.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
