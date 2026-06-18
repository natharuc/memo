using Memo.Service;
using Memo.Service.Atualizacao;
using Memo.Service.Lembretes;
using Memo.Service.Repositorio;
using Memo.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Memo
{
    public partial class App : Application
    {
        private MemoService _service;
        private JanelaPrincipal _janela;
        private bool _encerrando;
        private bool _avisouBandeja;

        // Bandeja + agendador de lembretes
        private InstanciaUnica _instancia;
        private System.Windows.Forms.NotifyIcon _bandeja;
        private DispatcherTimer _agendador;
        private readonly LembreteService _lembretes = new LembreteService();
        private readonly HashSet<string> _popupsAbertos = new HashSet<string>();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Tema.Aplicar(Configuracoes.Atual.Tema);
            _service = new MemoService();

            var args = e.Args;

            // Pós-atualização: espera o processo antigo sair (libera o mutex de
            // instância única) e segue como uma abertura normal de bandeja.
            if (args.Length >= 2 && args[0] == "--apos-atualizacao" && int.TryParse(args[1], out var pidAntigo))
            {
                EsperarProcessoSair(pidAntigo, TimeSpan.FromSeconds(10));
                args = Array.Empty<string>();
            }

            var modoBandeja = args.Length == 0 || (args.Length == 1 && args[0] == "--tray");

            if (!modoBandeja)
            {
                // Comando de linha de comando (processo curto; sai sozinho).
                ExecutarComando(args);
                return;
            }

            // ---- Modo app/bandeja: instância única ----
            _instancia = new InstanciaUnica();
            if (!_instancia.Adquirir())
            {
                _instancia.SinalizarMostrar(); // pede para a instância existente abrir
                Shutdown();
                return;
            }
            _instancia.MostrarSolicitado += () => Dispatcher.Invoke(MostrarJanela);

            ShutdownMode = ShutdownMode.OnExplicitShutdown; // a bandeja mantém o app vivo
            AtualizadorService.LimparResiduos();
            IniciarBandeja();
            IniciarAgendador();

            // `--tray` (início com o Windows) fica só na bandeja; sem args, abre a janela.
            if (!(args.Length == 1 && args[0] == "--tray"))
                MostrarJanela();
        }

        // ----------------- Janela principal -----------------

        private void MostrarJanela()
        {
            if (_janela != null && _janela.IsLoaded)
            {
                if (!_janela.IsVisible) _janela.Show();
                if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
                _janela.Activate();
                return;
            }

            var cofre = _service.Cofre;
            if (!cofre.TentarDestrancarPelaSessao() && !JanelaSenha.Solicitar(cofre))
                return; // sem destrancar, não abre

            // Auto-reparo na primeira abertura.
            var resultado = _service.Migrar();
            if (resultado.HouveMudanca)
                JanelaDialogo.Informar(null, "Memo — manutenção", TextoRelatorio(resultado));

            _janela = new JanelaPrincipal(_service);
            _janela.Closing += (s, ev) =>
            {
                if (_encerrando) return;

                _janela.campoBusca.Clear(); // limpa busca para não mostrar resultados confusos ao reabrir

                ev.Cancel = true;   // X não fecha o app: esconde na bandeja
                _janela.Hide();

                if (!_avisouBandeja)
                {
                    _avisouBandeja = true;
                    try
                    {
                        _bandeja?.ShowBalloonTip(6000, "Memo continua na bandeja",
                            "Ele segue rodando aqui para avisar seus lembretes. Para encerrar de vez, use Sair no menu da bandeja.",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                    catch { }
                }
            };
            _janela.Show();

            VerificarAtualizacaoEmBackground(_janela);
        }

        // ----------------- Bandeja -----------------

        private void IniciarBandeja()
        {
            _bandeja = new System.Windows.Forms.NotifyIcon
            {
                Icon = ObterIcone(),
                Text = "Memo",
                Visible = true
            };
            _bandeja.DoubleClick += (_, __) => Dispatcher.Invoke(MostrarJanela);

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir Memo", null, (_, __) => Dispatcher.Invoke(MostrarJanela));
            menu.Items.Add("Lembretes…", null, (_, __) => Dispatcher.Invoke(AbrirLembretes));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Sair", null, (_, __) => Dispatcher.Invoke(Encerrar));
            _bandeja.ContextMenuStrip = menu;
        }

        private void AbrirLembretes()
        {
            var janela = new JanelaLembretes(_lembretes);
            if (_janela != null && _janela.IsVisible) janela.Owner = _janela;
            janela.Show();
            janela.Activate();
        }

        private void Encerrar()
        {
            _encerrando = true;
            _agendador?.Stop();
            if (_bandeja != null) { _bandeja.Visible = false; _bandeja.Dispose(); }
            _instancia?.Dispose();
            Shutdown();
        }

        private static void EsperarProcessoSair(int pid, TimeSpan timeout)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.WaitForExit((int)timeout.TotalMilliseconds);
            }
            catch
            {
                // Processo já saiu (ou não existe) — segue normalmente.
            }
        }

        private static System.Drawing.Icon ObterIcone()
        {
            // Carrega o memo.ico empacotado e escolhe o frame do tamanho do ícone de bandeja.
            try
            {
                var stream = GetResourceStream(new Uri("pack://application:,,,/memo.ico"))?.Stream;
                if (stream != null)
                    return new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
            }
            catch { }

            try { return System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName); }
            catch { return System.Drawing.SystemIcons.Application; }
        }

        // ----------------- Agendador de lembretes -----------------

        private void IniciarAgendador()
        {
            _agendador = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _agendador.Tick += (_, __) => VerificarLembretes();
            _agendador.Start();
            VerificarLembretes(); // pega atrasados logo ao iniciar
        }

        private void VerificarLembretes()
        {
            foreach (var lembrete in _lembretes.Devidos(DateTime.Now))
            {
                if (_popupsAbertos.Contains(lembrete.Id)) continue; // já tem popup aberto
                _popupsAbertos.Add(lembrete.Id);
                MostrarPopupLembrete(lembrete);
            }
        }

        private void MostrarPopupLembrete(Lembrete lembrete)
        {
            var id = lembrete.Id;
            var popup = new JanelaLembrete(lembrete.Texto);
            popup.Concluido += () => _lembretes.Concluir(id);
            popup.Adiado += min => _lembretes.Adiar(id, min);
            popup.Closed += (_, __) => _popupsAbertos.Remove(id);
            popup.Show();

            // Reforço: também notifica na bandeja, caso o popup passe despercebido.
            try { _bandeja?.ShowBalloonTip(8000, "Lembrete", lembrete.Texto, System.Windows.Forms.ToolTipIcon.Info); }
            catch { }
        }

        // ----------------- Linha de comando -----------------

        private void ExecutarComando(string[] args)
        {
            var cofre = _service.Cofre;
            var cmd = args[0].ToLowerInvariant();

            // Bloqueio: não passam pelo destravamento automático.
            if (cmd == "lock")
            {
                cofre.Trancar();
                Toast.Mostrar("Cofre trancado", true);
                return;
            }
            if (cmd == "unlock")
            {
                cofre.Trancar();
                var ok = JanelaSenha.Solicitar(cofre);
                Toast.Mostrar(ok ? "Cofre destrancado" : "Destravamento cancelado", ok);
                return;
            }

            // Lembretes não são segredo: não exigem o cofre destrancado.
            if (cmd == "remember" || cmd == "lembrar" || cmd == "lembrete")
            {
                var r = _service.ProcessarRemember(args);
                Toast.Mostrar(r.Mensagem, r.Sucesso);
                return;
            }

            // Demais comandos precisam do cofre aberto.
            if (!cofre.TentarDestrancarPelaSessao() && !JanelaSenha.Solicitar(cofre))
            {
                Shutdown();
                return;
            }

            if (cmd == "new")
            {
                ExecutarNovo();
                return;
            }

            ResultadoCli resultado;
            try
            {
                if (cmd == "get") resultado = _service.ProcessarGet(args);
                else if (cmd == "set") resultado = _service.ProcessarSet(args);
                else if (cmd == "migrar") resultado = ExecutarMigracao();
                else if (cmd == "guid") resultado = _service.ProcessarGuid();
                else if (cmd == "pass") resultado = _service.ProcessarPass(args);
                else resultado = ResultadoCli.Falha($"Comando desconhecido: {args[0]}");
            }
            catch (Exception ex)
            {
                resultado = ResultadoCli.Falha(ex.Message);
            }

            Toast.Mostrar(resultado.Mensagem, resultado.Sucesso);
        }

        private void ExecutarNovo()
        {
            var doc = JanelaEditar.Criar(null);
            if (doc == null)
            {
                Shutdown();
                return;
            }

            _service.Salvar(doc);
            Toast.Mostrar($"\"{doc.Key}\" criado", true);
        }

        private ResultadoCli ExecutarMigracao()
        {
            var r = _service.Migrar();
            var msg = $"{r.Migrados.Count} migrado(s), {r.JaAtualizados} ok, {r.Falhas.Count} em quarentena";
            return r.Falhas.Any() ? ResultadoCli.Falha(msg) : ResultadoCli.Ok(msg);
        }

        private static async void VerificarAtualizacaoEmBackground(Window dono)
        {
            try
            {
                var versao = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
                var atualizador = new AtualizadorService(versao);

                var info = await atualizador.VerificarAsync();
                if (info != null)
                    JanelaAtualizacao.Mostrar(dono, atualizador, info);
            }
            catch
            {
                // A verificação de atualização nunca deve atrapalhar o uso do app.
            }
        }

        private static string TextoRelatorio(MigracaoResultado r)
        {
            var linhas = new System.Text.StringBuilder();
            linhas.AppendLine($"Documentos recifrados para o formato novo: {r.Migrados.Count}");
            linhas.AppendLine($"Já atualizados: {r.JaAtualizados}");

            if (r.Falhas.Any())
            {
                linhas.AppendLine();
                linhas.AppendLine($"Não foi possível abrir {r.Falhas.Count} arquivo(s). Eles foram movidos para:");
                linhas.AppendLine(r.PastaFalhas);
                linhas.AppendLine();
                linhas.AppendLine(string.Join(", ", r.Falhas));
            }

            return linhas.ToString().TrimEnd();
        }
    }
}
