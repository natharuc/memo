# Arquitetura

## Visão geral

Memo tem duas peças:

- **`source/Memo`** — aplicativo **WPF (.NET 8, `net8.0-windows`)**. É também o
  ponto de entrada de linha de comando: o mesmo executável (`Memo.exe`) abre a
  GUI quando chamado sem argumentos e age como CLI quando recebe argumentos.
- **`source/Memo.Service`** — biblioteca (`net8.0-windows`) com toda a lógica de
  negócio: cofre, criptografia, repositório de documentos. Não depende de WPF.

A UI é "burra": ela orquestra janelas e delega tudo para `MemoService`.

```
┌─────────────────────────── Memo (WPF / CLI) ───────────────────────────┐
│ App.xaml.cs        roteamento: sessão → senha → GUI ou CLI              │
│ Janela*.xaml(.cs)  telas (principal, senha, editar) + Toast            │
│ Nativo.cs          interop (barra de título escura)                    │
└───────────────────────────────┬────────────────────────────────────────┘
                                 │ usa
┌───────────────────────────────▼──── Memo.Service ───────────────────────┐
│ MemoService          fachada: GUI/CLI chamam só isto                    │
│ Cofre                ciclo de vida do cofre + sessão                    │
│ CryptoCofre          primitivas de cripto (AES-GCM, PBKDF2, legado)     │
│ DocumentoRepository  IO dos arquivos + migração + sanitização          │
│ Documento            POCO { Key, Value }                               │
└─────────────────────────────────────────────────────────────────────────┘
                                 │ lê/escreve
                       arquivos cifrados no disco
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
- `Carregar(key)`, `Salvar(doc)`, `Deletar(key)`.
- `Migrar()` — recifra legado→novo e move o que não abre para `falhas/`
  (retorna `MigracaoResultado`).
- `SanitizarChave(key)` — rejeita separadores de caminho (anti path-traversal).

### `Documento` — `source/Memo.Service/Classes/Documento.cs`
POCO `{ string Key; string Value; }`. Serializado em JSON e então cifrado.

## Componentes (Memo / WPF)

- **`App.xaml.cs`** — `OnStartup` faz o roteamento (ver [Fluxo](#fluxo-de-inicialização)).
- **`JanelaPrincipal`** — lista, busca, painel de detalhes, ações (copiar,
  mostrar/ocultar, editar, excluir, novo).
- **`JanelaSenha`** — cria (primeira vez) ou destranca o cofre.
- **`JanelaEditar`** — cria/edita um documento.
- **`Toast`** — aviso flutuante usado no modo CLI.
- **`Tema.xaml`** — `ResourceDictionary` com o tema escuro (cores e estilos).
- **`Nativo.cs`** — P/Invoke `DwmSetWindowAttribute` para barra de título escura.

Detalhes de comportamento em [ui.md](ui.md).

## Fluxo de inicialização

`App.OnStartup` (`source/Memo/App.xaml.cs`):

1. Se não há pasta configurada (`DiretorioConfigurado == null`), pergunta ao
   usuário (`EscolherPastaDocumentos`) e salva. Depois cria `MemoService`.
2. `TentarDestrancarPelaSessao()`. Se falhar, abre `JanelaSenha` (criar ou
   destrancar). Cancelar → encerra.
3. **Sem argumentos** → roda `service.Migrar()` (auto-reparo; mostra relatório
   se mexeu em algo) e abre `JanelaPrincipal`.
4. **Com argumentos** → executa `get` / `set` / `migrar` e mostra um `Toast`.

## Fluxo de dados de um segredo

```
Salvar:   Documento → JSON → AES-256-GCM(chave) → base64 → arquivo "<Key>"
Carregar: arquivo "<Key>" → base64 → AES-256-GCM⁻¹(chave) → JSON → Documento
```

A chave vem de `PBKDF2(senha, salt do vault.json)`. Ver [security.md](security.md).

## Notas / dívidas conhecidas

- **Pasta dos documentos**: sem caminho fixo. Vem de `MEMO_DIR` ou de
  `Configuracoes.DiretorioDocumentos`; a GUI pergunta na 1ª execução.
- **`source/Memo.Service/Extensoes/StringExtensao.cs`** contém utilitários de
  string legados (`StringUtil`) que **não são usados** pelo app atual. Mantidos
  por inércia; podem ser removidos. Não confie neles (alguns têm bugs, ex.:
  `RemoverCaracteresEspeciais` usa `string.Replace` com padrões de regex como
  literais).
