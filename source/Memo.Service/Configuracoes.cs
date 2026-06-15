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

        /// <summary>Instância única carregada do disco (cria padrões se não existir).</summary>
        public static Configuracoes Atual => _atual ?? (_atual = Carregar());

        public string Tema { get; set; } = TemaEscuro;

        public int DuracaoSessaoMinutos { get; set; } = 15;

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
        }
    }
}
