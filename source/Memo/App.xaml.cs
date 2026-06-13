using System;
using System.Linq;
using System.Windows;
using Memo.Service.Repositorio;
using Memo.Services;

namespace Memo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var service = new MemoService();
            var cofre = service.Cofre;

            // Reaproveita a sessão recente; senão pede para criar/destrancar o cofre.
            if (!cofre.TentarDestrancarPelaSessao())
            {
                var ok = JanelaSenha.Solicitar(cofre);
                if (!ok)
                {
                    Shutdown();
                    return;
                }
            }

            if (e.Args.Length == 0)
            {
                // Passada de auto-reparo: recifra o que for antigo e isola o que não abrir.
                var resultado = service.Migrar();
                if (resultado.HouveMudanca)
                    MessageBox.Show(TextoRelatorio(resultado), "Memo — manutenção",
                        MessageBoxButton.OK,
                        resultado.Falhas.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);

                new JanelaPrincipal(service).Show();
                return;
            }

            ExecutarLinhaDeComando(service, e.Args);
        }

        private void ExecutarLinhaDeComando(MemoService service, string[] args)
        {
            ResultadoCli resultado;
            try
            {
                var acao = args[0].ToLowerInvariant();
                if (acao == "get")
                    resultado = service.ProcessarGet(args);
                else if (acao == "set")
                    resultado = service.ProcessarSet(args);
                else if (acao == "migrar")
                    resultado = ExecutarMigracao(service);
                else
                    resultado = ResultadoCli.Falha($"Comando desconhecido: {args[0]}");
            }
            catch (Exception ex)
            {
                resultado = ResultadoCli.Falha(ex.Message);
            }

            // Mostra um aviso discreto e encerra quando ele some.
            Toast.Mostrar(resultado.Mensagem, resultado.Sucesso);
        }

        private static ResultadoCli ExecutarMigracao(MemoService service)
        {
            var r = service.Migrar();
            var msg = $"{r.Migrados.Count} migrado(s), {r.JaAtualizados} ok, {r.Falhas.Count} em quarentena";
            return r.Falhas.Any() ? ResultadoCli.Falha(msg) : ResultadoCli.Ok(msg);
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
