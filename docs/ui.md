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
  (com confirmação via `MessageBox` nativo), **+ Novo**.
- Duplo-clique ou Enter na lista = copiar; Delete = excluir.
- A barra de status (rodapé) mostra o último resultado.

### `JanelaEditar` (`JanelaEditar.xaml[.cs]`)
Criar/editar um documento.
- `static Documento JanelaEditar.Criar(owner)` e `Editar(owner, doc)` → devolvem
  o `Documento` ou `null` (cancelado).
- Ao **editar**, a chave fica travada (renomear trocaria o arquivo; não suportado
  hoje). Ao **criar**, a chave é validada por `DocumentoRepository.SanitizarChave`.

### `JanelaConfiguracoes` (`JanelaConfiguracoes.xaml[.cs]`)
Preferências do usuário, aberta pelo botão **⚙** na `JanelaPrincipal`.
- **Tema**: Escuro/Claro, com pré-visualização ao vivo (revertida se cancelar).
- **Tempo para guardar a senha**: presets (15 min … 1 dia) que definem
  `Configuracoes.DuracaoSessaoMinutos`, usado pelo `Cofre` na expiração da sessão.
- `static void JanelaConfiguracoes.Mostrar(owner)`.

### `Toast` (`Toast.xaml[.cs]`)
Aviso flutuante no canto inferior direito, com fade-in/out e auto-fechamento
(~2,2 s). Usado no **modo CLI** para mostrar o resultado. `static void
Toast.Mostrar(mensagem, sucesso)`.

## Padrões de UI

- **Sem MVVM**: code-behind direto, simples. A `JanelaPrincipal` mantém a lista
  completa e uma `ObservableCollection` filtrada.
- **Diálogos nativos** para confirmação (`MessageBox`); janelas próprias
  (temizadas) para senha e edição.
- **Segredos ocultos por padrão** na tela; só aparecem ao clicar em Mostrar.
- Estilos reutilizáveis em `Tema.xaml`: `BotaoPrimario`, `BotaoPerigo`,
  `ItemDocumento`, além de estilos default para `TextBox`/`PasswordBox`/`Button`.

## Como adicionar uma tela

1. Crie `MinhaJanela.xaml` + `.cs` em `source/Memo` (o SDK inclui `.xaml`
   automaticamente com `UseWPF`).
2. Use `Background="{DynamicResource CorFundo}"` e os estilos do `Tema.xaml`
   (sempre **`DynamicResource`** para cores, senão a tela não troca de tema).
3. Chame `Nativo.AplicarBarraTitulo(this)` no construtor.
