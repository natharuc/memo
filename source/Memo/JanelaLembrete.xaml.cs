using System;
using System.Windows;
using System.Windows.Controls;

namespace Memo
{
    /// <summary>Popup do lembrete: Concluído ou Adiar 5/10/15 min.</summary>
    public partial class JanelaLembrete : Window
    {
        /// <summary>Disparado ao clicar em Concluído.</summary>
        public event Action Concluido;

        /// <summary>Disparado ao adiar (passa os minutos).</summary>
        public event Action<int> Adiado;

        public JanelaLembrete(string texto)
        {
            InitializeComponent();
            textoLembrete.Text = texto;

            Loaded += (_, __) =>
            {
                Activate();
                Som.TocarLembrete();
            };
        }

        private void Concluir_Click(object sender, RoutedEventArgs e)
        {
            Concluido?.Invoke();
            Close();
        }

        private void Adiar_Click(object sender, RoutedEventArgs e)
        {
            var minutos = int.Parse((string)((Button)sender).Tag);
            Adiado?.Invoke(minutos);
            Close();
        }
    }
}
