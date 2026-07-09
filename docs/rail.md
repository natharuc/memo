# Memo Rail — assistente de foco

Companheiro de foco pensado para quem se distrai fácil (TDAH-friendly): você
declara a **missão do dia** e o Memo te **puxa de volta pro trilho** — em
check-ins periódicos e quando detecta distração. Roda dentro da instância da
bandeja; é **opt-in** (desligado por padrão).

## Como funciona

1. **Missão do dia** — na primeira abertura dentro do horário de atuação (ou pelo
   botão **🚂** da janela principal / menu da bandeja / `memo rail`), o Memo
   pergunta "o que vamos fazer hoje?". Você monta um checklist; a "tarefa atual"
   é sempre a primeira pendente — **atrasada tem prioridade**.
2. **Check-in periódico** — a cada X minutos (padrão 45), o **cerebrinho** 🧠
   surge como uma bolha pulsando **na posição do mouse**; um clique expande:
   *"Ainda na '<tarefa>'?"* → **✔ Concluí** · **Ainda nela** · **+15 min** ·
   **🔗 Abrir**.
3. **Detector de desvio** — a cada tick (1s), o Rail olha a **janela em primeiro
   plano** (processo + título). Se bater com um termo da lista de **distrações**
   por N minutos contínuos (N vem do **nível de distração**), o cerebrinho aparece:
   **Voltar pro trilho** (abre o link da tarefa, se houver) · **Preciso focar**
   (liga o modo foco — abaixo) · **Estou trabalhando** (silencia aquele termo por
   um tempo, também do nível).

## Modo foco ("Preciso focar")

No aviso de desvio, **Preciso focar** oferece durações (1 · 5 · 10 · 25 · 30 · 60
min). Durante esse tempo, **toda vez que a janela ativa for uma distração**, o Rail
cobre a tela dela com um **backdrop** de tela cheia (`JanelaBloqueio`) — você não
consegue mais ver a distração. O backdrop:

- Aparece **só sobre a distração**, no monitor dela; ir para uma janela de trabalho
  o esconde na hora (ele nunca cobre o que não é distração).
- Mostra a tarefa atual e a contagem regressiva; tem um discreto **"encerrar modo
  foco"** como válvula de escape.
- É gerenciado por `Memo/Rail/BloqueioFoco.cs`, avaliado a cada tick do Rail
  (independe do horário/nível). Enquanto ativo, o cerebrinho **não** abre aviso de
  desvio — o backdrop já cuida.

## Tarefas: datas, atrasadas e formatação

- **Cada tarefa tem uma data** (`Data`, yyyy-MM-dd). Dá para lançar tarefas para
  outros dias (campo de data no add, editor ✏ ou `--data` na CLI: `hoje`,
  `amanha`, `dd/MM`, `dd/MM/yyyy`, `yyyy-MM-dd` — `RailService.ParseData`).
- **Acúmulo**: pendência de dia anterior vira **ATRASADA** — continua na missão,
  com prioridade no check-in (prefixo "Atrasada:"), até ser concluída. Atrasada
  **nunca** é apagada pela poda (só concluídas com mais de 14 dias saem).
- **Seções** na janela e na CLI: `ATRASADAS` (vermelho) → `HOJE` → `PRÓXIMAS`.
  A numeração é contínua nessa ordem (a mesma para `done <n>`/`edit <n>`).
  Atrasada concluída hoje aparece em HOJE (tachada), para o clique poder ser desfeito.
- **Formatação leve** no texto: `**negrito**`, `*itálico*` e quebras de linha
  (Enter no editor). Renderizada nos cards (`FormatadorTexto.AplicarInlines`);
  cerebrinho e toasts mostram o texto limpo (`SemFormatacao`).
- **Edição**: botão **✏** no card (ou duplo-clique) abre a `JanelaEditarTarefa`
  (texto multiline, link, data com botões Hoje/Amanhã).

## Ação da tarefa (link 🔗)

Cada tarefa pode ter um **link** que ajuda a executá-la — a conversa do WhatsApp
(`https://wa.me/…`), um ticket, um documento:

- **Automático**: cole a URL junto do texto ao adicionar — ela sai do texto e vira a ação.
- **Explícito (CLI)**: `--link <url>`.

Aparece como **🔗 Abrir** no card, no check-in do cerebrinho, e o **Voltar pro
trilho** abre o link da tarefa atual — te levando direto pra ela.

## Nível de distração

Configurável em **Configurações → Memo Rail** (`RailConfig.Nivel` →
`Desvio()`): quanto maior, mais rápido e insistente o aviso.

| Nível | Avisa desvio após | Repete a cada | "Estou trabalhando" silencia por |
|---|---|---|---|
| Baixo | 10 min | 20 min | 120 min |
| Médio (padrão) | 5 min | 10 min | 60 min |
| Alto | 2 min | 5 min | 30 min |
| Muito alto | 1 min | 2 min | 15 min |
| **TDAH** 🧠 | **~1 s** (assim que abre) | 1 min | 10 min |

O cooldown do desvio é **separado** do cooldown de check-ins (`CooldownMinutos`,
que segue valendo só para check-ins). Para o **TDAH** reagir em ~1s, o Rail roda
num timer próprio de **1 segundo** (`App._agendadorRail`), separado do agendador
de lembretes (20s); cada tick faz uma única leitura do `rail.json`
(`RailService.Estado`).

## Anti-perturbação (por design)

- **Cooldowns**: check-ins respeitam `CooldownMinutos`; desvios, o do nível.
- **Ociosidade**: sem input por >3 min = usuário longe → não conta nem perturba.
- **Horário e dias**: só age na janela configurada (`HoraInicio`–`HoraFim`) e nos
  **dias da semana selecionados** (`DiasAtivos`, toggles Seg…Dom; padrão seg–sex).
- **Insistente por design**: o cerebrinho surge como bolha 🧠 pulsando na posição
  do mouse; não rouba o foco do teclado (`ShowActivated=False`) e **nunca some
  sozinho** — se você o ignorar por `RealocarMinutos` (padrão 2 min), ele some de
  onde estava e **reaparece na posição atual do mouse**, até você clicar. Ao
  expandir para o cartão, ele para de se mover (você já está interagindo).
- **Missão cumprida** (ou sem missão): silêncio total.

## Privacidade

O monitoramento lê **apenas** o processo e o título da janela ativa, **em
memória**, para comparação com a lista de distrações — e descarta. Nada de
screenshot, nada de teclado, nenhum histórico de janelas é gravado em disco e
nada sai da máquina. O que persiste:

| Arquivo | Conteúdo |
|---------|----------|
| `%LOCALAPPDATA%\Memo\rail.json` | **v2**: `{ Versao, UltimoCheckIn, Itens[] }` — pool de tarefas com data. O formato v1 (lista de dias) é migrado automaticamente na leitura, sem perder tarefa. |
| `config.json` → `Rail` | Preferências (check-in, nível, horário, dias, distrações). |

Missão **não é segredo**: não passa pelo cofre e os comandos `rail` não pedem a
senha-mestra.

## CLI

```
memo rail                                # abre o checklist
memo rail add <tarefa> [--data <d>]      # adiciona (URL no texto vira o 🔗)
memo rail done <n>                       # conclui pela numeração exibida
memo rail status                         # resumo em toast

memo-cli rail                            # status agrupado (ATRASADAS/HOJE/PRÓXIMAS)
memo-cli rail add <t> [--link <url>] [--data <d>]
memo-cli rail done <n>
memo-cli rail edit <n> [--texto <t>] [--link <url>] [--data <d>]
memo-cli rail clear                      # apaga só as de hoje (atrasadas ficam)
memo-cli rail --json                     # { date, items: [{n, text, done, link, date, overdue}] }
```

## Mapa do código

| Arquivo | Papel |
|---------|-------|
| `Memo.Service/Rail/MissaoDia.cs` | `ItemMissao` (com `Data`), `RailDados` (v2) e `MissaoVisivel` (atrasadas/hoje/futuras + ordem canônica). |
| `Memo.Service/Rail/RailService.cs` | Persistência v2 + migração v1, `MissaoAtual`, `ParseData`, mutações, heurística `EhDistracao`. |
| `Memo.Service/Rail/RailConfig.cs` | Preferências, `NivelDistracao`/`Desvio()`, `DiasAtivos`/`DiasEfetivos`, `DentroDoHorario`. |
| `Memo/Rail/MonitorFoco.cs` | Win32: janela ativa (`GetForegroundWindow`) e ociosidade (`GetLastInputInfo`). |
| `Memo/Rail/RailCoordenador.cs` | Orquestração: quando perguntar/checar/avisar (cooldowns, silenciados, adiar). |
| `Memo/Rail/FormatadorTexto.cs` | Markdown-lite → Inlines; `SemFormatacao` para textos "crus". |
| `Memo/Rail/JanelaMissao.xaml` | Checklist com seções e cards. |
| `Memo/Rail/JanelaEditarTarefa.xaml` | Edição de texto/link/data. |
| `Memo/Rail/JanelaCerebrinho.xaml` | O widget 🧠 (bolha no mouse → cartão). |
| `Memo/Rail/BloqueioFoco.cs` + `JanelaBloqueio.xaml` | Modo foco: backdrop de tela cheia sobre a distração. |

O `RailCoordenador.Tick()` é chamado pelo mesmo `DispatcherTimer` de 20s que
verifica lembretes (`App.IniciarAgendador`).
