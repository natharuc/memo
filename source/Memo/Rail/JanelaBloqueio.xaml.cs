using System;
using System.Windows;
using System.Windows.Interop;

namespace Memo.Rail
{
    /// <summary>
    /// Backdrop de tela cheia do modo foco: cobre a janela de distração para o
    /// usuário não conseguir vê-la. Não rouba o foco do teclado (`ShowActivated`);
    /// é mostrado/escondido dinamicamente pelo <see cref="BloqueioFoco"/>.
    /// </summary>
    public partial class JanelaBloqueio : Window
    {
        /// <summary>Usuário pediu para encerrar o modo foco (válvula de escape).</summary>
        public event Action EncerrarSolicitado;

        /// <summary>Usuário pediu para fechar a janela/aba que está distraindo.</summary>
        public event Action FecharDistracaoSolicitado;

        public JanelaBloqueio()
        {
            InitializeComponent();
        }

        public IntPtr Handle => new WindowInteropHelper(this).Handle;

        public void DefinirTarefa(string tarefa)
        {
            textoTarefa.Text = string.IsNullOrWhiteSpace(tarefa) ? "" : $"Sua missão agora: {tarefa}";
        }

        public void AtualizarContagem(TimeSpan restante)
        {
            if (restante < TimeSpan.Zero) restante = TimeSpan.Zero;
            textoContagem.Text = restante.TotalMinutes >= 1
                ? $"{(int)restante.TotalMinutes} min {restante.Seconds:00}s restantes"
                : $"{restante.Seconds}s restantes";
        }

        private void Fechar_Click(object sender, RoutedEventArgs e) => FecharDistracaoSolicitado?.Invoke();

        private void Encerrar_Click(object sender, RoutedEventArgs e) => EncerrarSolicitado?.Invoke();
    }
}
