namespace Memo.Service.Notificacoes
{
    /// <summary>Canal do Telegram (bot + chat de destino).</summary>
    public class CanalTelegram
    {
        public bool Habilitado { get; set; }
        public string BotToken { get; set; }
        public string ChatId { get; set; }

        /// <summary>
        /// Liga o listener do bot (long-polling) enquanto o Memo está na bandeja:
        /// permite controlar o Memo Rail pelo Telegram (ver/adicionar/concluir/
        /// reordenar tarefas). Só obedece mensagens do <see cref="ChatId"/> acima.
        /// Não dá acesso a documentos/segredos — apenas ao Rail. Opt-in.
        /// </summary>
        public bool OuvirComandos { get; set; }
    }

    /// <summary>Canal de e-mail via SMTP.</summary>
    public class CanalEmail
    {
        public bool Habilitado { get; set; }
        public string Servidor { get; set; }
        public int Porta { get; set; } = 587;
        public bool UsarSsl { get; set; } = true;
        public string Usuario { get; set; }
        public string Senha { get; set; }
        public string De { get; set; }
        public string Para { get; set; }
    }

    /// <summary>
    /// Configuração dos canais de notificação. Contém segredos (token, senha SMTP):
    /// é gravada cifrada por DPAPI e nunca deve ser logada.
    /// </summary>
    public class NotificacaoConfig
    {
        public CanalTelegram Telegram { get; set; } = new CanalTelegram();
        public CanalEmail Email { get; set; } = new CanalEmail();
    }
}
