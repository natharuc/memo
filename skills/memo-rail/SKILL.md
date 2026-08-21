---
name: memo-rail
description: Manage the user's daily mission (focus checklist) with Memo Rail via memo-cli — read today's task list, add tasks, and mark them done. Use when the user asks to plan their day, add a task to today's mission, check what's pending, or mark work as completed. The Memo tray app then keeps the user on track with periodic check-ins and distraction alerts.
---

# Memo Rail — missão do dia (memo-cli)

O **Memo Rail** é o assistente de foco do Memo (pensado para TDAH): o usuário
define a **missão do dia** (um checklist) e o app na bandeja faz **check-ins
periódicos** e avisa quando detecta **distração** (janela ativa batendo com uma
lista de termos). Um agente interage com a missão pela CLI.

## O que um agente pode fazer

- **Consultar** a missão de hoje (o que está pendente, qual a tarefa atual).
- **Adicionar** tarefas (ex.: transformar um plano em checklist do dia).
- **Concluir** tarefas em nome do usuário quando ele disser que terminou.

Quem faz os check-ins/avisos é o **app na bandeja** — o agente só gerencia a lista.
Missão **não é segredo**: nenhum comando `rail` exige a senha-mestra.

## Comandos

```
memo-cli rail                    # status agrupado: ATRASADAS / HOJE / PRÓXIMAS
memo-cli rail add <tarefa> [--link <url>] [--data <d>]   # d: hoje|amanha|dd/MM|yyyy-MM-dd
memo-cli rail done <n>           # conclui pela numeração exibida (1-based)
memo-cli rail edit <n> [--texto <t>] [--link <url>] [--data <d>]
memo-cli rail move <n> up|down   # reordena dentro do mesmo dia
memo-cli rail clear              # apaga só as tarefas de HOJE (atrasadas ficam)
memo-cli rail --json             # {"date":"…","items":[{n,text,done,link,date,overdue}]}
```

**Datas e atrasadas**: cada tarefa tem uma data (`--data`; padrão hoje). Pendência
de dia anterior **acumula como atrasada** (`overdue: true` no JSON) e continua na
missão, com prioridade, até ser concluída. A numeração segue a ordem exibida:
atrasadas → hoje → próximas.

**Formatação**: o texto aceita `**negrito**`, `*itálico*` e quebras de linha —
renderizados na UI; na CLI aparecem literais.

**Ação da tarefa (link)**: uma tarefa pode carregar um link que ajuda a executá-la
(conversa do WhatsApp, ticket, doc). Passe `--link <url>` ou simplesmente deixe a
URL no texto — ela é detectada e vira a ação. O app mostra um botão 🔗 na tarefa e
nos check-ins do cerebrinho.

Saída do status (texto): uma linha por tarefa `[x] 1. texto`, e o resumo
(`2/5 concluída(s)`) no stderr.

## Exemplos

**Plano do dia vira checklist (com ações):**
```bash
memo-cli rail add "resolver bug do login"
memo-cli rail add "revisar PR 42" --link "https://github.com/org/repo/pull/42"
memo-cli rail add "mandar msg pro caio https://wa.me/5511999998888"
```

**Ver o que falta e qual é a atual (a primeira pendente):**
```bash
memo-cli rail --json | jq '[.items[] | select(.done|not)][0]'
```

**Usuário disse que terminou a tarefa 2:**
```bash
memo-cli rail done 2
```

## Códigos de saída

`0` ok · `3` não encontrado (`done` com número inexistente) · `64` uso incorreto.

## Notas

- A missão vale para **hoje** (`rail.json` local, com histórico curto de ~14 dias).
- Comportamento do Rail (intervalos, horário, distrações) é configurado pelo
  usuário no app (Configurações → Memo Rail) — não há comando para alterá-lo.
- Combine com [memo-notify](../memo-notify/SKILL.md) para avisar o usuário em
  canais externos quando concluir algo por ele.
