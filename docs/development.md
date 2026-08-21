# Desenvolvimento

## Requisitos

- **.NET SDK 8+** (o repo foi construído com SDK 8/9/10 instalados; o alvo é
  `net8.0-windows`).
- **Windows** — o app usa WPF, DPAPI e a API DWM. Não é multiplataforma.

## Projetos

| Projeto | Alvo | Tipo |
|---------|------|------|
| `source/Memo` | `net8.0-windows`, `UseWPF` | WinExe (GUI; também aceita args, mas não captura stdout) |
| `source/Memo.Cli` | `net8.0-windows` | Exe console (`memo-cli.exe`) — CLI scriptável, saída em stdout |
| `source/Memo.Service` | `net8.0-windows` | biblioteca (núcleo compartilhado por GUI e CLI) |

### Dependências (`Memo.Service`)
- `Newtonsoft.Json` — serialização do `Documento` e do `vault.json`.
- `System.Security.Cryptography.ProtectedData` — DPAPI para o cache de sessão.
- `TextCopy` — clipboard.

`Memo` e `Memo.Cli` referenciam `Memo.Service` via `ProjectReference`.

## Build

```powershell
dotnet build source/Memo.slnx -c Release
```

(O SDK do .NET 10 criou a solution no formato novo `.slnx`. Funciona com
`dotnet` e com VS recente. Se preferir um `.sln` clássico, gere com
`dotnet sln`/VS.)

## Executar

```powershell
# GUI
dotnet run --project source/Memo

# CLI (note o "--" separando args do dotnet dos args do app)
dotnet run --project source/Memo -- get senha pollaris
dotnet run --project source/Memo -- set teste = 123
```

Ou rode o binário direto: `source/Memo/bin/Debug/net8.0-windows/Memo.exe`.

> ⚠️ **Cuidado ao rodar/testar**: sem `MEMO_DIR`, o app usa a pasta configurada
> do usuário (e a **sessão** pode estar válida, ignorando `--password`!). Para
> testar sem tocar nos dados reais, **defina `MEMO_DIR` para uma pasta temporária**
> (ou use `new MemoService("<dir de teste>")`). Veja as lições em
> [agent-guide.md](agent-guide.md).

## Publicação

Para gerar um pacote distribuível manualmente (pasta **self-contained**, não exige
runtime instalado) — o mesmo formato das releases:

```powershell
dotnet publish source/Memo/Memo.csproj -c Release -r win-x64 --self-contained true -o publish
dotnet publish source/Memo.Cli/Memo.Cli.csproj -c Release -r win-x64 --self-contained true -o publish
```

Gera a pasta `publish/` com `Memo.exe`, `memo-cli.exe` e as DLLs; zipe a pasta para
distribuir. **Não** usamos `PublishSingleFile`: o single-file de WPF apresentou
falha de abertura em algumas máquinas; a pasta com DLLs soltas é mais confiável.

Para usar como comando `memo`, coloque a pasta no `PATH` ou crie um atalho para o `.exe`.

## Releases (GitHub Actions)

A esteira `.github/workflows/release.yml` roda ao empurrar uma tag `v*`. Publica a
GUI e o `memo-cli` (self-contained, **em pasta**, win-x64), assina os exes (se
houver certificado) e cria a GitHub Release com:

- **`Memo-win-x64.zip`** — o **artefato de distribuição**: a pasta inteira (exes +
  DLLs + runtime) — mais `Memo-win-x64.zip.sha256`;
- **`latest.json`** — manifesto `{ version, url (do zip), sha256 (do zip) }`.

O **auto-update baixa o zip** (ver abaixo). O usuário baixa o zip, **extrai** e roda
`Memo.exe`.

```powershell
# a versão sai da tag; o CI passa -p:Version=<tag sem o "v"> ao publish
git tag v1.2.0
git push origin v1.2.0
```

`<Version>` em `source/Memo/Memo.csproj` é só o baseline para builds locais — em
release quem manda é a tag. Mantenha `app.manifest` alinhado.

### Assinatura (certificado auto-assinado)

O passo de assinatura roda se os secrets `SIGN_PFX_BASE64` e `SIGN_PFX_PASSWORD`
existirem. Para gerar um certificado auto-assinado e cadastrá-lo:

```powershell
$c = New-SelfSignedCertificate -Type CodeSigning -Subject "CN=Memo" -CertStoreLocation Cert:\CurrentUser\My
Export-PfxCertificate -Cert $c -FilePath memo.pfx -Password (ConvertTo-SecureString "<senha>" -AsPlainText -Force)
[Convert]::ToBase64String([IO.File]::ReadAllBytes("memo.pfx")) | Set-Content memo.pfx.b64
```

Depois cadastre em **Settings → Secrets and variables → Actions**:
`SIGN_PFX_BASE64` (conteúdo de `memo.pfx.b64`) e `SIGN_PFX_PASSWORD`.

> Cert auto-assinado **não** remove o aviso do SmartScreen (isso exige um cert EV
> com reputação). Ele serve para integridade e consistência de publisher, e o
> `.pfx`/`.b64` **nunca** deve ser versionado.

## Auto-update

`Memo.Service/Atualizacao/AtualizadorService.cs` consulta a release mais recente
(`releases/latest` da API do GitHub) no startup, em background. Se a tag for maior
que a versão atual, abre a `JanelaAtualizacao`; ao confirmar:

1. baixa o **pacote `.zip`** da release (o primeiro asset `.zip`) e **valida o
   SHA256** (asset `*.zip.sha256`);
2. extrai para uma pasta de staging (`%LOCALAPPDATA%\Memo\update`);
3. como o deploy é **em pasta** (DLLs carregadas ficam travadas com o app rodando),
   escreve um script `.cmd` que **espera este processo sair**, copia os arquivos
   novos por cima (`robocopy`) e **reinicia** o `Memo.exe`; o app então encerra.

Falha de rede é silenciosa. O updater **não** toca no vault nem no cache de sessão.

## Site (GitHub Pages)

A landing page fica em `site/` e é publicada por `.github/workflows/pages.yml` a
cada push na `main` que altere `site/`. Habilite em **Settings → Pages → Source:
GitHub Actions**. O botão de download aponta para `releases/latest`.

## Convenções de código

- **Idioma**: código, nomes e comentários em **português** (ex.: `Cofre`,
  `Cifrar`, `DocumentoRepository`). Mantenha o padrão.
- **Estilo**: C# convencional; chaves em nova linha; `var` quando o tipo é óbvio.
  `Nullable` desabilitado (`<Nullable>disable</Nullable>`).
- **Sem MVVM** na UI — code-behind direto e simples.
- **`Memo.Service` não conhece WPF.** Mantenha a lógica de negócio nele; a UI só
  orquestra.

## `.gitignore` e segredos

`source/.gitignore` exclui `bin/`, `obj/`, `*.user`, `*.pfx`, e também
`vault.json`, `session*.bin` e `falhas/`. **Nunca** versione cofres, salts,
sessões ou certificados. A pasta-cofre é escolhida pelo usuário e normalmente fica
**fora** do repositório (ex.: OneDrive); esses padrões são uma rede de segurança
caso um cofre (ou um `MEMO_DIR` de teste) acabe dentro do repo.

## Testes

Não há projeto de teste versionado. Durante o desenvolvimento, valida-se com
consoles descartáveis que referenciam `Memo.Service` e usam um **diretório
temporário** (jamais a pasta real do usuário; use `MEMO_DIR`). Um bom teste cobre: round-trip de
cifragem (inclusive Unicode), senha errada rejeitada, detecção de adulteração
(GCM), leitura/migração do formato legado, e bloqueio de path-traversal.
Adicionar um projeto xUnit é uma melhoria recomendada.
