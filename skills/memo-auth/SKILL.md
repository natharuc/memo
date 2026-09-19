---
name: memo-auth
description: Copy or print a current TOTP code from the user's Ente Auth vault using memo-cli auth on Windows. Use when the user needs a 2FA/2auth/OTP code (GitHub, Vercel, etc.) and stores authenticators in Ente Auth. Does not require unlocking the Memo vault.
---

# Memo — TOTP / Ente Auth (memo-cli)

Gera o código TOTP atual (6 dígitos) a partir da conta **Ente Auth** do usuário.
Não lê o app GUI do Ente Auth (não há API local). Usa a **Ente CLI** oficial
(`ente.exe`) e um cache DPAPI local.

**Não exige o cofre do Memo.** A sessão é a da Ente CLI.

## Setup (uma vez)

No app: **Configurações → Ente → Instalar Ente CLI**, depois **Conectar conta Auth**
(quando perguntar o app, `auth`; mesmo e-mail do Ente Auth). A CLI vai para
`%LOCALAPPDATA%\Memo\ente` e entra no PATH do usuário.

Se o comando falhar dizendo que a CLI/conta não existe, peça ao usuário para abrir
essa aba — **não** rode `ente account add` você mesmo (é interativo, pede senha).

## Comandos

```
memo-cli auth <issuer> [conta] [--copy|--json|--sync]
memo-cli auth list [--json] [--sync]
memo-cli auth sync
```

- Tokens depois de `auth` são filtro **AND**, case-insensitive, substring em
  issuer / account / label (ex.: `vercel nathanarrudacamara@gmail.com`).
- **1 match:** imprime o código de 6 dígitos no stdout (padrão).
- `--copy` copia e **não** imprime o código (status no stderr).
- `--json` → `{"issuer","account","code","remainingSeconds"}`.
- **0 matches:** exit **3**. **Vários:** exit **1**, lista os candidatos, não copia.
- `list` lista `Issuer · conta` (sem secret / sem código).
- `sync` / `--sync` força refresh do cache (~6 h).

## Exemplos

```bash
memo-cli auth vercel nathanarrudacamara@gmail.com
memo-cli auth vercel nathanarrudacamara@gmail.com --copy
memo-cli auth github --json
memo-cli auth list
memo-cli auth sync
```

**Usar o código numa automação sem ecoar em log:**

```bash
CODE=$(memo-cli auth vercel nathanarrudacamara@gmail.com --text) || exit $?
# use $CODE na hora; não grave em arquivo nem commite
```

## Códigos de saída

`0` ok · `1` erro (CLI ausente, conta auth não cadastrada, match ambíguo) ·
`3` não encontrado · `64` uso.

## Segurança

- Nunca imprima o `--json` completo em logs (tem o código vigente).
- O cache (`%LOCALAPPDATA%\Memo\ente-auth.bin`) contém secrets TOTP cifrados por
  DPAPI da conta Windows — não versione, não copie para outro usuário.
- A GUI (`memo auth …`) também funciona (Toast + clipboard); agentes devem usar
  **`memo-cli`**.
