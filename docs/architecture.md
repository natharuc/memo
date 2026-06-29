# Arquitetura

## Visão geral

Memo tem três projetos:

- **`source/Memo`** — aplicativo **WPF (.NET 8, `net8.0-windows`)**. Roda em modo
  **bandeja** (ícone na área de notificação, dispara lembretes) e abre a janela
  principal. O mesmo `Memo.exe` também age como CLI quando recebe argumentos
  (copia para a área de transferência e mostra um `Toast`), mas **não** captura
  stdout — para isso existe o `Memo.Cli`.
- **`source/Memo.Cli`** — CLI **console** (`memo-cli.exe`): saída no **stdout**,
  **exit codes**, sem janela. Interface recomendada para automação/scripts.
- **`source/Memo.Service`** — biblioteca (`net8.0-windows`) com toda a lógica de
  negócio: cofre, criptografia, repositório de documentos e lembretes. Não
  depende de WPF — os dois front-ends chamam só ela.

A UI é "burra": orquestra janelas e delega tudo para `MemoService` (e
`LembreteService`).

```
┌──────────────── Memo (WPF + bandeja) ─────────────┐   ┌──── Memo.Cli ────┐
│ App.xaml.cs    roteamento: bandeja / args / senha │   │ Program.cs       │
│ Janela*.xaml   telas (principal, senha, editar,   │   │ console: stdout, │
│                config, lembretes, atualização…)   │   │ exit codes,      │
│ Toast/Nativo   aviso flutuante + barra escura     │   │ --json/--bytes   │
└────────────────────────┬──────────────────────────┘   └────────┬─────────┘
                         │ usam                                    │
┌────────────────────────▼──────── Memo.Service ──────────────────▼─────────┐
│ MemoService          fachada de negócio (GUI/CLI chamam isto)             │
│ Cofre                ciclo de vida do cofre + sessão (DPAPI)              │
│ CryptoCofre          primitivas de cripto (AES-GCM, PBKDF2, legado)       │
│ DocumentoRepository  IO dos arquivos + migração + sanitização            │
│ LembreteService      lembretes (lembretes.json) — não é segredo           │
│ HistoricoExecutar    higiene: limpa "memo set" do histórico do Win+R      │
│ Configuracoes        preferências (tema, duração da sessão, gerador)      │
│ AtualizadorService   auto-update via GitHub Releases                      │
└───────────────────────────────────────────────────────────────────────────┘
                         │ lê/escreve
                arquivos cifrados no disco (pasta-cofre)
```

## Componentes (Memo.Service)

### `MemoService` — `source/Memo.Service/MemoService.cs`
Fachada única usada pela GUI e pela CLI. Cria um `Cofre` e um
`DocumentoRepository` para um diretório.
- `Cofre` (propriedade) — expõe o cofre para destrancar.
- `GetDocumentos()`, `Carregar(key)`, `Salvar(doc)`, `Deletar(doc)`.
- `Migrar()` — recifra/migra (ver `DocumentoRepository.Migrar`).
- `CopiarValor(doc)` — copia para o clipboard (via TextCopy).
- `ProcessarGet(args)`, `ProcessarSet(args)` — entradas de linha de comando,
  retornam `ResultadoCli { Sucesso, Mensagem }`.
- `DiretorioConfigurado` — pasta dos documentos, resolvida por `MEMO_DIR` (env) →
  `Configuracoes.DiretorioDocumentos`. Null se ainda não escolhida.

### `Cofre` — `source/Memo.Service/Seguranca/Cofre.cs`
Estado e ciclo de vida do cofre. Mantém a chave-mestra em memória quando
destrancado. Detalhes de formato em [security.md](security.md).
- `Inicializado`, `Destrancado`.
- `Inicializar(senha)` — cria um cofre novo (gera salt, grava `vault.json`).
- `Destrancar(senha)` — valida a senha contra o verificador.
- `TentarDestrancarPelaSessao()` — usa o cache de sessão **se** ele abrir o
  cofre atual (`ChaveAbreCofre`).
- `Cifrar(texto)` / `Decifrar(base64)` / `TentarDecifrar(...)`.
- `Trancar()` — esquece a chave e apaga a sessão.

### `CryptoCofre` — `source/Memo.Service/Seguranca/CryptoCofre.cs`
Primitivas puras de criptografia, sem estado:
- `DerivarChave(senha, salt, iteracoes)` — PBKDF2-SHA256.
- `Cifrar(texto, chave)` / `TentarDecifrar(base64, chave, out)` — AES-256-GCM.
- `TentarDecifrarLegado(...)` / `EhFormatoLegado(...)` — leitura do formato antigo.

### `DocumentoRepository` — `source/Memo.Service/Repositorio/DocumentoRepository.cs`
IO dos documentos (um arquivo por chave).
- `CarregarTodos()` — **resiliente**: pula arquivos que não abrem, nunca lança.
- `Carregar(key)`, `Salvar(doc)`.
- `Deletar(key)` — **exclusão definitiva** (remove o arquivo; não há lixeira).
- `Migrar()` — recifra legado→novo e move o que não abre para `falhas/`
  (retorna `MigracaoResultado`).
- `SanitizarChave(key)` — rejeita separadores de caminho (anti path-traversal).

### `Documento` — `source/Memo.Service/Classes/Documento.cs`
POCO `{ string Key; string Value; }`. Serializado em JSON e então cifrado.

### `LembreteService` — `source/Memo.Service/Lembretes/LembreteService.cs`
Persistência dos lembretes em `%LOCALAPPDATA%\Memo\lembretes.json` (**não é
segredo** — não passa pelo cofre). `ParserLembrete` interpreta a linguagem
natural. Ver [Lembretes](#lembretes-bandeja) e [cli.md](cli.md).

### `HistoricoExecutar` — `source/Memo.Service/Seguranca/HistoricoExecutar.cs`
Higiene de segurança: apaga os comandos `memo set ...` do histórico do "Executar"
(Win+R / RunMRU), onde o segredo apareceria em texto puro. Best-effort, nunca
quebra o app. Ver [security.md](security.md).

## Componentes (Memo / WPF)

- **`App.xaml.cs`** — `OnStartup` faz o roteamento e mantém a **bandeja** e o
  **agendador de lembretes** (ver [Fluxo](#fluxo-de-inicialização)).
- **`InstanciaUnica.cs`** — mutex + sinal para garantir **uma só instância** na
  bandeja (a 2ª execução pede para a 1ª aparecer e sai).
- **`JanelaPrincipal`** — lista, busca, painel de detalhes, badge de sessão,
  ações (copiar, mostrar/ocultar, editar, **excluir definitivamente**, novo).
- **`JanelaSenha`** — cria (primeira vez) ou destranca o cofre.
- **`JanelaEditar`** — cria/edita um documento.
- **`JanelaGerarSenha`** — gerador de senha (preferências reusadas pelo `pass`).
- **`JanelaConfiguracoes`** — preferências (tema, duração da sessão) e atalhos.
- **`JanelaLembrete` / `JanelaLembretes`** — popup de um lembrete (concluir/
  adiar) e a lista de lembretes.
- **`JanelaAtualizacao`** — oferece baixar/instalar uma nova versão.
- **`JanelaDialogo`** — diálogo temizado (confirmar/informar), no lugar do
  `MessageBox` do Windows.
- **`Toast`** — aviso flutuante usado no modo CLI da GUI.
- **`Tema.cs` + `Tema.xaml` + `PaletaEscura.xaml`/`PaletaClara.xaml`** — estilos
  em `Tema.xaml`, cores nas paletas; `Tema.Aplicar(...)` troca a paleta em runtime
  (tema claro/escuro). Ver [ui.md](ui.md).
- **`Nativo.cs`** — P/Invoke: `DwmSetWindowAttribute` (barra de título escura) e
  `TrazerParaFrente` (foreground confiável via `AttachThreadInput` +
  `SetForegroundWindow`, p/ a janela abrir focada mesmo vinda da bandeja).
- **`Som.cs`** — toca o som de notificação dos lembretes.

Detalhes de comportamento em [ui.md](ui.md).

## Fluxo de inicialização

`App.OnStartup` (`source/Memo/App.xaml.cs`):

1. Limpa o histórico do Win+R (`HistoricoExecutar`) e aplica o tema.
2. Se não há pasta configurada (`DiretorioConfigurado == null`), pergunta ao
   usuário (`EscolherPastaDocumentos`) e salva. Depois cria `MemoService`.
3. **`--apos-atualizacao <pid>`** → espera o processo antigo sair (libera o mutex
   de instância única) e segue como uma abertura normal.
4. **Com argumentos** (que não `--tray`) → modo CLI: executa
   `get`/`set`/`new`/`pass`/`guid`/`migrar`/`lock`/`unlock`/`remember`, mostra um
   `Toast` e sai. O cofre é destrancado pela sessão ou pela `JanelaSenha`
   (lembretes e `lock` não exigem cofre aberto).
5. **Sem argumentos** ou **`--tray`** → modo bandeja: adquire a **instância única**
   (se já houver outra, manda mostrar e sai), liga a bandeja e o **agendador de
   lembretes**, e limpa resíduos de update. Com `--tray` (início com o Windows)
   fica só na bandeja; senão chama `MostrarJanela`.

A **migração de auto-reparo** (`service.Migrar()`, com relatório se mexer em algo)
roda em `MostrarJanela`, na primeira vez que a janela principal é aberta — depois
de destrancar o cofre.

## Fluxo de dados de um segredo

```
Salvar:   Documento → JSON → AES-256-GCM(chave) → base64 → arquivo "<Key>"
Carregar: arquivo "<Key>" → base64 → AES-256-GCM⁻¹(chave) → JSON → Documento
```

A chave vem de `PBKDF2(senha, salt do vault.json)`. Ver [security.md](security.md).

## Lembretes (bandeja)

Lembretes **não são segredos** — não passam pelo cofre. Ficam em
`%LOCALAPPDATA%\Memo\lembretes.json` (`LembreteService`), com texto, próximo
disparo e, se recorrente, o intervalo em minutos.

- **Criação**: `memo remember <texto/quando>` (ou `memo-cli remember`) →
  `ParserLembrete` interpreta linguagem natural PT/EN (ver [cli.md](cli.md)).
- **Disparo**: a instância de bandeja roda um `DispatcherTimer` (~20 s,
  `App.IniciarAgendador`) que pega os lembretes **devidos** e abre um
  `JanelaLembrete` (+ balão na bandeja e som). Concluir um recorrente reagenda o
  próximo; adiar (soneca) joga o disparo para daqui a N min.
- Sem instância de bandeja rodando, o lembrete só aparece quando o app abrir.

## Notas / dívidas conhecidas

- **Pasta dos documentos**: sem caminho fixo. Vem de `MEMO_DIR` ou de
  `Configuracoes.DiretorioDocumentos`; a GUI pergunta na 1ª execução.
- **`source/Memo.Service/Extensoes/StringExtensao.cs`** contém utilitários de
  string legados (`StringUtil`) que **não são usados** pelo app atual. Mantidos
  por inércia; podem ser removidos. Não confie neles (alguns têm bugs, ex.:
  `RemoverCaracteresEspeciais` usa `string.Replace` com padrões de regex como
  literais).
