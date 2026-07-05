---
name: memo-reminders
description: Create natural-language reminders with Memo's memo-cli (e.g. "beber agua every 30 minutes", "ligar joão in 15 minutes", "ver tarefa 10:00 tomorrow"). Use when the user wants to be reminded of something at a time or on a recurring interval. Reminders are not secrets and do not require unlocking the vault.
---

# Memo — Lembretes (memo-cli)

Cria lembretes em **linguagem natural** (PT/EN). O disparo é feito pelo app do Memo
rodando na bandeja; os lembretes ficam em `lembretes.json` (não são segredo, **não**
exigem o cofre destrancado).

## Comando

```
memo-cli remember <texto/quando> [--json]     # aliases: lembrar, lembrete
```

O texto e o "quando" vão juntos; o parser separa os termos de tempo do texto.

## Regras de tempo

- **Recorrência**: `every N minutes|hours` / `a cada N minutos|horas` → repete; o
  primeiro disparo é daqui a N. Tem prioridade sobre hora do dia.
- **Relativo**: `in N minutes|hours`, `daqui [a] N min`, `em N horas`.
- **Hora do dia**: `HH:mm` (10:00), `HHh` (22h), `HHhMM` (22h30). Com `tomorrow`/
  `amanhã` vai para o dia seguinte; sem dia e já passou hoje → joga para amanhã.
- O **texto** é o que sobra depois de tirar os termos de tempo. Números soltos
  (ex.: `477987`) não são confundidos com hora (exige `:` ou `h`).

## Exemplos

```
memo-cli remember ver tarefa 477987 10:00 tomorrow    # amanhã 10:00
memo-cli remember ver tarefa 477987 22h               # hoje 22:00 (ou amanhã se passou)
memo-cli remember beber agua every 30 minutes         # repete a cada 30 min
memo-cli remember ligar joão in 15 minutes            # daqui a 15 min
memo-cli remember pagar boleto 9h tomorrow --json     # { text, next, repeatMinutes }
```

## Códigos de saída

`0` ok · `64` uso (não deu para entender o "quando").

## Notas

- Quem dispara o alerta é o **app na bandeja**. Se não houver instância rodando, o
  lembrete só aparece quando o app abrir.
- Para avisar em canais externos (Telegram/e-mail) em vez de popup local, use a
  skill [memo-notify](../memo-notify/SKILL.md).
