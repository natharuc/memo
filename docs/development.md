# Desenvolvimento

## Requisitos

- **.NET SDK 8+** (o repo foi construído com SDK 8/9/10 instalados; o alvo é
  `net8.0-windows`).
- **Windows** — o app usa WPF, DPAPI e a API DWM. Não é multiplataforma.

## Projetos

| Projeto | Alvo | Tipo |
|---------|------|------|
| `source/Memo` | `net8.0-windows`, `UseWPF` | WinExe (GUI + CLI) |
| `source/Memo.Service` | `net8.0-windows` | biblioteca |

### Dependências (`Memo.Service`)
- `Newtonsoft.Json` — serialização do `Documento` e do `vault.json`.
- `System.Security.Cryptography.ProtectedData` — DPAPI para o cache de sessão.
- `TextCopy` — clipboard.

`Memo` referencia `Memo.Service` via `ProjectReference`.

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

> ⚠️ **Cuidado ao rodar/testar**: o app real usa `MemoService.DiretorioPadrao`
> (a pasta de documentos do usuário). Para experimentar sem tocar nos dados
> reais, use `new MemoService("<diretório de teste>")` num projeto de teste, ou
> ajuste o diretório. Veja as lições em [agent-guide.md](agent-guide.md).

## Publicação

Para gerar um executável distribuível manualmente:

```powershell
# self-contained, arquivo único (não exige runtime instalado) — formato das releases:
dotnet publish source/Memo/Memo.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtraction=true
```

Para usar como comando `memo`, coloque o `.exe` no `PATH` ou crie um atalho/alias.

## Releases (GitHub Actions)

A esteira `.github/workflows/release.yml` roda ao empurrar uma tag `v*` e cria a
GitHub Release com o `Memo.exe` (single-file, self-contained, win-x64), o
`Memo.exe.sha256` e um `latest.json` (manifesto consumido pelo auto-update).

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
(`releases/latest` da API do GitHub) no startup, em background. Se houver versão
maior, abre a `JanelaAtualizacao` perguntando se quer atualizar; ao confirmar,
baixa o `Memo.exe`, **valida o SHA256**, renomeia o exe atual para `.old`, põe o
novo no lugar e reinicia. Resíduos `.old` são removidos no próximo start. Falha de
rede é silenciosa. O updater **não** toca no vault nem no cache de sessão.

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
`documents/`, `vault.json`, `session*.bin`. **Nunca** versione cofres, salts,
sessões ou certificados.

## Testes

Não há projeto de teste versionado. Durante o desenvolvimento, valida-se com
consoles descartáveis que referenciam `Memo.Service` e usam um **diretório
temporário** (jamais o `DiretorioPadrao`). Um bom teste cobre: round-trip de
cifragem (inclusive Unicode), senha errada rejeitada, detecção de adulteração
(GCM), leitura/migração do formato legado, e bloqueio de path-traversal.
Adicionar um projeto xUnit é uma melhoria recomendada.
