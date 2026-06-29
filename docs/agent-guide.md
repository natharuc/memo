# Guia para agentes de IA

Leia isto antes de mexer no código. Resume o que você precisa saber para ser
produtivo e, principalmente, para **não destruir dados do usuário**.

## O que é, em 30 segundos

Cofre de segredos **file-based**: cada segredo é um arquivo cifrado
(AES-256-GCM) num diretório; a chave vem de uma senha-mestra (PBKDF2). Três
projetos: `source/Memo` (WPF + bandeja, também age como CLI), `source/Memo.Cli`
(CLI console scriptável) e `source/Memo.Service` (núcleo). Os front-ends só
chamam `MemoService`. Plataforma: Windows, .NET 8.

## Mapa do código (onde mexer)

| Quero... | Vá para |
|----------|---------|
| Mudar criptografia / formato de arquivo | `Memo.Service/Seguranca/CryptoCofre.cs` |
| Mudar senha-mestra / sessão / vault.json | `Memo.Service/Seguranca/Cofre.cs` |
| Mudar leitura/escrita/migração de documentos | `Memo.Service/Repositorio/DocumentoRepository.cs` |
| Adicionar uma operação de negócio | `Memo.Service/MemoService.cs` |
| Adicionar/alterar um comando da CLI | `Memo.Cli/Program.cs` (console) **e** `Memo/App.xaml.cs` (GUI) |
| Mexer nos lembretes (parser/persistência) | `Memo.Service/Lembretes/*` |
| Mudar o roteamento de inicialização / bandeja | `Memo/App.xaml.cs` (`OnStartup`) |
| Mexer na tela principal | `Memo/JanelaPrincipal.xaml[.cs]` |
| Telas de senha / edição / toast | `Memo/JanelaSenha.*`, `JanelaEditar.*`, `Toast.*` |
| Tema (estilos) / cores | `Memo/Tema.xaml` (estilos) + `PaletaEscura.xaml`/`PaletaClara.xaml` (cores); troca em `Memo/Tema.cs` |

Detalhes em [architecture.md](architecture.md), [security.md](security.md),
[cli.md](cli.md), [ui.md](ui.md).

## ⛔ Invariantes — não quebre

1. **Não mude o formato de cifragem sem migração.** O byte de versão é `0x02`
   (`CryptoCofre.Versao`). Se mudar o formato, escreva o caminho de leitura do
   formato antigo **e** migre, como já existe para o legado. Decifrar deve sempre
   tentar o formato atual e cair para os antigos.
2. **`vault.json` é dado, não config.** Ele guarda o **salt**. Sobrescrever com
   um salt novo torna os documentos existentes ilegíveis (mesmo com a senha
   certa). Nunca recrie o `vault.json` de um cofre que já tem documentos.
3. **A sessão precisa ser validada contra o cofre.** `TentarDestrancarPelaSessao`
   só aceita a chave se ela decifrar o verificador atual (`ChaveAbreCofre`).
   Nunca confie numa chave de sessão sem essa checagem.
4. **A sessão é isolada por diretório.** O arquivo é
   `session-<hash do diretório>.bin`. Nunca volte para um caminho global
   compartilhado.
4b. **A expiração da sessão é absoluta.** O prazo é fixado quando a senha é
   digitada e **não** é renovado a cada uso. Não reintroduza renovação na leitura
   (`TentarDestrancarPelaSessao`) — isso fazia o prazo nunca vencer e quebraria o
   badge de contagem regressiva da `JanelaPrincipal`.
5. **Sanitize chaves.** Toda operação por chave passa por
   `DocumentoRepository.SanitizarChave` (anti path-traversal). Mantenha.
6. **Leitura é resiliente.** `CarregarTodos` nunca pode lançar por causa de um
   arquivo ruim — pula e continua. A GUI depende disso.
7. **A migração só pode quarentenar com a chave certa.** `Migrar` recifra com a
   chave do cofre **destrancado e validado**. Se a chave estiver errada, ela
   moveria documentos bons para `falhas/` (foi exatamente o incidente abaixo).

## 🔥 Lições do incidente (leia antes de tocar em cripto/migração)

Houve uma perda parcial de dados durante o desenvolvimento. Causa:

- Um teste gravou um `session.bin` num **caminho global compartilhado** com uma
  **chave de teste**. Numa execução seguinte, o app **reaproveitou essa sessão
  sem validar**, e a migração **recifrou ~80 documentos com a chave errada**
  (efêmera, não reproduzível), movendo-os para `falhas/`. A senha do usuário
  estava certa, mas a chave dos arquivos não era a dela.
- Recuperação possível só porque havia uma **cópia-sombra (VSS)** do Windows com
  os arquivos no formato **legado** (chave conhecida). 69 de 80 voltaram; os
  outros eram placeholders "online-only" do OneDrive (sem conteúdo no snapshot).

Correções já aplicadas (invariantes 3 e 4 acima): sessão validada
(`ChaveAbreCofre`) e isolada por diretório (`session-<hash>.bin`).

**Regras que vieram daí:**
- **Nunca rode o app real apontado para os dados do usuário em testes.** Use
  `new MemoService("<dir temporário>")` ou `new Cofre("<dir temporário>")`.
  Lembre que a sessão é por diretório, então um dir temporário não toca a sessão
  real — mantenha assim.
- **Antes de qualquer operação destrutiva ou de migração em dados reais, faça
  backup** do diretório (copie a pasta inteira para fora da nuvem).
- **`Migrar` quarentena = mover, nunca apagar.** Mantenha esse comportamento.

## Como fazer mudanças comuns

### Adicionar um comando de CLI (ex.: `memo arquivar <chave>`)
1. Se for lógica de negócio, coloque em `MemoService` (método retornando
   `ResultadoCli`) para os dois front-ends reusarem.
2. **Console** (`memo-cli`): adicione um `case` no `switch` de `Program.Main` e o
   método correspondente, com o **exit code** certo (`Codigo.*`).
3. **GUI** (`Memo.exe <args>`): adicione o ramo em `App.ExecutarComando`
   (mostra um `Toast`).
4. Atualize [cli.md](cli.md) **e** este guia.

### Pasta dos documentos (sem caminho fixo)
Resolvida por `MemoService.DiretorioConfigurado`: `MEMO_DIR` (env) → senão
`Configuracoes.DiretorioDocumentos`. Se nenhuma → null: a GUI pergunta na 1ª
execução (`App.EscolherPastaDocumentos`, `FolderBrowserDialog`) e salva; o CLI
usa `memo-cli config --dir <pasta>`. `MemoService()` lança se ainda não houver
pasta — quem cria o serviço deve garantir a pasta antes.

### Permitir renomear a chave (hoje não suportado)
Renomear = `Salvar` com a chave nova + `Deletar` a antiga, no repositório, de
forma atômica o suficiente (escreve o novo antes de apagar o velho). Hoje
`JanelaEditar` trava a chave na edição justamente por isso.

### Mexer no tema
**Estilos** ficam em `Memo/Tema.xaml`; **cores** em `PaletaEscura.xaml` e
`PaletaClara.xaml` (mescladas em `App.xaml`). Os estilos referenciam as cores por
**`DynamicResource`** (`CorFundo`, `CorPainel`, `CorDestaque`, …), então
`Tema.Aplicar(...)` (em `Memo/Tema.cs`) troca a paleta em runtime. Para uma cor
nova, declare-a nas **duas** paletas. Reutilize estilos existentes. Ver
[ui.md](ui.md).

## Build e teste rápido

```powershell
dotnet build source/Memo.slnx -c Release
dotnet run --project source/Memo -- get alguma-chave   # CLI
```

Para validar cripto sem GUI, escreva um console que referencie `Memo.Service`,
crie um `Cofre` num **diretório temporário**, e teste round-trip / senha errada /
adulteração / legado. Ver [development.md](development.md) → Testes.

## Convenções

- **Documentação primeiro.** Comece pela documentação (`docs/`) antes de mexer no
  código e **atualize-a na mesma alteração** sempre que mudar comportamento,
  formato de arquivo, fluxo ou comandos. Ver [CLAUDE.md](../CLAUDE.md) /
  [AGENTS.md](../AGENTS.md).
- Código e nomes em **português**. Mantenha.
- `Memo.Service` **não** depende de WPF — não traga UI para lá.
- UI sem MVVM (code-behind simples).
- Segredos: nunca logar `Value`; nunca versionar `vault.json`, `session*.bin`,
  `falhas/`, `*.pfx`.

## Estado conhecido / pendências

- Pasta dos documentos: sem caminho fixo (MEMO_DIR / config / pergunta na 1ª vez).
- Sem projeto de testes versionado (recomendado adicionar xUnit).
- `StringUtil` em `Memo.Service/Extensoes/StringExtensao.cs` é legado e não usado
  (alguns métodos têm bugs); pode ser removido.
- Sem limpeza automática do clipboard após `get` (possível melhoria de
  segurança).
- O **`memo-cli`** já define exit codes por sucesso/erro; a **GUI em modo CLI**
  (`Memo.exe <args>`) não — ela só mostra um `Toast`.
- A subpasta `deletados/` **não é mais criada** (exclusão virou definitiva); pode
  haver pastas `deletados/` antigas em cofres já existentes, sem uso pelo app.
