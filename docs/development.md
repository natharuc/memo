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

Não há instalador no repo. Para gerar um executável distribuível:

```powershell
dotnet publish source/Memo -c Release -r win-x64 --self-contained false
# ou self-contained (não exige runtime instalado):
dotnet publish source/Memo -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Para usar como comando `memo`, coloque o `.exe` no `PATH` ou crie um atalho/alias.

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
