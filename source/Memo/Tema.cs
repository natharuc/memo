using System;
using System.Windows;
using Memo.Service;

namespace Memo
{
    /// <summary>
    /// Troca a paleta de cores em runtime (índice 0 das MergedDictionaries do App)
    /// e reaplica a barra de título nativa em todas as janelas abertas.
    /// </summary>
    internal static class Tema
    {
        public static bool EhEscuro { get; private set; } = true;

        /// <summary>Aplica o tema pelo nome (Configuracoes.TemaEscuro / TemaClaro).</summary>
        public static void Aplicar(string tema)
        {
            var escuro = !string.Equals(tema, Configuracoes.TemaClaro, StringComparison.OrdinalIgnoreCase);
            EhEscuro = escuro;

            var fonte = escuro ? "PaletaEscura.xaml" : "PaletaClara.xaml";
            var paleta = new ResourceDictionary { Source = new Uri(fonte, UriKind.Relative) };

            var dicts = Application.Current.Resources.MergedDictionaries;
            if (dicts.Count > 0) dicts[0] = paleta;   // índice 0 = paleta (ver App.xaml)
            else dicts.Add(paleta);

            foreach (Window janela in Application.Current.Windows)
                Nativo.AplicarBarraTitulo(janela);
        }
    }
}
