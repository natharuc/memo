# Interface (WPF)

App WPF em `source/Memo`. Os **estilos** ficam em `Tema.xaml` e as **cores** em
`PaletaEscura.xaml` / `PaletaClara.xaml` (ambas mescladas em `App.xaml`, paleta no
índice 0). Os estilos referenciam as cores via **`DynamicResource`**, então
`Tema.Aplicar(...)` troca a paleta em runtime e todas as janelas abertas atualizam.
A barra de título acompanha o tema via `Nativo.AplicarBarraTitulo`
(`DwmSetWindowAttribute`).

Preferências (tema e duração da sessão) ficam em `Configuracoes` (Memo.Service),
gravadas em `%LOCALAPPDATA%\Memo\config.json`.

## Janelas

### `JanelaSenha` (`JanelaSenha.xaml[.cs]`)
Criar ou destrancar o cofre.
- Se o cofre **não existe** (`!Cofre.Inicializado`): modo **criar** — dois campos
  (senha + confirmar), mínimo de 4 caracteres, chama `Cofre.Inicializar`.
- Se **existe**: modo **destrancar** — um campo, valida com `Cofre.Destrancar`.
- API: `static bool JanelaSenha.Solicitar(Cofre)` → `true` se destrancou/criou.

### `JanelaPrincipal` (`JanelaPrincipal.xaml[.cs]`)
Tela principal.
- **Busca** (topo): filtra por substring da chave (case-insensitive). Enter copia
  o selecionado/primeiro; ↓ foca a lista.
- **Lista** (esquerda): documentos por `Key` (`ObservableCollection<Documento>`).
- **Painel de detalhes** (direita): mostra a chave e o valor. O valor começa
  **oculto** (`••••••`); botão **Mostrar/Ocultar** alterna.
- **Ações**: **Copiar** (clipboard), **Mostrar/Ocultar**, **Editar**, **Excluir**
  (confirmação via `JanelaDialogo`, avisando que é **definitiva/irreversível** —
  não há lixeira), **+ Novo**.
- Duplo-clique ou Enter na lista = copiar; Delete = excluir.
- **Badge de sessão** (`botaoSessao`, topo): contagem regressiva até pedir a senha
  de novo (`Cofre.TempoRestante`), atualizada por um `DispatcherTimer` de 1s. Fica
  vermelho no último minuto. **Clicar TRANCA o cofre na hora** (`Sessao_Click` →
  `Bloquear()`) — útil para entregar o PC já trancado.
- **Bloqueio** (`Bloquear()`): para o timer, limpa a lista/detalhes/busca, chama
  `Cofre.Trancar()` e mostra o **overlay `overlayBloqueio`** (opaco, esconde os
  segredos). Acontece ao clicar no badge **ou** quando a contagem zera. O botão
  **Destrancar** do overlay (`Destrancar_Click`) pede a senha
  (`JanelaSenha.Solicitar`) e, se ok, recarrega e religa o timer.
- A barra de status (rodapé) mostra o último resultado.

### `JanelaEditar` (`JanelaEditar.xaml[.cs]`)
Criar/editar um documento.
- `static Documento JanelaEditar.Criar(owner)` e `Editar(owner, doc)` → devolvem
  o `Documento` ou `null` (cancelado).
- Ao **editar**, a chave fica travada (renomear trocaria o arquivo; não suportado
  hoje). Ao **criar**, a chave é validada por `DocumentoRepository.SanitizarChave`.

### `JanelaConfiguracoes` (`JanelaConfiguracoes.xaml[.cs]`)
Preferências do usuário, aberta pelo botão **⚙** na `JanelaPrincipal`. Organizada
em **abas** (`TabControl`, estilizado em `Tema.xaml`):
- **Preferências**: **Tema** (Escuro/Claro, com pré-visualização ao vivo revertida
  se cancelar) e **Tempo para guardar a senha** (presets 15 min … 1 dia →
  `Configuracoes.DuracaoSessaoMinutos`, usado pelo `Cofre` na expiração).
- **Atalhos**: texto didático explicando cada comando de CLI (`get`, `set`, `new`,
  `pass`, `guid`, `lock`, `unlock`, `migrar`). É só conteúdo estático — para mudar
  a explicação, edite o XAML; a referência completa fica em [cli.md](cli.md).
- `static bool JanelaConfiguracoes.Mostrar(owner)` → `true` se o usuário salvou
  (a `JanelaPrincipal` usa isso para re-ancorar a sessão com a nova duração).

### `JanelaGerarSenha` (`JanelaGerarSenha.xaml[.cs]`)
Gerador de senha: comprimento e tipos de caractere (maiúsculas, minúsculas,
números, símbolos). As preferências são salvas em `Configuracoes.Senha` e
**reusadas pelo comando `pass`** (GUI e `memo-cli`). Aberta pelo botão **Gerar
senha** nas janelas de novo/editar documento.

### `JanelaLembrete` (`JanelaLembrete.xaml[.cs]`)
Popup de um lembrete que **venceu**: mostra o texto e oferece **Concluir** ou
**Adiar** (soneca, N min). Disparado pelo agendador da bandeja (ver
[Bandeja e lembretes](#bandeja-e-lembretes)).

### `JanelaLembretes` (`JanelaLembretes.xaml[.cs]`)
Lista dos lembretes ativos, aberta pelo menu da bandeja ("Lembretes…") ou pelo
botão na `JanelaPrincipal`. Permite ver/remover lembretes.

### `JanelaAtualizacao` (`JanelaAtualizacao.xaml[.cs]`)
Aparece quando o auto-update encontra uma versão mais nova: mostra a versão e
oferece baixar/instalar (ver [development.md](development.md) → Auto-update).

### `JanelaDialogo` (`JanelaDialogo.xaml[.cs]`)
Diálogo temizado que substitui o `MessageBox` do Windows.
`static bool JanelaDialogo.Confirmar(owner, titulo, msg, perigo, aviso)` (Sim/Não;
`aviso` opcional mostra um **rótulo de alerta destacado** em cor de perigo, com
ícone ⚠ — usado na exclusão para deixar claro que é **irreversível**) e
`static void JanelaDialogo.Informar(owner, titulo, msg)` (só OK).

### `Toast` (`Toast.xaml[.cs]`)
Aviso flutuante no canto inferior direito, com fade-in/out e auto-fechamento
(~2,2 s). Usado no **modo CLI** da GUI para mostrar o resultado. `static void
Toast.Mostrar(mensagem, sucesso)`.

## Bandeja e lembretes

Sem argumentos (ou com `--tray`), o `Memo.exe` roda como app de **bandeja**
(ícone na área de notificação), em **instância única** (`InstanciaUnica`): uma 2ª
execução só pede para a janela existente aparecer e sai.

- **Menu da bandeja**: *Abrir Memo*, *Lembretes…*, *Sair*. Duplo-clique abre a
  janela. Fechar a `JanelaPrincipal` no **X** apenas esconde na bandeja (o app
  segue rodando para disparar lembretes); para encerrar de vez, use *Sair*.
- **Agendador**: um `DispatcherTimer` (~20 s) verifica os lembretes **devidos** e
  abre uma `JanelaLembrete` (+ balão na bandeja e som via `Som.cs`). Detalhes do
  modelo em [architecture.md](architecture.md) → Lembretes.

## Padrões de UI

- **Sem MVVM**: code-behind direto, simples. A `JanelaPrincipal` mantém a lista
  completa e uma `ObservableCollection` filtrada.
- **Diálogos temizados** (`JanelaDialogo.Confirmar`/`Informar`) no lugar do
  `MessageBox` do Windows; janelas próprias para senha e edição.
- **Segredos ocultos por padrão** na tela; só aparecem ao clicar em Mostrar.
- Estilos reutilizáveis em `Tema.xaml`: `BotaoPrimario`, `BotaoPerigo`,
  `ItemDocumento`, além de estilos default para `TextBox`/`PasswordBox`/`Button`.

## Como adicionar uma tela

1. Crie `MinhaJanela.xaml` + `.cs` em `source/Memo` (o SDK inclui `.xaml`
   automaticamente com `UseWPF`).
2. Use `Background="{DynamicResource CorFundo}"` e os estilos do `Tema.xaml`
   (sempre **`DynamicResource`** para cores, senão a tela não troca de tema).
3. Chame `Nativo.AplicarBarraTitulo(this)` no construtor.
