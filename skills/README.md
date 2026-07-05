# Skills do Memo

Conjunto de **Agent Skills** que ensinam um agente de IA a operar o Memo pela
linha de comando (`memo-cli`). Cada pasta tem um `SKILL.md` com instruções
completas, exemplos e códigos de saída.

| Skill | Para quê |
|-------|----------|
| [memo-secrets](memo-secrets/SKILL.md) | Ler, gravar, listar e excluir segredos (senhas, tokens, chaves) no cofre. |
| [memo-passwords](memo-passwords/SKILL.md) | Gerar senhas fortes (e opcionalmente salvá-las como segredo). |
| [memo-reminders](memo-reminders/SKILL.md) | Criar lembretes em linguagem natural. |
| [memo-notify](memo-notify/SKILL.md) | Enviar notificações para Telegram / e-mail. |

## O que é o Memo (resumo para agentes)

Memo é um **cofre de segredos file-based para Windows** (.NET 8). Cada segredo é um
arquivo cifrado em **AES-256-GCM**, com a chave derivada de uma **senha-mestra**
(PBKDF2-SHA256). Existem dois executáveis com a mesma lógica:

- **`Memo.exe`** — app WPF com bandeja; sem argumentos abre a GUI, com argumentos
  vira CLI de conveniência (mostra um *toast*).
- **`memo-cli.exe`** — **console scriptável**, com `stdout`/`stderr` separados,
  `--json` e **códigos de saída** — é o que os agentes devem usar.

> Todas as skills assumem `memo-cli` disponível (no `PATH` ou pelo caminho do
> executável). No repositório, o binário sai em
> `source/Memo.Cli/bin/Release/net8.0-windows/memo-cli.exe` após
> `dotnet build source/Memo.slnx -c Release`.

## Conceitos comuns

- **Pasta do cofre**: resolvida por `MEMO_DIR` (variável de ambiente) → preferência
  salva (`memo-cli config --dir <pasta>`). Sem isso, comandos que tocam o cofre falham.
- **Destravar**: comandos que leem/gravam segredos precisam do cofre destrancado —
  via **sessão** (cache DPAPI, ~15 min), `--password`/`MEMO_PASSWORD` ou
  `memo-cli unlock`. `remember` e `notify` **não** precisam do cofre.
- **Saída**: `--text` (padrão, valor cru no stdout), `--json`, `--bytes`, `--copy`.
  Mensagens de status vão para o **stderr**; o **valor** vai para o **stdout**.
- **Códigos de saída**: `0` ok · `1` erro · `2` trancado · `3` não encontrado · `64` uso.
