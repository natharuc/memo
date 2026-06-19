# Linha de comando

Há **dois jeitos** de usar o Memo pelo terminal:

1. **`memo-cli.exe`** (projeto `source/Memo.Cli`) — um app **console** de verdade:
   a saída vai pro **stdout** e é **capturável** por scripts/outros sistemas
   (`$(memo-cli get x --text)`, pipes, etc.), com **exit codes**. É a interface
   recomendada para automação. Veja [CLI console](#cli-console-memo-cli).
2. **`Memo.exe`** (a própria GUI) — sem argumentos abre a interface; **com
   argumentos** roda o comando e mostra um `Toast`, copiando para a área de
   transferência. Bom para uso interativo, **não** para capturar saída (é um app
   GUI; o shell não espera nem captura o stdout).

Roteamento da GUI em `source/Memo/App.xaml.cs`; lógica compartilhada em
`MemoService` (`source/Memo.Service/MemoService.cs`) — o mesmo núcleo usado pelos dois.

---

## CLI console (`memo-cli`)

App console que "cospe" o resultado no stdout. Implementado em
`source/Memo.Cli/Program.cs` sobre o `Memo.Service`.

### Formatos de saída
| Flag | Efeito |
|------|--------|
| `--text` | (padrão) valor cru no stdout |
| `--json` | objeto/array JSON |
| `--bytes` | bytes crus (binário) no stdout — bom para pipe |
| `--copy` | copia para a área de transferência em vez de imprimir |

### Comandos
```
memo-cli get <chave> [--json|--text|--bytes|--copy]
memo-cli set <chave> <valor>        # ou <chave>=<valor>, --value <v>, --stdin
memo-cli list [--json]
memo-cli del <chave>
memo-cli remember <texto/quando>    # mesmas regras do "memo remember"
memo-cli pass [<chave>] [--json]
memo-cli guid [--json]
memo-cli unlock | lock
memo-cli migrar
memo-cli version | help
```

### Cofre trancado (automação)
Comandos que leem/gravam segredos precisam do cofre destrancado. A ordem é:
1. **sessão** válida (DPAPI, 15 min) — reaproveitada entre processos;
2. `--password <senha>` ou variável **`MEMO_PASSWORD`**;
3. **prompt** mascarado, se rodando num terminal interativo.

Fluxo típico em scripts: `memo-cli unlock --password "$PW"` uma vez, depois
`memo-cli get x --json` durante a janela da sessão.

### Variáveis de ambiente
- **`MEMO_DIR`** — aponta o cofre para outro diretório (padrão: pasta no OneDrive).
- **`MEMO_PASSWORD`** — senha-mestra para destravar sem prompt.

### Exit codes
`0` ok · `1` erro · `2` cofre trancado · `3` não encontrado · `64` uso incorreto.

### Exemplos
```bash
memo-cli unlock --password "$MEMO_PW"
valor=$(memo-cli get "github token" --text)
memo-cli list --json | jq -r '.[]'
echo -n "$SEGREDO" | memo-cli set "api key" --stdin
memo-cli get "chave bin" --bytes > saida.bin
```

---

## GUI em modo CLI (`Memo.exe <args>`)

> Mantido para uso interativo; copia para a área de transferência e mostra um
> `Toast`. Não é capturável (app GUI).

## Comandos

### `memo get <chave>`
Copia o **valor** do documento para a área de transferência.

```
memo get senha pollaris
```

- A chave pode conter espaços (tudo após `get` é a chave): `args.Skip(1)` juntado
  por espaço (`MemoService.ProcessarGet`).
- Se não existir: aviso `"<chave>" não encontrado`.

### `memo set <chave> = <valor>`
Cria ou atualiza um documento cifrado.

```
memo set senha pollaris = minhaSenha123
memo set token azure = eyJ...
memo set so a chave            (cria com valor vazio)
```

- Separador é o **primeiro `=`**. À esquerda vira a chave, à direita o valor
  (ambos com `Trim`). Sem `=`, tudo vira chave e o valor fica vazio
  (`MemoService.ProcessarSet`).
- A chave é sanitizada (sem `/`, `\`, `..`).

### `memo new`
Abre a janela de **novo documento** (modo gráfico). Ao salvar, grava o documento e
mostra um `Toast`; ao cancelar, encerra sem fazer nada.

### `memo guid`
Gera um **GUID** novo e copia para a área de transferência.

```
memo guid        →  GUID copiado: 3f2a... 
```

### `memo pass [<chave>]`
Gera uma **senha** usando as preferências salvas do usuário
(`Configuracoes.Senha`: comprimento e tipos de caractere) e copia para a área de
transferência. Se uma **chave** for informada, também salva o documento.

```
memo pass              copia uma senha nova
memo pass foo bar      cria o documento "foo bar" com a senha e copia
memo pass {foo bar}    chaves entre { } também funcionam
```

- As preferências vêm da tela **Gerar senha** (botão na janela de novo/editar);
  o que o usuário configurar lá é reaproveitado aqui (`MemoService.ProcessarPass`).

### `memo lock`
**Tranca o cofre**: apaga o cache de sessão (`Cofre.Trancar`). O próximo acesso
(`get`/`set`/GUI) vai pedir a senha. Não pede senha para trancar.

> Trata uma janela do app **já aberta**? Não — `memo lock` roda em outro processo
> e só invalida a sessão em disco. Para trancar uma janela aberta, clique no badge
> de sessão na tela principal.

### `memo unlock`
**Destranca pedindo a senha**: ignora a sessão atual e abre a janela de senha
(`JanelaSenha`). Útil para "esquentar" a sessão antes de usar `get` em scripts.

### `memo remember <texto> <quando>`
Cria um lembrete em **linguagem natural** (PT/EN). Não pede a senha-mestra
(lembrete não é segredo). Aliases: `lembrar`, `lembrete`.

```
memo remember ver tarefa 477987 10:00 tomorrow   # amanhã 10:00
memo remember ver tarefa 477987 10:00            # hoje 10:00 (ou amanhã, se já passou)
memo remember ver tarefa 477987 22h              # hoje 22:00
memo remember ver tarefa 477987 22h tomorrow     # amanhã 22:00
memo remember beber agua every 30 minutes        # repete a cada 30 min
memo remember ligar joão in 15 minutes           # daqui a 15 min
```

Regras (em [ParserLembrete](../source/Memo.Service/Lembretes/ParserLembrete.cs)):
- **Recorrência**: `every N minutes|hours` ou `a cada N minutos|horas` → repete; o
  primeiro disparo é daqui a N. Tem prioridade sobre hora do dia.
- **Relativo**: `in N minutes|hours`, `daqui [a] N min`, `em N horas`.
- **Hora do dia**: `HH:mm` (10:00), `HHh` (22h), `HHhMM` (22h30). Com `tomorrow`/
  `amanhã` vai para o dia seguinte; sem dia e já passou hoje → joga para amanhã.
- O **texto** é o que sobra depois de tirar os termos de tempo. Números soltos
  (ex.: `477987`) não são confundidos com hora (exige `:` ou `h`).

> O lembrete é gravado no `lembretes.json`; quem dispara é o app na bandeja. Se
> não houver instância na bandeja rodando, ele só aparece quando o app abrir.

### `memo migrar`
Recifra todos os documentos no formato atual e move para `falhas/` os que não
abrirem. Mostra um resumo `X migrado(s), Y ok, Z em quarentena`.

> A GUI também roda uma passada de migração automaticamente ao abrir (auto-reparo).

## Cofre trancado na CLI

Se a sessão não estiver válida, **mesmo um comando de CLI abre a janela de senha**
(`JanelaSenha`) para destrancar/criar o cofre, e só então executa. Depois de
destrancar uma vez, os próximos `get`/`set` dentro do prazo da sessão não pedem
senha. O prazo é **absoluto** (conta a partir da senha digitada, não renova a
cada uso) e configurável (`Configuracoes.DuracaoSessao`).

## Como o usuário chama `memo`

O projeto gera `Memo.exe`. Para usar como `memo`, aponte um atalho/alias ou
adicione a pasta do executável ao `PATH`. (Não há instalador no repo; ver
[development.md](development.md) → Publicação.)

## Códigos de retorno

A CLI hoje sempre encerra após o `Toast` (não define exit code por sucesso/erro).
Se precisar de exit codes para scripts, é um ponto a implementar em
`App.ExecutarLinhaDeComando`.
