# PcismoMaster

Aplicativo **Windows** (WPF, .NET 8) para **diagnóstico, monitoramento, benchmark e otimização** de PC. Roda **como administrador** para ler sensores de baixo nível e aplicar ajustes somente quando você pede.

Versão atual: **0.8.2** — novidades em [CHANGELOG.md](CHANGELOG.md). Fluxo de contribuição em [CONTRIBUTING.md](CONTRIBUTING.md) e diretrizes do projeto em [AGENTS.md](AGENTS.md).

> Projeto mantido em dupla. Antes de mexer, sincronize com o `origin` (`git fetch` + `git pull --ff-only`) e trabalhe em branch própria, entregando por pull request.

## Como usar

- **Baixar:** pegue o `PcismoMaster-<versão>.exe` (ou o `.zip`) na página de [Releases](https://github.com/vbryanreis/PcismoMaster/releases). É um único arquivo, sem instalação.
- **Abrir:** dê dois cliques no `.exe`. Ele pede elevação (**Executar como administrador**) — isso é necessário para ler sensores, mexer em drivers/serviços e aplicar ajustes do Windows.
- **Primeira abertura:** a partir da 0.6.3 o `.exe` é comprimido (~83 MB); a primeira abertura pode levar uma fração de segundo a mais para descomprimir em memória. Depois abre normal.
- **Nada é aplicado automaticamente:** otimizações, overclock, limpezas e desinstalações só acontecem com a sua confirmação.

## Navegação

O app abre no **Início** ("O que você quer fazer?"), com as tarefas mais comuns em blocos e uma busca por tudo que ele faz. No topo fica o botão **Modo Simples / Modo Avançado**, destacado em cor para ser fácil de achar (azul no simples, roxo no avançado): o simples (padrão) mostra só o essencial e seguro; o avançado libera sensores, placa de vídeo, overclock, stress test, ajustes técnicos e os scripts completos. Cada página tem uma explicação curta no topo.

A barra lateral agrupa as páginas (os grupos acendem no nosso azul ao passar o mouse):

- **Visão geral** — painel inicial: estado do hardware, gráfico de CPU/GPU por faixa de cor, cartão *Saúde do PC* e o botão **OTIMIZAR**.
- **MASTER** — Sistema, Sensores (no estilo HWiNFO), Placa de vídeo (com a logo da fabricante em destaque: NVIDIA em ciano, AMD em vermelho), Discos, Energia e Rede.
- **MANUTENÇÃO → Reparos do Windows** — rotinas oficiais do Windows: SFC, DISM, CHKDSK, redefinir rede / Windows Update / spooler, reiniciar áudio, Bluetooth e o Explorer.
- **TWEAKING** — sete páginas, cada ajuste com desfazer exato e ponto de restauração antes da primeira mudança:
  - **Formatação**: depois de instalar o Windows, aplica o pacote completo de uma vez (mais Visualizador de Fotos clássico, remover OneDrive, valer para contas novas e limpeza), e pergunta se reinicia.
  - **Debloat** em três níveis (Essencial e Recomendado já marcados; Avançado escondido e com aviso em cada item): telemetria, tarefas e serviços de coleta, Copilot, Recall, Clique para Fazer, apps de IA, propaganda da Microsoft, recursos opcionais (escrita à mão, Hello facial, vozes…), hibernação, armazenamento reservado, OneDrive e Área de Trabalho Remota. Embaixo, o **bloatware escolhido app por app**.
  - **Windows**: mais de cem ajustes com interruptor, pesquisa e filtro por categoria (Geral, Aparência, Desempenho, Privacidade, Jogos, Rede, IA): compressão de memória, prioridade da janela ativa, núcleos acordados, Explorer, menu de contexto clássico, barra de tarefas, IPv4/IPv6, Edge e Brave sem propaganda, telemetria do Office e da NVIDIA, desligamento mais rápido, .NET 3.5 e outros.
  - **Mouse e teclado**: mouse sem atrasos, teclas de aderência/filtro/alternância sem atalho, Num Lock ligado, sem Alt+Shift trocando idioma e sem atalho do Narrador.
  - **GPU**: monitor na taxa máxima, agendamento de GPU por hardware e PCI Express sem economia de energia.
  - **Perfis**: Básico, Sem IA, Padrão do Windows (desfaz tudo) e perfis próprios para salvar, exportar e importar.
  - **Limpeza**: mede e limpa temporários, Lixeira, cache do Windows Update e da Otimização de Entrega, relatórios de erro, despejos de memória, cache de shaders, cache dos navegadores, miniaturas e Prefetch.
- **JOGOS** — Biblioteca de jogos, **Promoções** (preço dos jogos agora nas lojas, com o menor preço da história, filtros — inclusive de onde o jogo roda: Steam, Epic, Xbox, EA, Ubisoft, Battle.net ou sem DRM —, **favoritos com coração** para acompanhar o preço, pesquisa que compara as lojas, jogos grátis e os seus jogos em promoção), FPS estimado, detector de Gargalo e **Overlay** sobre o jogo no estilo do RivaTuner, sem injetar nada (vertical em tabela ou barra numa linha, prévia ao vivo, posição, opacidade, tamanho, cores e fonte; FPS, frametime, 1% e 0,1% low, mín/méd/máx, variação e gráficos; **limitador de FPS** pelo driver NVIDIA, global e por jogo; **benchmark** com CSV por frame; **captura de tela** e **gravação de vídeo**; atalhos à escolha e **perfis por jogo**).
- **DESEMPENHO** — Benchmark (CPU, GPU, memória, disco e ray tracing DXR 1.1), Stress test (CPU / RAM / GPU) e **Overclock** no estilo do MSI Afterburner.
- **FERRAMENTAS** — Otimização (ajustes do Windows para jogos, guardando o valor original para desfazer), Inicialização (o que abre junto com o Windows), **Instalar apps** (cartões com o ícone de cada app, por categorias, via winget) e **Desinstalar Apps** (estilo Revo/Geek Uninstaller, com varredura profunda de sobras, Lixeira e backup), **Drivers** (limpeza com o DDU, instalação do driver normal ou enxuta com as configurações ajustadas à placa, Painel NVIDIA, Profile Inspector e verificação do Visual C++/DirectX), Integração (RGB: iCUE e OpenRGB) e **Utilidades** — *Baixar vídeos* (YouTube/Instagram em MP4 ou MP3) e o **Finder**, busca instantânea de arquivos e pastas lendo a MFT dos discos NTFS.
- **MAKER** — Portas COM (resolve portas fantasmas/presas) e Drivers seriais (CH340, CP210x, FTDI, Prolific, Arduino + Arduino IDE).

## Observações importantes

- **Executa como administrador e mexe no sistema real.** Otimização, Reparos e Overclock alteram o Windows ou a GPU de verdade — sempre com confirmação e, quando possível, com backup para desfazer.
- **Desinstalar Apps é seguro por design:** usa o desinstalador oficial do programa e, na varredura de sobras, só apaga dentro de *Program Files* / *AppData* / *ProgramData* e de ramos `Software\` do registro. **Nunca** toca em pastas do Windows nem em chaves do sistema, e nada é removido sem você marcar e confirmar.
- **Baixar vídeos** precisa do **yt-dlp** e do **ffmpeg** (grátis). Se faltarem, a ferramenta avisa e leva até *Instalar apps* para instalá-los uma vez pelo winget.
- **Finder** depende do modo administrador e de discos **NTFS** (lê a MFT direto).
- **Promoções** usa a API gratuita do [IsThereAnyDeal](https://isthereanydeal.com/) e precisa de internet. O `.exe` do Release já vem com a chave do app; se ela faltar (ou para usar a sua), clique em **Chave da API** na aba e cole uma chave grátis criada em isthereanydeal.com/apps (fica cifrada pelo Windows). Os preços saem na moeda do país configurado no Windows. O PcismoMaster não é afiliado ao IsThereAnyDeal.
- **Overclock** funciona em placas **NVIDIA**: Core/Memory Clock, Power/Temp Limit, curva V/F com undervolt, curva de fans, 5 perfis e monitoramento ao vivo.
- **Onde ficam os dados:** configurações em `%AppData%\PcismoMaster`; backups dos ajustes do Ultimate em `%LOCALAPPDATA%\PcismoMaster\ModuleBackups`; capas de jogos em `%LOCALAPPDATA%\PcismoMaster\covers`; registro da limpeza com o DDU em `%LOCALAPPDATA%\PcismoMaster\ddu-log.txt`.

## Compilar

```powershell
dotnet run --project tests/ModuleTests.csproj -c Release   # testes
dotnet build PcismoMaster.csproj -c Release                # compilar
# .exe único (igual ao Release):
dotnet publish PcismoMaster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

O `.exe` **não** é versionado no Git; ele sai do workflow de Release (ao criar a tag `vX.Y.Z`) ou do publish local.

A chave do app no IsThereAnyDeal fica no segredo **`ITAD_API_KEY`** do repositório (Settings → Secrets and variables → Actions) e o workflow de Release a injeta com `-p:ItadApiKey=…`. **Nunca** coloque a chave no código. No publish local ela é opcional: sem ela, cole a sua chave na própria aba Promoções.

## Licenças e créditos

Interface e base herdadas do **ResetExplode**; funções do **Ultimate** em FERRAMENTAS. O Profile Inspector e o Painel NVIDIA usam o catálogo do [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector) (licença em `licenses/nvidiaProfileInspector-LICENSE.txt`); a limpeza do driver usa o DDU da Wagnardsoft (baixado na hora, com a assinatura conferida). A camada NVAPI de overclock/undervolt foi portada do [simple-nvidia-undervolt](https://github.com/vuplea/simple-nvidia-undervolt). A fonte **Orbitron** (botão de modo) é de Matt McInerney / The League of Moveable Type, sob a **SIL Open Font License 1.1** (`licenses/Orbitron-OFL.txt`). Licenças preservadas em [LICENSE](LICENSE), [licenses/Ultimate-LICENSE.txt](licenses/Ultimate-LICENSE.txt) e [licenses/simple-nvidia-undervolt-LICENSE.txt](licenses/simple-nvidia-undervolt-LICENSE.txt).
