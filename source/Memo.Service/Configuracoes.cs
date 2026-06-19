using System;
using System.IO;
using Newtonsoft.Json;

namespace Memo.Service
{
    /// <summary>
    /// Preferências do usuário (tema e duração da sessão), gravadas em
    /// <c>%LOCALAPPDATA%\Memo\config.json</c>. Não guarda nada sensível.
    /// </summary>
    public class Configuracoes
    {
        public const string TemaEscuro = "Escuro";
        public const string TemaClaro = "Claro";

        private const int MinutosMinimo = 1;
        private const int MinutosMaximo = 60 * 24 * 7; // 7 dias

        private static readonly string Caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Memo", "config.json");

        private static Configuracoes _atual;
        private static DateTime _carregadoEm;

        /// <summary>
        /// Configuração atual. Recarrega do disco se o arquivo mudou (assim a GUI e o
        /// memo-cli sempre veem a mesma config, mesmo alterada por outro processo).
        /// </summary>
        public static Configuracoes Atual
        {
            get
            {
                var mtime = MtimeArquivo();
                if (_atual == null || mtime != _carregadoEm)
                {
                    _atual = Carregar();
                    _carregadoEm = mtime;
                }
                return _atual;
            }
        }

        private static DateTime MtimeArquivo()
        {
            try { return File.Exists(Caminho) ? File.GetLastWriteTimeUtc(Caminho) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        public string Tema { get; set; } = TemaEscuro;

        /// <summary>Pasta onde ficam os documentos cifrados. Vazio = ainda não escolhida.</summary>
        public string DiretorioDocumentos { get; set; }

        public int DuracaoSessaoMinutos { get; set; } = 15;

        /// <summary>Preferências do gerador de senha (reusadas na UI e na CLI).</summary>
        public OpcoesSenha Senha { get; set; } = new OpcoesSenha();

        [JsonIgnore]
        public TimeSpan DuracaoSessao =>
            TimeSpan.FromMinutes(Math.Min(MinutosMaximo, Math.Max(MinutosMinimo, DuracaoSessaoMinutos)));

        public static Configuracoes Carregar()
        {
            try
            {
                if (File.Exists(Caminho))
                    return JsonConvert.DeserializeObject<Configuracoes>(File.ReadAllText(Caminho))
                           ?? new Configuracoes();
            }
            catch
            {
                // Config corrompida: volta ao padrão em vez de quebrar o app.
            }

            return new Configuracoes();
        }

        public void Salvar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
            File.WriteAllText(Caminho, JsonConvert.SerializeObject(this, Formatting.Indented));
            _atual = this;
            _carregadoEm = MtimeArquivo();
        }
    }
}
