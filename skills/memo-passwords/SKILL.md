---
name: memo-passwords
description: Generate strong random passwords with Memo's memo-cli, optionally saving them straight into the vault as a named secret. Use when you need a secure password/passphrase for a new account, service, or credential rotation. Passwords use a cryptographic RNG and honor the user's saved character-set preferences.
---

# Memo — Gerador de senhas (memo-cli)

Gera senhas fortes com um **RNG criptográfico** e, se quiser, já salva no cofre.

## Como funciona

O `pass` usa `System.Security.Cryptography.RandomNumberGenerator` e respeita as
**preferências salvas do usuário** (comprimento e tipos de caractere — maiúsculas,
minúsculas, números, símbolos), definidas na tela **Gerar senha** do app. Garante
ao menos um caractere de cada conjunto habilitado.

## Comando

```
memo-cli pass [<chave>] [--json|--bytes]
```

- **Sem chave**: imprime a senha no `stdout`.
- **Com chave**: gera a senha, **salva** como documento `<chave>` (exige o cofre
  destrancado — ver a skill [memo-secrets](../memo-secrets/SKILL.md)) e imprime.
- `--json` → `{"password":"...","key":"..."|null}`; `--bytes` → bytes crus.

## Receitas

**Só gerar uma senha e usar numa variável:**
```bash
PW=$(memo-cli pass)
```

**Gerar e já guardar no cofre (rotação de credencial):**
```bash
memo-cli unlock --password "$MEMO_PASSWORD"
memo-cli pass "senha servidor prod" --json | jq -r .password
```

**Gerar e copiar para o clipboard** (via app de conveniência, não o memo-cli):
```
memo pass            # Memo.exe: gera e copia para a área de transferência
memo pass foo bar    # gera, salva em "foo bar" e copia
```

## Códigos de saída

`0` ok · `1` erro (ex.: nenhum tipo de caractere habilitado nas preferências) ·
`2` trancado (só quando salva com `<chave>`).

## Notas

- As preferências (comprimento/tipos) são globais do usuário; para mudá-las, use a
  tela **Gerar senha** no app. O `pass` sempre segue essa configuração.
- Não ecoe a senha gerada em logs. Se for salvá-la, prefira a forma com `<chave>`
  para que ela vá direto ao cofre cifrado.
