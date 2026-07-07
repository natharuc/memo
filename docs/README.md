# Documentação do Memo

Documentação completa da aplicação. Escrita para humanos e para agentes de IA
que vão dar manutenção no código.

## Índice

| Documento | Conteúdo |
|-----------|----------|
| [agent-guide.md](agent-guide.md) | **Comece aqui se você é um agente de IA.** Mapa do código, como fazer mudanças comuns, invariantes que não podem ser quebradas e lições do incidente de perda de dados. |
| [architecture.md](architecture.md) | Visão geral, componentes, fluxo de dados e camadas. |
| [security.md](security.md) | Criptografia, formatos de arquivo (vault.json, documentos, sessão), modelo de ameaças e o formato legado. |
| [cli.md](cli.md) | Linha de comando: o `memo-cli` (console scriptável, exit codes) e a GUI em modo CLI. |
| [ui.md](ui.md) | A interface WPF: janelas, comportamento e tema. |
| [rail.md](rail.md) | Memo Rail: assistente de foco (missão do dia, check-ins, detector de desvio). |
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
- **Lembrete**: aviso em linguagem natural (**não é segredo**), disparado pelo app
  na bandeja. Guardado em `lembretes.json`.
- **Canal de notificação**: destino externo (Telegram ou e-mail/SMTP) para onde o
  `memo notify` envia mensagens. Configurado na aba Notificações; credenciais
  cifradas por DPAPI em `notificacoes.bin`.
- **Missão do dia (Memo Rail)**: checklist do assistente de foco (**não é
  segredo**), guardado em `rail.json` como um pool de tarefas com data —
  pendências de dias anteriores acumulam como **atrasadas**. O Rail faz
  check-ins e avisa desvios; ver [rail.md](rail.md).
