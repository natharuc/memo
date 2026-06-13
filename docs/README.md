# Documentação do Memo

Documentação completa da aplicação. Escrita para humanos e para agentes de IA
que vão dar manutenção no código.

## Índice

| Documento | Conteúdo |
|-----------|----------|
| [agent-guide.md](agent-guide.md) | **Comece aqui se você é um agente de IA.** Mapa do código, como fazer mudanças comuns, invariantes que não podem ser quebradas e lições do incidente de perda de dados. |
| [architecture.md](architecture.md) | Visão geral, componentes, fluxo de dados e camadas. |
| [security.md](security.md) | Criptografia, formatos de arquivo (vault.json, documentos, sessão), modelo de ameaças e o formato legado. |
| [cli.md](cli.md) | Referência da linha de comando (`get`, `set`, `migrar`). |
| [ui.md](ui.md) | A interface WPF: janelas, comportamento e tema. |
| [development.md](development.md) | Build, execução, publicação, dependências e convenções de código. |

## Resumo de uma linha

Memo é um cofre de segredos file-based: cada segredo é um arquivo cifrado em
AES-256-GCM, com chave derivada de uma senha-mestra (PBKDF2). App WPF/.NET 8 +
biblioteca `Memo.Service`.

## Glossário rápido

- **Cofre (vault)**: o conjunto `vault.json` + arquivos de documentos de um
  diretório, protegido por uma senha-mestra.
- **Documento**: um par `{ Key, Value }`. `Key` é o nome (e o nome do arquivo);
  `Value` é o segredo.
- **Sessão**: cache temporário da chave-mestra para não pedir a senha a cada uso.
- **Formato legado**: o esquema de criptografia antigo (AES-128-CBC com chave
  fixa), lido apenas para migração.
