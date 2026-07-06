# Memo Rail — assistente de foco

Companheiro de foco pensado para quem se distrai fácil (TDAH-friendly): você
declara a **missão do dia** e o Memo te **puxa de volta pro trilho** — em
check-ins periódicos e quando detecta distração. Roda dentro da instância da
bandeja; é **opt-in** (desligado por padrão).

## Como funciona

1. **Missão do dia** — na primeira abertura dentro do horário de atuação (ou pelo
   menu da bandeja → *Missão do dia…*), o Memo pergunta "o que vamos fazer hoje?".
   Você monta um checklist. A "tarefa atual" é sempre a primeira pendente.
2. **Check-in periódico** — a cada X minutos (padrão 45), o **cerebrinho** 🧠
   aparece no canto: *"Ainda na '<tarefa>'?"* → **✔ Concluí** · **Ainda nela** ·
   **+15 min**.
3. **Detector de desvio** — a cada tick (~20s), o Rail olha a **janela em primeiro
   plano** (processo + título). Se bater com um termo da lista de **distrações**
   (YouTube, Instagram…) por N minutos contínuos (padrão 5), o cerebrinho aparece:
   *"Isso não parece a missão…"* → **Voltar pro trilho** · **Estou trabalhando**
   (silencia aquele termo por 1h).

## Ação da tarefa (link 🔗)

Cada tarefa pode ter um **link** que ajuda a executá-la — a conversa do WhatsApp
(`https://wa.me/…`), um ticket, um documento. Como definir:

- **Automático**: cole a URL junto do texto ao adicionar
  (`mandar msg pro caio https://wa.me/5511…`) — a URL sai do texto e vira a ação.
- **Explícito (CLI)**: `memo-cli rail add "revisar ticket" --link <url>`.

Onde aparece: botão **🔗** na linha da tarefa (`JanelaMissao`), botão **🔗 Abrir**
no check-in do cerebrinho, e o **Voltar pro trilho** (aviso de desvio) abre o link
da tarefa atual — te levando direto pra ela.

## Anti-perturbação (por design)

- **Cooldown**: silêncio mínimo entre aparições (padrão 10 min), aconteça o que acontecer.
- **Ociosidade**: sem input por >3 min = usuário longe → não conta nem perturba.
- **Horário de atuação**: só age na janela configurada (padrão 9h–18h, dias úteis).
- **Gentil, mas visível**: o cerebrinho surge como uma **bolha redonda 🧠
  pulsando na posição do mouse** (difícil de não ver); um clique expande para o
  cartão com os botões. Não rouba o foco do teclado (`ShowActivated=False`),
  some sozinho após ~30s se ignorado e só volta no próximo ciclo.
- **Missão cumprida** (ou sem missão): silêncio total.

## Privacidade

O monitoramento lê **apenas** o processo e o título da janela ativa, **em
memória**, para comparação com a lista de distrações — e descarta. Nada de
screenshot, nada de teclado, nenhum histórico de janelas é gravado em disco e
nada sai da máquina. O que persiste:

| Arquivo | Conteúdo |
|---------|----------|
| `%LOCALAPPDATA%\Memo\rail.json` | Missões dos últimos 14 dias (texto das tarefas, concluído/não, último check-in). |
| `config.json` → `Rail` | Preferências (intervalos, horário, lista de distrações). |

Missão **não é segredo**: não passa pelo cofre e os comandos `rail` não pedem a
senha-mestra.

## Configuração

**Configurações → Memo Rail**: habilitar, perguntar missão ao iniciar, intervalo
de check-in (25/45/60/90 min), limiar de desvio (3/5/10 min), cooldown (5/10/20
min), horário (início/fim + dias úteis) e a **lista de distrações** (um termo por
linha; a comparação é por substring, sem diferenciar maiúsculas, contra o título
da janela e o nome do processo).

## CLI

```
memo rail                 # abre o checklist da missão do dia
memo rail add <tarefa>    # adiciona tarefa
memo rail done <n>        # conclui a tarefa n
memo rail status          # resumo em toast

memo-cli rail             # status (checklist no stdout)
memo-cli rail add <tarefa> [--link <url>]   # URL no texto também vira o link
memo-cli rail done <n>
memo-cli rail clear       # apaga a missão de hoje
memo-cli rail --json      # { date, items: [{n, text, done, link}] }
```

## Mapa do código

| Arquivo | Papel |
|---------|-------|
| `Memo.Service/Rail/MissaoDia.cs` | POCOs (missão + itens). |
| `Memo.Service/Rail/RailService.cs` | Persistência (`rail.json`), concluir/adicionar, heurística `EhDistracao`. |
| `Memo.Service/Rail/RailConfig.cs` | Preferências + `DentroDoHorario` + distrações padrão. |
| `Memo/Rail/MonitorFoco.cs` | Win32: janela ativa (`GetForegroundWindow`) e ociosidade (`GetLastInputInfo`). |
| `Memo/Rail/RailCoordenador.cs` | Orquestração: decide *quando* aparecer (cooldown, silenciados, adiar). |
| `Memo/Rail/JanelaMissao.xaml` | Checklist da missão do dia. |
| `Memo/Rail/JanelaCerebrinho.xaml` | O widget 🧠 (modos check-in e desvio). |

O `RailCoordenador.Tick()` é chamado pelo mesmo `DispatcherTimer` de 20s que
verifica lembretes (`App.IniciarAgendador`).
