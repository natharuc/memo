using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Memo.Services;
using Newtonsoft.Json;

namespace Memo.Service.Notificacoes
{
    /// <summary>
    /// Persiste os canais de notificação (cifrados por DPAPI, conta do Windows) e
    /// envia mensagens para Telegram e e-mail (SMTP). Sem dependência de WPF.
    /// O envio é síncrono de propósito, para o processo curto da CLI.
    /// </summary>
    public class NotificacaoService
    {
        private const string TituloPadrao = "Memo";

        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "notificacoes.bin");

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly object Trava = new object();

        // ----------------- Persistência (DPAPI) -----------------

        public NotificacaoConfig Carregar()
        {
            lock (Trava)
            {
                try
                {
                    if (File.Exists(Caminho))
                    {
                        var protegido = File.ReadAllBytes(Caminho);
                        var json = Encoding.UTF8.GetString(
                            ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser));
                        return JsonConvert.DeserializeObject<NotificacaoConfig>(json) ?? new NotificacaoConfig();
                    }
                }
                catch
                {
                    // Arquivo ausente/corrompido/de outra conta: começa vazio.
                }
                return new NotificacaoConfig();
            }
        }

        public void Salvar(NotificacaoConfig config)
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
                var json = JsonConvert.SerializeObject(config);
                var protegido = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(Caminho, protegido);
            }
        }

        // ----------------- Envio -----------------

        public ResultadoCli EnviarTelegram(CanalTelegram c, string titulo, string mensagem)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.BotToken) || string.IsNullOrWhiteSpace(c.ChatId))
                return ResultadoCli.Falha("Telegram: bot token e chat id são obrigatórios");

            try
            {
                var url = $"https://api.telegram.org/bot{c.BotToken.Trim()}/sendMessage";
                var corpo = new Dictionary<string, string>
                {
                    ["chat_id"] = c.ChatId.Trim(),
                    ["text"] = MontarTexto(titulo, mensagem)
                };

                using (var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(corpo) })
                using (var resp = Http.Send(req))
                {
                    if (resp.IsSuccessStatusCode)
                        return ResultadoCli.Ok("Telegram enviado");

                    return ResultadoCli.Falha($"Telegram: HTTP {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ResultadoCli.Falha($"Telegram: {ex.Message}");
            }
        }

        public ResultadoCli EnviarEmail(CanalEmail c, string titulo, string mensagem)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Servidor) ||
                string.IsNullOrWhiteSpace(c.De) || string.IsNullOrWhiteSpace(c.Para))
                return ResultadoCli.Falha("E-mail: servidor, remetente e destinatário são obrigatórios");

            try
            {
                using (var msg = new MailMessage(c.De.Trim(), c.Para.Trim(),
                           string.IsNullOrWhiteSpace(titulo) ? TituloPadrao : titulo, mensagem ?? string.Empty))
                using (var smtp = new SmtpClient(c.Servidor.Trim(), c.Porta) { EnableSsl = c.UsarSsl })
                {
                    if (!string.IsNullOrWhiteSpace(c.Usuario))
                        smtp.Credentials = new NetworkCredential(c.Usuario.Trim(), c.Senha ?? string.Empty);

                    smtp.Send(msg);
                    return ResultadoCli.Ok("E-mail enviado");
                }
            }
            catch (Exception ex)
            {
                return ResultadoCli.Falha($"E-mail: {ex.Message}");
            }
        }

        // ----------------- Orquestração CLI -----------------

        /// <summary>
        /// Processa <c>memo notify [canal] [-t titulo] &lt;mensagem&gt;</c>.
        /// Sem canal, envia a todos os habilitados.
        /// </summary>
        public ResultadoCli Notificar(string[] args)
        {
            var tokens = args.Skip(1).ToList();

            // Canal opcional no início.
            string canal = null;
            if (tokens.Count > 0 &&
                (tokens[0].Equals("telegram", StringComparison.OrdinalIgnoreCase) ||
                 tokens[0].Equals("email", StringComparison.OrdinalIgnoreCase)))
            {
                canal = tokens[0].ToLowerInvariant();
                tokens.RemoveAt(0);
            }

            // Título opcional: -t <titulo> / --titulo <titulo>.
            var titulo = TituloPadrao;
            var iTitulo = tokens.FindIndex(t =>
                t.Equals("-t", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("--titulo", StringComparison.OrdinalIgnoreCase));
            if (iTitulo >= 0 && iTitulo + 1 < tokens.Count)
            {
                titulo = tokens[iTitulo + 1];
                tokens.RemoveRange(iTitulo, 2);
            }

            var mensagem = string.Join(" ", tokens).Trim();
            return Enviar(canal, titulo, mensagem);
        }

        /// <summary>
        /// Envia <paramref name="mensagem"/> para o <paramref name="canal"/> indicado
        /// (`telegram`/`email`) ou, se null, para todos os canais habilitados.
        /// </summary>
        public ResultadoCli Enviar(string canal, string titulo, string mensagem)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
                return ResultadoCli.Falha("Informe a mensagem: notify [canal] [-t titulo] <mensagem>");

            canal = canal?.Trim().ToLowerInvariant();
            if (canal == string.Empty) canal = null;

            var config = Carregar();

            var enviarTelegram = canal == null ? config.Telegram.Habilitado : canal == "telegram";
            var enviarEmail = canal == null ? config.Email.Habilitado : canal == "email";

            if (!enviarTelegram && !enviarEmail)
                return ResultadoCli.Falha(canal == null
                    ? "Nenhum canal habilitado. Configure em Configurações → Notificações."
                    : $"Canal \"{canal}\" não configurado.");

            var enviados = 0;
            var erros = new List<string>();

            if (enviarTelegram)
            {
                var r = EnviarTelegram(config.Telegram, titulo, mensagem);
                if (r.Sucesso) enviados++; else erros.Add(r.Mensagem);
            }
            if (enviarEmail)
            {
                var r = EnviarEmail(config.Email, titulo, mensagem);
                if (r.Sucesso) enviados++; else erros.Add(r.Mensagem);
            }

            if (enviados == 0)
                return ResultadoCli.Falha(string.Join(" | ", erros));

            var resumo = $"Notificação enviada ({enviados} canal(is))";
            return erros.Count == 0
                ? ResultadoCli.Ok(resumo)
                : ResultadoCli.Ok($"{resumo}; falhas: {string.Join(" | ", erros)}");
        }

        private static string MontarTexto(string titulo, string mensagem)
        {
            titulo = string.IsNullOrWhiteSpace(titulo) ? TituloPadrao : titulo.Trim();
            return $"{titulo}\n\n{mensagem}";
        }
    }
}
