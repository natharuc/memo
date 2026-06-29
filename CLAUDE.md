# CLAUDE.md

Orientações para agentes de IA (Claude Code e afins) trabalhando neste repositório.

## Regra nº 1 — Documentação primeiro

**Sempre comece pela documentação e atualize-a depois de cada alteração
relevante.**

1. **Antes de codar**: leia a doc da área que vai mexer. Comece por
   [docs/agent-guide.md](docs/agent-guide.md) (mapa do código + invariantes) e
   pelo [índice da documentação](docs/README.md).
2. **Depois de codar**: se a mudança alterou comportamento, formato de arquivo,
   fluxo de inicialização, comandos da CLI, UI ou segurança, **atualize a
   documentação na mesma alteração**. Documentação desatualizada conta como bug.

"Relevante" = qualquer mudança que afete o que está documentado. Ajustes triviais
(typo, refactor interno sem efeito observável) não precisam de doc.

## O que é o Memo

Cofre de segredos **file-based**: cada segredo é um arquivo cifrado (AES-256-GCM,
chave PBKDF2) numa pasta escolhida pelo usuário. Três projetos: `source/Memo`
(WPF + bandeja, também age como CLI), `source/Memo.Cli` (CLI console scriptável) e
`source/Memo.Service` (núcleo, sem WPF). Plataforma: Windows, .NET 8.

## Por onde começar

- **Mapa do código e invariantes**: [docs/agent-guide.md](docs/agent-guide.md)
  — ⚠️ leia antes de tocar em cripto/migração (`Cofre`, `CryptoCofre`,
  `DocumentoRepository.Migrar`).
- **Arquitetura**: [docs/architecture.md](docs/architecture.md)
- **Segurança e formatos**: [docs/security.md](docs/security.md)
- **CLI**: [docs/cli.md](docs/cli.md) · **UI**: [docs/ui.md](docs/ui.md) ·
  **Build/release**: [docs/development.md](docs/development.md)

## Build rápido

```powershell
dotnet build source/Memo.slnx -c Release
```

## Convenções essenciais

- Código, nomes e comentários em **português**.
- `Memo.Service` **não** depende de WPF (a UI só orquestra).
- Nunca logar `Value`; nunca versionar `vault.json`, `session*.bin`, `falhas/`,
  `*.pfx`.
- Em testes, **nunca** aponte para os dados reais do usuário — use um diretório
  temporário (`MEMO_DIR` ou `new MemoService("<dir temp>")`). Ver as lições do
  incidente de perda de dados em [docs/agent-guide.md](docs/agent-guide.md).
