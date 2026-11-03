# PcismoMaster

Aplicativo **Windows** (WPF, .NET 8) para **diagnóstico, monitoramento, benchmark e otimização** de PC. Roda **como administrador** para ler sensores de baixo nível e aplicar ajustes somente quando você pede.

Versão atual: **0.6.3** — novidades em [CHANGELOG.md](CHANGELOG.md). Fluxo de contribuição em [CONTRIBUTING.md](CONTRIBUTING.md) e diretrizes do projeto em [AGENTS.md](AGENTS.md).

> Projeto mantido em dupla. Antes de mexer, sincronize com o `origin` (`git fetch` + `git pull --ff-only`) e trabalhe em branch própria, entregando por pull request.

## Como usar

- **Baixar:** pegue o `PcismoMaster-<versão>.exe` (ou o `.zip`) na página de [Releases](https://github.com/vbryanreis/PcismoMaster/releases). É um único arquivo, sem instalação.
- **Abrir:** dê dois cliques no `.exe`. Ele pede elevação (**Executar como administrador**) — isso é necessário para ler sensores, mexer em drivers/serviços e aplicar ajustes do Windows.
- **Primeira abertura:** a partir da 0.6.3 o `.exe` é comprimido (~83 MB); a primeira abertura pode levar uma fração de segundo a mais para descomprimir em memória. Depois abre normal.
- **Nada é aplicado automaticamente:** otimizações, overclock, limpezas e desinstalações só acontecem com a sua confirmação.

## Navegação

A barra lateral agrupa as páginas (os grupos acendem no nosso azul ao passar o mouse):

- **Visão geral** — painel inicial: estado do hardware, gráfico de CPU/GPU por faixa de cor, cartão *Saúde do PC* e o botão **OTIMIZAR**.
- **MASTER** — Sistema, Sensores (no estilo HWiNFO), Placa de vídeo (com a logo da fabricante em destaque: NVIDIA em ciano, AMD em vermelho), Discos, Energia e Rede.
- **MANUTENÇÃO → Reparos do Windows** — rotinas oficiais do Windows: SFC, DISM, CHKDSK, redefinir rede / Windows Update / spooler, reiniciar áudio, Bluetooth e o Explorer.
- **JOGOS** — Biblioteca de jogos, FPS estimado, detector de Gargalo e Overlay de FPS sobre o jogo.
- **DESEMPENHO** — Benchmark (CPU, GPU, memória, disco e ray tracing DXR 1.1), Stress test (CPU / RAM / GPU) e **Overclock** no estilo do MSI Afterburner.
- **FERRAMENTAS** — Otimização (ajustes do Windows para jogos, guardando o valor original para desfazer), Inicialização (o que abre junto com o Windows), **Instalar apps** (catálogo por categorias no estilo Ninite, via winget), Drivers (NVIDIA / AMD / Windows Update), Integração (RGB: iCUE e OpenRGB) e **Utilidades** — *Baixar vídeos* (YouTube/Instagram em MP4 ou MP3) e o **Finder**, busca instantânea de arquivos e pastas lendo a MFT dos discos NTFS.
- **MAKER** — Portas COM (resolve portas fantasmas/presas) e Drivers seriais (CH340, CP210x, FTDI, Prolific, Arduino + Arduino IDE).
- **OTIMIZAÇÃO** (sempre abaixo de MAKER) — **Inicialização do Windows** (pausar o Windows Update com opção de restaurar os valores anteriores) e **Desinstalar Apps** (desinstalador seguro no estilo do Revo).

## Observações importantes

- **Executa como administrador e mexe no sistema real.** Otimização, Reparos e Overclock alteram o Windows ou a GPU de verdade — sempre com confirmação e, quando possível, com backup para desfazer.
- **Desinstalar Apps é seguro por design:** usa o desinstalador oficial do programa e, na varredura de sobras, só apaga dentro de *Program Files* / *AppData* / *ProgramData* e de ramos `Software\` do registro. **Nunca** toca em pastas do Windows nem em chaves do sistema, e nada é removido sem você marcar e confirmar.
- **Baixar vídeos** precisa do **yt-dlp** e do **ffmpeg** (grátis). Se faltarem, a ferramenta avisa e leva até *Instalar apps* para instalá-los uma vez pelo winget.
- **Finder** depende do modo administrador e de discos **NTFS** (lê a MFT direto).
- **Pausar o Windows Update** pode adiar também correções de segurança, e às vezes o próprio Windows limita a duração da pausa.
- **Overclock** funciona em placas **NVIDIA**: Core/Memory Clock, Power/Temp Limit, curva V/F com undervolt, curva de fans, 5 perfis e monitoramento ao vivo.
- **Onde ficam os dados:** configurações em `%AppData%\PcismoMaster`; backups dos ajustes do Ultimate em `%LOCALAPPDATA%\PcismoMaster\ModuleBackups`; capas de jogos em `%LOCALAPPDATA%\PcismoMaster\covers`.

## Compilar

```powershell
dotnet run --project tests/ModuleTests.csproj -c Release   # testes
dotnet build PcismoMaster.csproj -c Release                # compilar
# .exe único (igual ao Release):
dotnet publish PcismoMaster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

O `.exe` **não** é versionado no Git; ele sai do workflow de Release (ao criar a tag `vX.Y.Z`) ou do publish local.

## Licenças e créditos

Interface e base herdadas do **ResetExplode**; funções do **Ultimate** no grupo OTIMIZAÇÃO. A camada NVAPI de overclock/undervolt foi portada do [simple-nvidia-undervolt](https://github.com/vuplea/simple-nvidia-undervolt). Licenças preservadas em [LICENSE](LICENSE), [licenses/Ultimate-LICENSE.txt](licenses/Ultimate-LICENSE.txt) e [licenses/simple-nvidia-undervolt-LICENSE.txt](licenses/simple-nvidia-undervolt-LICENSE.txt).
