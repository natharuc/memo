# Linha de comando

O mesmo executável (`Memo.exe`) é GUI e CLI: **sem argumentos** abre a interface;
**com argumentos** roda um comando e mostra um `Toast` (aviso flutuante) com o
resultado.

Roteamento em `source/Memo/App.xaml.cs` (`ExecutarLinhaDeComando`); a lógica em
`MemoService` (`source/Memo.Service/MemoService.cs`).

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
