# Funcionalidades

Catálogo de tudo que o Memo faz, agrupado por área e com exemplos. É a visão
"por capacidade" — para os detalhes internos, cada seção aponta para o documento
de referência ([architecture.md](architecture.md), [security.md](security.md),
[cli.md](cli.md), [ui.md](ui.md)).

## Visão geral

| Função | O que faz | Onde |
|--------|-----------|------|
| **Guardar segredo** | Cria/atualiza um documento cifrado | GUI (**+ Novo**) · `set` |
| **Ler segredo** | Copia o valor / imprime no stdout | GUI (**Copiar**) · `get` |
| **Listar** | Todas as chaves do cofre | GUI (lista) · `list` |
| **Buscar** | Filtra por substring da chave | GUI (campo de busca) |
| **Editar** | Altera o valor de um documento | GUI (**Editar**) |
| **Excluir** | Remove **definitivamente** (sem lixeira) | GUI (**Excluir**) · `del` |
| **Gerar senha** | Senha aleatória pelas suas preferências | GUI (**Gerar senha**) · `pass` |
| **Gerar GUID** | UUID novo | `guid` |
| **Lembrete** | Aviso em linguagem natural, na bandeja | GUI (**⏰**) · `remember` |
| **Missão do dia (Rail)** | Foco: checklist, check-ins, detector de distração | GUI (**🚂**) · `rail` |
| **Notificar** | Envia para Telegram/e-mail | `notify` |
| **Bot do Telegram** | Controla o Rail pelo Telegram (opt-in) | Config. → Notificações |
| **Trancar / destrancar** | Encerra/reabre a sessão | badge da tela · `lock`/`unlock` |
| **Trocar tema** | Claro ou escuro, em runtime | GUI (**⚙**) |
| **Migrar** | Recifra documentos antigos no formato atual | automática · `migrar` |
| **Auto-update** | Atualiza pelo GitHub Releases | automática (no startup) |

Duas formas de usar pela linha de comando: o **`memo-cli`** (console scriptável,
com stdout e exit codes) e o **`Memo.exe <args>`** (a própria GUI, que copia para
a área de transferência e mostra um `Toast`). Ver [cli.md](cli.md).

---

## Cofre e criptografia

O cofre é **file-based**: cada segredo é um arquivo cifrado numa pasta escolhida
por você (sincronizável, ex.: OneDrive). Uma **senha-mestra** protege tudo.

- Chave de 256 bits derivada por **PBKDF2-SHA256** (200.000 iterações, salt
  aleatório por cofre).
- Cada documento é cifrado com **AES-256-GCM** (cifra autenticada: detecta
  adulteração), com nonce aleatório por arquivo.
- A senha nunca é gravada; a chave só vive em memória e no cache de sessão
  (DPAPI).

Detalhes e formatos: [security.md](security.md).

## Documentos

Um documento é um par `{ chave, valor }`; a chave é o nome do arquivo.

- **Guardar**: pela GUI (**+ Novo**) ou `set "github token" = ghp_...`. A chave
  é **sanitizada** (sem `/`, `\`, `..`).
- **Ler/copiar**: **Copiar** na GUI, ou `get "github token"` (copia; no
  `memo-cli`, imprime no stdout — ideal para scripts).
- **Buscar**: o campo no topo filtra por substring (case-insensitive). Enter copia
  o selecionado; ↓ vai para a lista.
- **Mostrar/ocultar**: o valor começa mascarado (`••••••`) e só aparece ao clicar
  em **Mostrar**.
- **Editar**: altera o valor (a chave fica travada — renomear não é suportado).
- **Excluir**: **definitivo e irreversível** — o arquivo é removido de vez (não há
  lixeira). A GUI confirma com um alerta destacado; a CLI é o comando `del`.

Comportamento da tela em [ui.md](ui.md); IO/persistência em
[architecture.md](architecture.md).

## Gerador de senha

Gera uma senha aleatória a partir das suas preferências (comprimento e tipos de
caractere: maiúsculas, minúsculas, números, símbolos).

- Preferências na janela **Gerar senha** (botão em novo/editar); salvas em
  `Configuracoes.Senha` e **reusadas na CLI**.
- `pass` copia/imprime uma senha nova. Com uma chave, também **salva** o
  documento: `pass "wifi casa"`.

## GUID

`guid` gera um UUID novo e copia (GUI) ou imprime (`memo-cli`). Útil para
identificadores rápidos.

## Lembretes

Avisos em **linguagem natural** (PT/EN) que **não são segredos** (não pedem a
senha-mestra). Disparam pela **bandeja** com popup + som.

```
memo remember pagar boleto 9h tomorrow      # amanhã 09:00
memo remember ligar joão in 15 minutes      # daqui a 15 min
memo remember beber agua every 30 minutes   # repete a cada 30 min
memo remember ver tarefa 477987 22h         # hoje 22:00 (ou amanhã, se já passou)
```

- **Recorrência**: `every N minutes|hours` / `a cada N minutos|horas`.
- **Relativo**: `in N …`, `daqui [a] N …`, `em N …`.
- **Hora do dia**: `HH:mm`, `HHh`, `HHhMM`, com `tomorrow`/`amanhã`/`hoje`.
- No popup dá para **Concluir** ou **Adiar** (soneca). Recorrentes reagendam o
  próximo disparo automaticamente.
- Guardados em `%LOCALAPPDATA%\Memo\lembretes.json`. Sem o app na bandeja, o
  lembrete só aparece quando o Memo abrir.

Regras do parser: [cli.md](cli.md) → `remember`. Agendador: [architecture.md](architecture.md) → Lembretes.

## Memo Rail (foco)

Assistente de foco TDAH-friendly (**opt-in**): você declara a **missão do dia** e o
Memo te puxa de volta com check-ins e um detector de distração (que bloqueia a
janela da distração no "modo foco"). Tarefas têm data, acumulam como **atrasadas**,
aceitam link (🔗) e formatação leve.

- Abrir: botão **🚂** / menu da bandeja / `memo rail`.
- CLI: `rail add|done|edit|move|clear|status`.
- **Reordenar**: `rail move <n> up|down`.

Detalhes (nível de distração, modo foco, privacidade): [rail.md](rail.md).

## Notificações e bot do Telegram

- **Saída** (`notify`): envia mensagens para **Telegram** e/ou **e-mail** (canais
  configurados em Configurações → Notificações, cifrados por DPAPI).
- **Entrada** (bot): com **"Ouvir comandos"** ligado, você controla o **Rail pelo
  Telegram** por **linguagem natural** (ex.: "priorizar 2", "nova missão: X
  amanhã", "remover 1"). Só o chat configurado é obedecido e **nenhum segredo
  trafega pelo Telegram** (mexe só no Rail). Ver [rail.md](rail.md) → Controle pelo
  Telegram.

## Sessão e bloqueio

Depois de destrancar, a chave fica em **cache de sessão** para não pedir a senha a
cada uso.

- Protegida por **DPAPI** (só o mesmo usuário Windows lê), **isolada por
  diretório** e **validada** contra o cofre atual.
- Prazo **absoluto** (conta a partir da senha digitada; não renova a cada uso) e
  **configurável** (15 min por padrão, de 1 min a 7 dias, em **⚙**).
- A tela principal mostra um **badge** com a contagem regressiva; ao zerar,
  **bloqueia** (overlay que esconde os segredos). **Clicar no badge tranca na
  hora** — bom para entregar o PC já trancado.
- `lock` / `unlock` fazem o mesmo pela linha de comando (compartilham a sessão em
  disco com a GUI).

Detalhes: [security.md](security.md) → Cache de sessão.

## Temas

Tema **claro** ou **escuro**, trocado em runtime (**⚙** → Preferências), com
pré-visualização ao vivo. A barra de título acompanha o tema. Ver [ui.md](ui.md).

## Linha de comando

Referência completa em [cli.md](cli.md). Resumo dos comandos do **`memo-cli`**:

| Comando | Efeito |
|---------|--------|
| `get <chave>` | Lê um segredo (stdout; `--copy` = área de transferência) |
| `set <chave> <valor>` | Cria/atualiza (`<chave>=<valor>`, `--value`, `--stdin`) |
| `list` | Lista as chaves |
| `del <chave>` | Exclui **definitivamente** |
| `remember <texto/quando>` | Cria um lembrete |
| `pass [chave]` | Gera uma senha (e salva, se der uma chave) |
| `guid` | Gera um GUID |
| `unlock` / `lock` | Destranca (pede senha) / tranca o cofre |
| `migrar` | Recifra documentos antigos |
| `config [--dir <pasta>]` | Mostra/define a pasta do cofre |
| `version` / `help` | Versão / ajuda |

- **Formatos de saída**: `--text` (padrão), `--json`, `--bytes`, `--copy`.
- **Senha (cofre trancado)**: `--password`, variável `MEMO_PASSWORD`, ou prompt
  mascarado num terminal interativo.
- **Pasta do cofre**: variável `MEMO_DIR` ou `config --dir`.
- **Exit codes**: `0` ok · `1` erro · `2` trancado · `3` não encontrado · `64` uso.

A GUI (`Memo.exe <args>`) aceita `get`/`set`/`new`/`pass`/`guid`/`migrar`/
`lock`/`unlock`/`remember`, mas **não** captura stdout — use o `memo-cli` para
automação.

## Segurança

Além da criptografia do cofre:

- **Anti path-traversal**: chaves com `/`, `\` ou `..` são rejeitadas.
- **Histórico do Win+R**: `memo set ...` (segredo em texto puro) é apagado do
  RunMRU automaticamente.
- **Leitura resiliente**: um arquivo corrompido nunca derruba o app — é pulado.
- **Migração segura**: recifra só com a chave certa; o que não abre vai para
  `falhas/` (move, nunca apaga).

Modelo de ameaças completo: [security.md](security.md).

## Bandeja e instância única

Sem argumentos (ou com `--tray`), o Memo roda como app de **bandeja** em
**instância única** (uma 2ª execução só traz a janela existente à frente). O menu
tem *Abrir Memo*, *Lembretes…* e *Sair*. Fechar no **X** esconde na bandeja (segue
disparando lembretes). Ver [ui.md](ui.md) → Bandeja e lembretes.

A janela **abre focada**: vem ao primeiro plano e o cursor cai direto no campo
(senha ou busca), mesmo aberta a partir da bandeja.

## Atualização automática

No startup, o Memo consulta a release mais recente no GitHub. Havendo versão nova,
oferece baixar/instalar: valida o **SHA256**, troca o executável e reinicia. Falha
de rede é silenciosa; o updater não toca no cofre nem na sessão. Ver
[development.md](development.md) → Auto-update.

## Pasta do cofre

Sem caminho fixo no código. Resolvida por **`MEMO_DIR`** (env) → preferência salva
(`config --dir`). Na primeira execução, a GUI pergunta a pasta (dica: uma pasta
sincronizada como OneDrive deixa o cofre disponível em outros PCs).
