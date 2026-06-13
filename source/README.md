# Memo

Cofre pessoal de segredos, file-based e criptografado. Guarda senhas/anotações
em arquivos cifrados e permite acesso rápido pela linha de comando ou por uma
interface WPF.

## Uso (linha de comando)

```
memo get <chave>            # copia o valor para a área de transferência
memo set <chave> = <valor>  # cria/atualiza um documento cifrado
memo migrar                 # recifra documentos antigos no formato atual
memo                        # abre a interface gráfica
```

## Como funciona

- **Um arquivo cifrado por segredo**, num diretório que pode ser sincronizado
  (ex.: OneDrive). Esse modelo file-based é proposital — é o diferencial do Memo.
- **Cofre com senha-mestra**: a chave é derivada da senha via PBKDF2-SHA256
  (200k iterações), e cada documento é cifrado com **AES-256-GCM** (autenticado),
  com salt e nonce aleatórios. A senha-mestra nunca é gravada nem fica no código.
- **Sessão**: após destrancar, a chave fica em cache por 15 min (protegida por
  DPAPI, isolada por diretório e validada contra o cofre atual), para o `get`
  não pedir senha a cada uso.

## Estrutura

- `Memo/` — aplicativo WPF (.NET 8) e entrada de linha de comando.
- `Memo.Service/` — núcleo: cofre, criptografia e repositório de documentos.

## Build

```
dotnet build Memo.sln -c Release
```
