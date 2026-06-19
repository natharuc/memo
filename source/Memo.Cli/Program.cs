using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Memo.Service;
using Memo.Service.Classes;
using Memo.Service.Lembretes;
using Memo.Service.Seguranca;
using Memo.Services;

namespace Memo.Cli
{
    // Códigos de saída (úteis para scripts).
    internal static class Codigo
    {
        public const int Ok = 0;
        public const int Erro = 1;
        public const int Trancado = 2;
        public const int NaoEncontrado = 3;
        public const int Uso = 64;
    }

    internal enum Formato { Texto, Json, Bytes }

    internal static class Program
    {
        private static int Main(string[] argv)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var args = new Args(argv);
            if (args.Comando == null || args.Comando == "help" || args.Tem("--help") || args.Tem("-h"))
            {
                Ajuda();
                return Codigo.Ok;
            }

            try
            {
                switch (args.Comando)
                {
                    case "get": return Get(args);
                    case "set": return Set(args);
                    case "list": case "ls": return List(args);
                    case "del": case "rm": case "delete": return Del(args);
                    case "remember": case "lembrar": case "lembrete": return Remember(args);
                    case "pass": return Pass(args);
                    case "guid": return Guid(args);
                    case "unlock": return Unlock(args);
                    case "lock": return Lock();
                    case "migrar": case "migrate": return Migrar();
                    case "version": case "--version": case "-v": return Versao();
                    default:
                        Erro($"Comando desconhecido: {args.Comando}. Use 'memo-cli help'.");
                        return Codigo.Uso;
                }
            }
            catch (Exception ex)
            {
                Erro(ex.Message);
                return Codigo.Erro;
            }
        }

        // ----------------- comandos -----------------

        private static int Get(Args a)
        {
            var key = a.Positional();
            if (string.IsNullOrWhiteSpace(key)) { Erro("Uso: memo-cli get <chave> [--json|--text|--bytes|--copy]"); return Codigo.Uso; }

            var svc = new MemoService();
            if (!Destrancar(svc.Cofre, a)) return Codigo.Trancado;

            var doc = svc.Carregar(key);
            if (doc == null) { Erro($"\"{key}\" não encontrado"); return Codigo.NaoEncontrado; }

            if (a.Tem("--copy"))
            {
                svc.CopiarValor(doc);
                Console.Error.WriteLine($"\"{key}\" copiado para a área de transferência");
                return Codigo.Ok;
            }

            switch (a.Formato())
            {
                case Formato.Json:
                    EscreverJson(new { key = doc.Key, value = doc.Value });
                    break;
                case Formato.Bytes:
                    EscreverBytes(Encoding.UTF8.GetBytes(doc.Value ?? string.Empty));
                    break;
                default:
                    Console.Out.Write(doc.Value ?? string.Empty);
                    Console.Out.Write(Environment.NewLine);
                    break;
            }
            return Codigo.Ok;
        }

        private static int Set(Args a)
        {
            var pos = a.Positionals();
            string key, value;

            if (a.TemValor("--value", out var v)) { key = string.Join(" ", pos); value = v; }
            else if (a.Tem("--stdin")) { key = string.Join(" ", pos); value = Console.In.ReadToEnd().TrimEnd('\r', '\n'); }
            else if (pos.Count >= 2)
            {
                // "chave" "valor" (use aspas). O valor pode conter '=' à vontade.
                // Também aceita "chave = valor" (com '=' como token solto).
                if (pos[1] == "=" && pos.Count >= 3) { key = pos[0]; value = string.Join(" ", pos.Skip(2)); }
                else { key = pos[0]; value = string.Join(" ", pos.Skip(1)); }
            }
            else if (pos.Count == 1 && pos[0].Contains('='))
            {
                var i = pos[0].IndexOf('=');               // forma "chave=valor"
                key = pos[0].Substring(0, i).Trim();
                value = pos[0].Substring(i + 1);
            }
            else if (pos.Count == 1) { key = pos[0]; value = string.Empty; }
            else { Erro("Uso: memo-cli set <chave> <valor> | <chave>=<valor> | <chave> --value <v> | <chave> --stdin"); return Codigo.Uso; }

            if (string.IsNullOrWhiteSpace(key)) { Erro("Informe a chave."); return Codigo.Uso; }

            var svc = new MemoService();
            if (!Destrancar(svc.Cofre, a)) return Codigo.Trancado;

            svc.Salvar(new Documento(key, value));
            if (a.Formato() == Formato.Json) EscreverJson(new { key, saved = true });
            else Console.Error.WriteLine($"\"{key}\" salvo");
            return Codigo.Ok;
        }

        private static int List(Args a)
        {
            var svc = new MemoService();
            if (!Destrancar(svc.Cofre, a)) return Codigo.Trancado;

            var chaves = svc.GetDocumentos().Select(d => d.Key).ToList();

            if (a.Formato() == Formato.Json)
                EscreverJson(chaves);
            else
                foreach (var k in chaves) Console.Out.WriteLine(k);
            return Codigo.Ok;
        }

        private static int Del(Args a)
        {
            var key = a.Positional();
            if (string.IsNullOrWhiteSpace(key)) { Erro("Uso: memo-cli del <chave>"); return Codigo.Uso; }

            var svc = new MemoService();
            if (!Destrancar(svc.Cofre, a)) return Codigo.Trancado;

            var doc = svc.Carregar(key);
            if (doc == null) { Erro($"\"{key}\" não encontrado"); return Codigo.NaoEncontrado; }

            svc.Deletar(doc);
            if (a.Formato() == Formato.Json) EscreverJson(new { key, deleted = true });
            else Console.Error.WriteLine($"\"{key}\" excluído");
            return Codigo.Ok;
        }

        private static int Remember(Args a)
        {
            var entrada = string.Join(" ", a.Positionals());
            var p = ParserLembrete.Analisar(entrada, DateTime.Now);
            if (!p.Ok) { Erro(p.Erro); return Codigo.Uso; }

            new LembreteService().Adicionar(p.Texto, p.Proximo, p.RepetirMinutos);

            if (a.Formato() == Formato.Json)
                EscreverJson(new { text = p.Texto, next = p.Proximo, repeatMinutes = p.RepetirMinutos });
            else
                Console.Error.WriteLine($"Lembrete \"{p.Texto}\" criado");
            return Codigo.Ok;
        }

        private static int Pass(Args a)
        {
            var senha = GeradorSenha.Gerar(Configuracoes.Atual.Senha);
            if (string.IsNullOrEmpty(senha)) { Erro("Nenhum tipo de caractere habilitado nas preferências."); return Codigo.Erro; }

            var key = a.Positional();
            if (!string.IsNullOrWhiteSpace(key))
            {
                var svc = new MemoService();
                if (!Destrancar(svc.Cofre, a)) return Codigo.Trancado;
                svc.Salvar(new Documento(key, senha));
            }

            if (a.Formato() == Formato.Json) EscreverJson(new { password = senha, key = string.IsNullOrWhiteSpace(key) ? null : key });
            else if (a.Formato() == Formato.Bytes) EscreverBytes(Encoding.UTF8.GetBytes(senha));
            else { Console.Out.Write(senha); Console.Out.Write(Environment.NewLine); }
            return Codigo.Ok;
        }

        private static int Guid(Args a)
        {
            var g = System.Guid.NewGuid().ToString();
            if (a.Formato() == Formato.Json) EscreverJson(new { guid = g });
            else { Console.Out.Write(g); Console.Out.Write(Environment.NewLine); }
            return Codigo.Ok;
        }

        private static int Unlock(Args a)
        {
            var cofre = new Cofre(MemoService.DiretorioPadrao);
            if (cofre.TentarDestrancarPelaSessao()) { Console.Error.WriteLine("Já destrancado (sessão válida)."); return Codigo.Ok; }

            var senha = ObterSenha(a);
            if (senha == null) { Erro("Senha necessária (use --password, MEMO_PASSWORD ou rode num terminal)."); return Codigo.Trancado; }

            if (!cofre.Destrancar(senha)) { Erro("Senha incorreta."); return Codigo.Trancado; }
            Console.Error.WriteLine("Cofre destrancado.");
            return Codigo.Ok;
        }

        private static int Lock()
        {
            new Cofre(MemoService.DiretorioPadrao).Trancar();
            Console.Error.WriteLine("Cofre trancado.");
            return Codigo.Ok;
        }

        private static int Migrar()
        {
            var svc = new MemoService();
            if (!svc.Cofre.TentarDestrancarPelaSessao())
            {
                Erro("Cofre trancado. Rode 'memo-cli unlock' primeiro.");
                return Codigo.Trancado;
            }
            var r = svc.Migrar();
            Console.Error.WriteLine($"{r.Migrados.Count} migrado(s), {r.JaAtualizados} ok, {r.Falhas.Count} em quarentena");
            return r.Falhas.Any() ? Codigo.Erro : Codigo.Ok;
        }

        private static int Versao()
        {
            var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
            Console.Out.WriteLine(v);
            return Codigo.Ok;
        }

        // ----------------- cofre / senha -----------------

        private static bool Destrancar(Cofre cofre, Args a)
        {
            if (cofre.TentarDestrancarPelaSessao()) return true;

            var senha = ObterSenha(a);
            if (senha != null && cofre.Destrancar(senha)) return true;

            Erro("Cofre trancado. Rode 'memo-cli unlock', ou passe --password / MEMO_PASSWORD.");
            return false;
        }

        private static string ObterSenha(Args a)
        {
            if (a.TemValor("--password", out var p) && !string.IsNullOrEmpty(p)) return p;

            var env = Environment.GetEnvironmentVariable("MEMO_PASSWORD");
            if (!string.IsNullOrEmpty(env)) return env;

            // Terminal interativo: pergunta com máscara.
            if (!Console.IsInputRedirected)
                return LerSenhaMascarada("Senha-mestra: ");

            return null;
        }

        private static string LerSenhaMascarada(string prompt)
        {
            Console.Error.Write(prompt);
            var sb = new StringBuilder();
            while (true)
            {
                var k = Console.ReadKey(intercept: true);
                if (k.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); break; }
                if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
                if (!char.IsControl(k.KeyChar)) sb.Append(k.KeyChar);
            }
            return sb.ToString();
        }

        // ----------------- saída -----------------

        private static void EscreverJson(object o)
        {
            var json = JsonSerializer.Serialize(o, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            Console.Out.WriteLine(json);
        }

        private static void EscreverBytes(byte[] bytes)
        {
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(bytes, 0, bytes.Length);
            stdout.Flush();
        }

        private static void Erro(string msg) => Console.Error.WriteLine("erro: " + msg);

        private static void Ajuda()
        {
            Console.Out.WriteLine(@"memo-cli — cofre de segredos via linha de comando

Uso: memo-cli <comando> [args] [--json|--text|--bytes]

Comandos:
  get <chave>             Lê um segredo (padrão: texto no stdout; --copy = clipboard)
  set <chave> <valor>     Cria/atualiza. Também: <chave>=<valor>, --value <v>, --stdin
  list                    Lista as chaves
  del <chave>             Exclui um segredo
  remember <texto/quando> Cria um lembrete (ex.: ""ver tarefa 10:00 tomorrow"")
  pass [chave]            Gera uma senha (e salva, se der uma chave)
  guid                    Gera um GUID
  unlock / lock           Destranca (pede senha) / tranca o cofre
  migrar                  Recifra documentos antigos
  version                 Versão

Saída:
  --text   (padrão) valor cru no stdout
  --json   objeto JSON
  --bytes  bytes crus (binário) no stdout
  --copy   copia para a área de transferência (em vez de imprimir)

Senha (quando o cofre está trancado):
  --password <senha> | variável MEMO_PASSWORD | prompt no terminal | 'memo-cli unlock'

Exit codes: 0 ok · 1 erro · 2 trancado · 3 não encontrado · 64 uso");
        }
    }

    /// <summary>Parser simples de argumentos: comando, posicionais e flags.</summary>
    internal sealed class Args
    {
        private readonly List<string> _pos = new List<string>();
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Args(string[] argv)
        {
            for (var i = 0; i < argv.Length; i++)
            {
                var t = argv[i];
                if (t.StartsWith("--"))
                {
                    var eq = t.IndexOf('=');
                    if (eq > 0) { _valores[t.Substring(0, eq)] = t.Substring(eq + 1); continue; }

                    // flags que consomem o próximo token
                    if ((t == "--password" || t == "--value") && i + 1 < argv.Length)
                    {
                        _valores[t] = argv[++i];
                        continue;
                    }
                    _flags.Add(t);
                }
                else if (t.StartsWith("-") && t.Length > 1 && !char.IsDigit(t[1]))
                {
                    _flags.Add(t);
                }
                else
                {
                    _pos.Add(t);
                }
            }
        }

        public string Comando => _pos.Count > 0 ? _pos[0].ToLowerInvariant() : null;

        public bool Tem(string flag) => _flags.Contains(flag);

        public bool TemValor(string nome, out string valor) => _valores.TryGetValue(nome, out valor);

        /// <summary>Primeiro posicional após o comando (vazio se não houver).</summary>
        public string Positional() => string.Join(" ", Positionals());

        /// <summary>Posicionais após o comando.</summary>
        public List<string> Positionals() => _pos.Skip(1).ToList();

        public Formato Formato()
        {
            if (Tem("--json")) return Memo.Cli.Formato.Json;
            if (Tem("--bytes")) return Memo.Cli.Formato.Bytes;
            return Memo.Cli.Formato.Texto;
        }
    }
}
