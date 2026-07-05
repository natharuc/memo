---
name: memo-notify
description: Send a notification message to the user's configured channels (Telegram and/or email) using Memo's memo-cli. Use to alert the user about a finished job, a deploy, a build result, or any status update from a script or agent. Channels are configured once in the Memo app; sending does not require unlocking the vault.
---

# Memo — Notificações (memo-cli)

Envia mensagens para os canais configurados pelo usuário: **Telegram** e/ou
**e-mail (SMTP)**. Ideal para avisar o fim de uma tarefa, um deploy, o resultado de
um build, etc.

## Como funciona

As credenciais dos canais (bot token do Telegram, senha SMTP) ficam **cifradas por
DPAPI** em `%LOCALAPPDATA%\Memo\notificacoes.bin` (atreladas à conta Windows do
usuário). Por isso o `notify` **não** exige o cofre destrancado — ele lê as
credenciais próprias, não os segredos do cofre.

> **Configuração** (feita uma vez pelo usuário): app do Memo →
> **Configurações → Notificações** → abas **Telegram** / **E-mail**, com botão
> **Testar** em cada uma. Um agente **não** configura os canais; apenas envia.

## Comando

```
memo-cli notify [telegram|email] [-t <titulo>] <mensagem> [--json]
```

- **Canal** (opcional, 1º token): `telegram` ou `email`. **Sem canal**, envia a
  **todos os canais habilitados**.
- **Título** (opcional): `-t` / `--titulo` seguido de um argumento (use aspas se
  tiver espaço). Vira o **assunto** do e-mail e o **cabeçalho** da mensagem do
  Telegram. Padrão: `Memo`.
- **Mensagem**: o restante dos argumentos.
- `--json` → `{"ok":true|false,"message":"..."}`.

## Exemplos

```bash
memo-cli notify produção no ar                      # todos os canais habilitados
memo-cli notify telegram deploy concluído           # só o Telegram
memo-cli notify email backup finalizado             # só o e-mail
memo-cli notify -t "Deploy" produção no ar          # com título/assunto
memo-cli notify -t "CI" build #482 quebrou --json   # saída estruturada
```

**Avisar ao terminar uma tarefa longa:**
```bash
./deploy.sh && memo-cli notify -t "Deploy" "produção atualizada com sucesso" \
  || memo-cli notify -t "Deploy" "FALHOU — ver logs"
```

## Códigos de saída

`0` sucesso (≥1 canal enviou) · `1` erro (nenhum canal habilitado, mensagem vazia,
ou todos os envios falharam). Com `--json`, o campo `ok` reflete o mesmo.

## Notas

- Se nenhum canal estiver habilitado, retorna erro pedindo para configurar em
  Configurações → Notificações.
- **Não** envie segredos na mensagem — o conteúdo trafega pelo Telegram/SMTP.
- Erros de rede/SMTP viram mensagem de falha (o comando nunca trava).
