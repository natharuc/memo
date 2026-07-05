---
name: memo-secrets
description: Read, write, list, and delete encrypted secrets (passwords, tokens, API keys) in a Memo vault using the memo-cli command-line tool on Windows. Use when you need to fetch a credential to authenticate against a service, store a new secret, or manage vault entries. Covers unlocking the vault, JSON output, and exit codes.
---

# Memo — Secrets (memo-cli)

Ler e gravar segredos no cofre do Memo pela linha de comando, de forma segura e
scriptável.

## Como o Memo guarda segredos

Cada segredo é um par **`{ chave, valor }`**: a `chave` é o nome (e o nome do
arquivo em disco); o `valor` é o segredo. Cada documento é um arquivo **cifrado
em AES-256-GCM** numa pasta escolhida pelo usuário. A chave-mestra nunca é gravada
em texto puro. `memo-cli` é o console feito para automação.

## Pré-requisitos

1. **`memo-cli`** disponível (no `PATH` ou use o caminho do executável).
2. **Pasta do cofre** configurada: variável `MEMO_DIR` **ou** `memo-cli config --dir <pasta>`.
3. **Cofre destrancado** (ver *Destravar* abaixo) para comandos que tocam segredos.

## Comandos

```
memo-cli get <chave> [--json|--text|--bytes|--copy]
memo-cli set <chave> <valor>            # ou <chave>=<valor>, --value <v>, --stdin
memo-cli list [--json]                   # (alias: ls)
memo-cli del <chave>                     # exclusão DEFINITIVA (alias: rm, delete)
```

- **`get`** imprime o **valor** no `stdout` (sem quebra extra em `--text`? há `\n`).
  Com `--json` sai `{"key":"...","value":"..."}`; com `--copy` copia para o
  clipboard e não imprime; com `--bytes` escreve os bytes crus (bom para binários).
- **`set`** aceita: `set nome valor`, `set nome=valor`, `set nome --value "<v>"`,
  ou `set nome --stdin` (lê o valor do `stdin` — **melhor para não expor o segredo**
  na linha de comando).
- **`list`** lista as chaves (uma por linha, ou array JSON).
- **`del`** exclui **definitivamente** (sem lixeira) — confirme antes.

## Destravar o cofre

Comandos que leem/gravam segredos precisam do cofre aberto. Ordem de resolução:

1. **Sessão** válida (cache DPAPI, ~15 min) — reaproveitada entre processos.
2. **`--password <senha>`** ou variável **`MEMO_PASSWORD`**.
3. **Prompt** mascarado, só se rodando num terminal interativo.

Para scripts: destranque uma vez e reutilize a sessão.
```
memo-cli unlock --password "$MEMO_PASSWORD"   # ou MEMO_PASSWORD no ambiente
memo-cli get "token github" --json            # dentro da janela da sessão
memo-cli lock                                  # tranca na hora quando terminar
```

## Códigos de saída

`0` ok · `1` erro · `2` **trancado** (destranque e repita) · `3` **não encontrado**
· `64` uso incorreto. Sempre cheque o exit code em vez de fazer parse de mensagens.

## Receitas

**Buscar um segredo para usar numa chamada (sem vazar em log):**
```bash
TOKEN=$(memo-cli get "token github" --text) || { echo "cofre trancado/ausente"; exit 1; }
curl -H "Authorization: Bearer $TOKEN" https://api.github.com/user
```

**Ler como JSON e extrair com jq:**
```bash
memo-cli get "smtp prod" --json | jq -r .value
```

**Gravar sem expor o valor na linha de comando (via stdin):**
```bash
printf '%s' "$NOVA_SENHA" | memo-cli set "senha banco" --stdin
```

**Existe? (checando exit code):**
```bash
if memo-cli get "chave x" --json >/dev/null 2>&1; then echo "existe"; fi
```

## Segurança (importante)

- **Nunca** ecoe o valor retornado por `get` em logs, prints de debug, ou mensagens
  que serão commitadas. Capture direto em variável ou use `--copy`.
- Prefira **`--stdin`** a passar o segredo como argumento (o argumento pode ficar no
  histórico do shell). No Windows, o Memo já limpa `memo set ...` do histórico do
  Win+R, mas isso não vale para outros shells.
- Não versione a pasta do cofre, `vault.json` nem `session*.bin`.
