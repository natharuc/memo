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

### `memo migrar`
Recifra todos os documentos no formato atual e move para `falhas/` os que não
abrirem. Mostra um resumo `X migrado(s), Y ok, Z em quarentena`.

> A GUI também roda uma passada de migração automaticamente ao abrir (auto-reparo).

## Cofre trancado na CLI

Se a sessão (15 min) não estiver válida, **mesmo um comando de CLI abre a janela
de senha** (`JanelaSenha`) para destrancar/criar o cofre, e só então executa.
Depois de destrancar uma vez, os próximos `get`/`set` dentro de 15 min não pedem
senha.

## Como o usuário chama `memo`

O projeto gera `Memo.exe`. Para usar como `memo`, aponte um atalho/alias ou
adicione a pasta do executável ao `PATH`. (Não há instalador no repo; ver
[development.md](development.md) → Publicação.)

## Códigos de retorno

A CLI hoje sempre encerra após o `Toast` (não define exit code por sucesso/erro).
Se precisar de exit codes para scripts, é um ponto a implementar em
`App.ExecutarLinhaDeComando`.
