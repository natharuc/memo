# Segurança e formatos

Toda a criptografia está em `source/Memo.Service/Seguranca/`
(`CryptoCofre.cs` = primitivas, `Cofre.cs` = ciclo de vida).

## Modelo

- Uma **senha-mestra** protege todos os documentos de um diretório.
- A chave de 256 bits é derivada com **PBKDF2-SHA256**, **200.000 iterações**,
  com um **salt** aleatório de 16 bytes por cofre (guardado no `vault.json`).
- Cada documento é cifrado com **AES-256-GCM** (cifra autenticada), com **nonce
  aleatório de 12 bytes** por arquivo. A tag de autenticação (16 bytes) detecta
  adulteração.
- A senha-mestra **nunca** é gravada. A chave derivada só existe em memória e,
  temporariamente, no cache de sessão (protegido por DPAPI).

## Constantes (`CryptoCofre`)

| Constante | Valor |
|-----------|-------|
| `Versao` | `0x02` (primeiro byte do formato novo) |
| `TamanhoChave` | 32 (AES-256) |
| `TamanhoSalt` | 16 |
| nonce GCM | 12 bytes |
| tag GCM | 16 bytes |
| iterações PBKDF2 | 200.000 (`Cofre.IteracoesPadrao`) |
| hash PBKDF2 | SHA-256 |

## Formato de um documento (formato novo, v2)

Conteúdo do arquivo = **Base64** de:

```
┌──────┬───────────────┬──────────────┬───────────────────────────┐
│ 0x02 │ nonce (12 B)  │ tag (16 B)   │ ciphertext (N B)          │
└──────┴───────────────┴──────────────┴───────────────────────────┘
  versão
```

O ciphertext é `AES-256-GCM(chave, nonce)` aplicado ao **JSON do `Documento`**
(`{"Key":"...","Value":"..."}`). Implementado em `CryptoCofre.Cifrar` /
`CryptoCofre.TentarDecifrar`.

## `vault.json`

Fica **junto dos documentos** (no diretório, portanto sincroniza junto). Ex.:

```json
{
  "Versao": 1,
  "Salt": "<base64 de 16 bytes>",
  "Iteracoes": 200000,
  "Verificador": "<documento v2 cifrando a string 'memo-cofre-ok'>"
}
```

- **Salt** + **Iteracoes** = parâmetros do PBKDF2 (precisam viajar com os dados;
  sem o salt, a chave é irrecuperável mesmo com a senha certa).
- **Verificador** = `Cifrar("memo-cofre-ok", chave)`. Para validar uma senha,
  deriva-se a chave e tenta-se decifrar o verificador: se voltar `"memo-cofre-ok"`,
  a senha está certa (`Cofre.Destrancar` / `ChaveAbreCofre`).

> ⚠️ **O `vault.json` é insubstituível.** Se ele for sobrescrito por um com salt
> diferente, os documentos cifrados com o salt antigo **não abrem mais**, mesmo
> com a senha correta. Trate-o como parte dos dados, não como config descartável.

## Cache de sessão

Arquivo: `%LOCALAPPDATA%\Memo\session-<id>.bin`, onde `<id>` são os 8 primeiros
bytes (hex) de `SHA256(caminho-do-diretório normalizado e em minúsculas)`
(`Cofre.IdDiretorio`). **Isolado por diretório** — cofres/testes diferentes nunca
compartilham sessão.

- Conteúdo (antes do DPAPI): `[expiraTicks (8 bytes)] [chave (32 bytes)]`.
- Protegido com **DPAPI** (`ProtectedData.Protect`, escopo `CurrentUser`) — só o
  mesmo usuário Windows consegue ler.
- **Validade: 15 min, deslizante** (renova a cada uso).
- **Validação obrigatória** (`ChaveAbreCofre`): ao carregar a sessão, a chave só
  é aceita se decifrar o verificador do `vault.json` atual. Isso impede que uma
  sessão antiga/estranha re-cifre documentos com a chave errada.

## Formato legado (somente leitura)

Esquema antigo, mantido só para migrar documentos antigos:

- **AES-128-CBC**, PKCS7.
- Chave = UTF-8 de `"9784612435679864"` (16 bytes). **Conhecida/pública** — não
  oferece segurança real; serve apenas para conseguir abrir e recifrar arquivos
  antigos.
- IV fixo = `{1,2,…,16}`.
- `CryptoCofre.TentarDecifrarLegado` decifra; `EhFormatoLegado` detecta (primeiro
  byte ≠ `0x02`).
- `Cofre.TentarDecifrar` tenta o formato novo e, se falhar, tenta o legado em até
  **4 camadas** (havia documentos com cifragem dupla por um bug antigo de
  `--rebuild`).

A migração reescreve esses arquivos no formato novo (ver [cli.md](cli.md) →
`migrar` e `DocumentoRepository.Migrar`).

## Anti path-traversal

A chave do documento vira nome de arquivo. `DocumentoRepository.SanitizarChave`
rejeita qualquer chave cujo `Path.GetFileName` difira dela (bloqueia `..`, `/`,
`\`). Toda leitura/escrita/exclusão passa por aí.

## Modelo de ameaças (o que protege e o que não)

**Protege contra:**
- Leitura dos arquivos por quem não tem a senha (cifra autenticada + KDF forte).
- Adulteração dos arquivos (GCM detecta).
- Vazamento via sincronização na nuvem — o conteúdo no provedor é só ciphertext.

**Não protege contra:**
- Senha-mestra fraca (PBKDF2 ajuda, mas senha boa é responsabilidade do usuário).
- Malware/keylogger na máquina do usuário com a sessão ativa.
- Perda do `vault.json` (salt) ou da senha → dados irrecuperáveis (por design).
- Segredo exposto no clipboard após `get` (fica no histórico do Windows; não há
  limpeza automática hoje — possível melhoria).

## Histórico

A chave legada `9784612435679864` apareceu em commits antigos (no repo monolítico
anterior). Ela só protege arquivos legados, que são recifrados na migração — mas
considere o histórico "queimado" para fins de segurança.
