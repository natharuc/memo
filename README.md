# Memo

Cofre pessoal de segredos — **file-based** e criptografado. Guarda senhas e
anotações em arquivos cifrados (um por segredo) e dá acesso rápido pela linha de
comando ou por uma interface gráfica (WPF/.NET 8).

```
memo get senha pollaris          # copia o valor para a área de transferência
memo set senha pollaris = 1234   # cria/atualiza um documento cifrado
memo                             # abre a interface gráfica
```

## Por que existe

Um gerenciador de segredos mínimo, sob controle do próprio usuário: cada segredo
é **um arquivo cifrado** num diretório que pode ser sincronizado (ex.: OneDrive).
Esse modelo file-based é proposital — é o diferencial do Memo, não um acidente.

## Características

- **Cofre com senha-mestra.** A chave é derivada da senha via **PBKDF2-SHA256**
  (200k iterações). Cada documento é cifrado com **AES-256-GCM** (cifra
  autenticada), com salt e nonce aleatórios. A senha nunca é gravada nem fica
  no código.
- **Sessão em cache (15 min)** protegida por **DPAPI**, isolada por diretório e
  validada contra o cofre, para o `memo get` não pedir senha a cada uso.
- **Interface WPF** com tema escuro, busca, painel de detalhes, e diálogos
  nativos.
- **Resiliência**: leitura tolerante a falhas e migração transparente de
  documentos em formato antigo.

## Estrutura do repositório

```
/                 (este README + docs/)
docs/             Documentação completa (ver docs/README.md)
source/           Código-fonte
  Memo.slnx         Solution
  Memo/             App WPF (.NET 8) + entrada de linha de comando
  Memo.Service/     Núcleo: cofre, criptografia e repositório
```

## Começando

```powershell
dotnet build source/Memo.slnx -c Release
```

Detalhes de build, execução e publicação em [docs/development.md](docs/development.md).

## Documentação

Comece por **[docs/README.md](docs/README.md)**. Para um agente de IA que vai
trabalhar no código, o ponto de partida é **[docs/agent-guide.md](docs/agent-guide.md)**.

> ⚠️ **Atenção a quem mexe na criptografia ou na migração:** leia
> [docs/security.md](docs/security.md) e a seção de incidentes em
> [docs/agent-guide.md](docs/agent-guide.md) **antes** de alterar `Cofre`,
> `CryptoCofre` ou `DocumentoRepository.Migrar`. Um erro ali pode tornar
> segredos irrecuperáveis.
