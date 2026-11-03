# Histórico de versões

A numeração segue **SemVer** (`MAJOR.MINOR.PATCH`). Regras e processo em [docs/projeto/ATUALIZACOES.md](docs/projeto/ATUALIZACOES.md).

## 0.7.0

- Nova aba **Promoções** em JOGOS (logo abaixo da Biblioteca), com o preço dos jogos agora nas lojas e o **menor preço da história**, usando a API gratuita do [IsThereAnyDeal](https://isthereanydeal.com/). Os preços saem na moeda do país do Windows (no Brasil, em reais). Sub-abas:
  - **Promoções**: cartões com capa, desconto, preço antigo e atual, loja, prazo e selos de "menor da história" / "menor na loja". Filtros de ordenação (em alta, maior desconto, menor preço, mais recentes, populares, melhor avaliados, acabando logo), desconto mínimo, preço máximo, tipo (jogos/DLCs) e lojas (Steam, Epic, GOG, Nuuvem, Microsoft e outras), além de "só menor preço da história" e CARREGAR MAIS.
  - **Pesquisar**: busca pelo nome e **compara o preço em cada loja**; o botão "Comparar lojas" dos cartões leva direto para cá.
  - **Grátis**: jogos grátis por tempo limitado (Epic, GOG, Steam…) com o prazo e o botão PEGAR GRÁTIS.
  - **Da minha biblioteca**: os jogos instalados no PC que estão em promoção agora (Steam pelo appid, os outros pelo nome).
- Clicar num jogo abre a oferta direto na loja, no navegador. Os dados vêm como a API entrega e o rodapé credita o IsThereAnyDeal (o PcismoMaster não é afiliado a ele).
- **Chave da API**: o `.exe` do Release já vem com a chave do app (vinda do segredo `ITAD_API_KEY` do GitHub, nunca do código). Quem quiser pode colar a própria chave grátis, que fica cifrada pelo Windows. As respostas ficam alguns minutos em cache para respeitar o limite de consultas. Espaços, quebras de linha e aspas que vêm junto ao colar a chave são removidos, e se a chave for recusada o app diz o motivo exato (faltando, inválida ou expirada).
- Testes novos (sem rede e sem chave real) para o cliente da API: filtros, leitura das respostas, moeda, chave só no cabeçalho, limpeza da chave colada, cache e mensagens de erro.
- Versão atualizada para 0.7.0.

## 0.6.4

- Nova categoria **ArtCraft** na aba **Instalar apps**, ao lado das outras (Navegadores, Mensagens, Mídia…), com os apps de criação da [ArtCraft](https://getartcraft.com/apps): **ArtCraft Launcher** (central oficial que instala e atualiza todos de uma vez — recomendado), **PhotoCraft**, **PdfCraft**, **FigCraft** e **Craft Anything**.
- Como esses apps **não estão no winget**, eles são baixados direto do **GitHub oficial** (sempre o instalador `.msi` da última versão), têm a **assinatura digital conferida** pelo Windows antes de rodar e são instalados **em silêncio** — igualzinho ao fluxo MARCAR / INSTALAR SELECIONADOS dos demais apps.
- Os outros apps da ArtCraft ainda estão "in development" (sem instalador publicado); entram aqui assim que tiverem download.
- Versão atualizada para 0.6.4.

## 0.6.3

- **Limpeza da marca "RESET"** herdada do app-base: nomes de recursos deixam de levar o prefixo (`RESET BENCH` → **BENCH**, `RESET MAKER` → **MAKER**, `RESET DRIVER` → **DRIVER**, `RESET FERRAMENTAS` → **FERRAMENTAS**, `RESET JOGOS` → **JOGOS**, `RESET INTEGRAÇÃO` → **INTEGRAÇÃO**, `RESET OC` → **OC**, `RESET OTIMIZA` → **OTIMIZAÇÃO**) em textos, títulos de janelas, mensagens e comentários.
- Botão do Overclock que restaura os padrões virou **REDEFINIR** (antes "RESET").
- Corrigidos textos desatualizados: o tooltip de exportação de drivers agora aponta para `Documentos\PcismoMaster-Drivers` (pasta que o app já usava) e as menções internas à pasta de configurações passam a `%AppData%\PcismoMaster`.
- **Saúde do PC** (Visão geral): o botão **OTIMIZA** agora é **OTIMIZAR**.
- **Placa de vídeo**: o nome da fabricante no canto do cartão de destaque aparece colorido como uma logo, dentro da nossa paleta — **NVIDIA** no ciano (azul mais próximo do verde) e **AMD** no vermelho; outras (Intel) seguem discretas.
- Preservados os créditos ao projeto original (MIT) e o namespace interno do código.
- **App mais leve**: o `.exe` agora usa compressão no single-file — o tamanho cai bastante sem remover nada (todos os módulos continuam presentes, só são descomprimidos em memória ao abrir; a primeira abertura pode levar uma fração de segundo a mais).
- Versão atualizada para 0.6.3.

## 0.6.2

- Nova sub-aba **Desinstalar Apps** em OTIMIZAÇÃO (abaixo de MAKER), um desinstalador no estilo do Revo, porém **seguro**:
  - Lista só **programas de verdade** (componentes do Windows, drivers e atualizações ficam de fora) com nome, fabricante, versão e tamanho, com busca.
  - Desinstala pelo **desinstalador oficial** do programa (o mesmo do Painel de Controle).
  - Depois faz uma **varredura de sobras** (pastas em AppData/ProgramData/Program Files e chaves de registro de fornecedor) ligadas ao app e lista para você **revisar e marcar** o que remover.
  - **Guardas de segurança**: só apaga dentro de Program Files / AppData / ProgramData e de ramos `Software\` do registro; **nunca** toca em pastas do Windows nem em chaves do sistema (Microsoft, Classes, Policies, Windows), e nada é apagado sem você marcar e confirmar.
- Versão atualizada para 0.6.2.

## 0.6.1

- Aba **Instalar apps** bem maior, no estilo do **Ninite**, com os apps organizados em **sub-abas de categoria**: Navegadores, Mensagens, Mídia, Imagem, Documentos, Segurança, Compactação, Utilitários, Desenvolvimento e Jogos (além de "Todos"). Clique numa categoria para filtrar; MARCAR TODOS / DESMARCAR agora valem para a categoria aberta.
- Dezenas de apps novos (Firefox, Brave, Opera, Zoom, Telegram, Spotify, Audacity, HandBrake, iTunes, GIMP, Paint.NET, Krita, Inkscape, IrfanView, ShareX, Greenshot, Blender, LibreOffice, SumatraPDF, Foxit, Acrobat Reader, Malwarebytes, PeaZip, Everything, TeamViewer, KeePassXC, WinDirStat, VS Code, Git, Python, Notepad++, PuTTY, WinSCP, Epic Games, entre outros), todos via winget.
- Nada vem pré-marcado (como no Ninite): você escolhe o que instalar.
- Os VC++ Redistributables **não** entram aqui de propósito (virão por outro caminho).
- Versão atualizada para 0.6.1.

## 0.6.0

- Nova sub-aba **Utilidades** em FERRAMENTAS, com ferramentas do dia a dia em sub-sub-abas:
  - **Baixar vídeos**: cola o link (YouTube, Instagram, etc.), escolhe **MP4 (vídeo)** ou **MP3 (áudio)**, define a pasta e baixa. Usa o **yt-dlp** + **ffmpeg** (grátis), que agora aparecem na aba **Instalar apps** para instalar uma vez pelo winget; enquanto não estiverem instalados, a ferramenta avisa e leva até lá.
  - **Finder**: busca instantânea de arquivos e pastas no estilo do Everything, lendo a **MFT** dos discos NTFS (usa o modo administrador). Digite para filtrar em tempo real, alterne entre Tudo / Arquivos / Pastas e dê um duplo clique para abrir a pasta do item.
- Versão atualizada para 0.6.0.

## 0.5.2

- Visão geral: o valor da **GPU** no gráfico agora usa as mesmas faixas de cor da CPU em **temperatura, carga e consumo** (azul tranquilo → amarelo → laranja → vermelho conforme aumenta), em vez do cinza fixo.
- Versão atualizada para 0.5.2.

## 0.5.1

- Visão geral, **cor da temperatura** de volta ao nosso azul quando está tranquila (abaixo de 60 °C), mantendo a mudança para amarelo (60–79), laranja (80–89) e vermelho (90+).
- O mesmo tratamento por faixa agora vale para **Carga** e **Consumo**: o valor acende em amarelo/laranja/vermelho quando entra em área de atenção (carga a partir de 70%; consumo ao se aproximar do pico do período).
- Rodapé do menu lateral: **CPU, GPU e TESTE** agora aparecem na nossa paleta (roxo → azul do ícone), em vez do cinza.
- No **modo claro**, o hover azul das categorias ficou um pouco mais fechado (menos claro) para ler melhor no fundo branco; no modo escuro continua igual.
- Página **OTIMIZA** passou a se chamar **OTIMIZAÇÃO** (título e botão), alinhada com a categoria da navegação.
- Versão atualizada para 0.5.1.

## 0.5.0

- **JOGOS · Biblioteca com novo visual**: cabeçalho com busca, **Adicionar jogo (.exe)…** e **Procurar de novo**; "Meus jogos" em **cartões com a capa** de cada jogo (a mesma da biblioteca da Steam; jogos sem capa ganham as iniciais no degradê do ícone), nome, loja e os botões **Preparar e jogar** e **Ajustes**.
- **Janela "Ajustes do jogo"** no estilo do exemplo, com interruptores e selos **SEGURO/AVANÇADO**; só grava ao clicar em **Salvar ajustes** (Cancelar descarta):
  - **Novo: Liberar o núcleo 0** (o jogo fica fora do núcleo físico 0, o mais usado pelo Windows e pelos drivers).
  - **Novo: Só núcleos físicos (sem SMT/Hyper-Threading)**, uma thread por núcleo.
  - **Novo: Só núcleos de desempenho (sem E-cores)** em CPUs Intel híbridas (desativado quando a CPU não tem E-cores).
  - Prioridade alta, plano de energia Alto desempenho, placa de vídeo dedicada, desativar otimizações de tela cheia e o executável do jogo com **Trocar .exe**.
  - **Opções avançadas** mantidas: argumentos de inicialização (modo de janela, resolução, Hz, engine, extras), aplicar nas opções da Steam e programas auxiliares.
- Os núcleos escolhidos são aplicados ao processo do jogo em "Preparar e jogar" (afinidade do Windows; nada muda nos arquivos do jogo) e aparecem no status, por exemplo "núcleos: 14 de 16 threads".
- **Exibição em cartões ou em lista** (botões Cartões / Lista; a escolha fica salva). Na lista, cada jogo mostra o banner, nome, loja e pasta, com os mesmos botões.
- Capas encontradas também no formato novo da Steam (subpastas com hash, `library_capsule.jpg` / `library_header.jpg`); se não houver no disco, a arte é baixada uma vez do servidor de imagens da Steam e guardada em `%LocalAppData%PcismoMastervers`.
- Capas carregadas fora da thread da janela e já reduzidas ao tamanho do quadro, para a aba abrir leve.
- Versão atualizada para 0.5.0.

## 0.4.0

- **Nova aba MANUTENÇÃO** (abaixo de MASTER) · **Reparos do Windows**:
  - **Revisão semanal**: verificar o disco (CHKDSK), os arquivos protegidos (SFC) e reparar a imagem do Windows (DISM), com ponto de restauração antes; mostra a data da última revisão.
  - **Ferramentas**: reparo completo (DISM + SFC), SFC, DISM, CHKDSK, limpar cache de DNS, renovar IP, redefinir a rede, redefinir o Windows Update, destravar a impressora, reiniciar áudio, Bluetooth e Windows Explorer, reconstruir o cache de ícones, reiniciar o driver de vídeo (Win+Ctrl+Shift+B), procurar alterações de hardware e reparar Microsoft Store e menu Iniciar.
  - Cada ferramenta pede confirmação, cria ponto de restauração quando mexe no sistema, mostra a saída ao vivo e pode ser cancelada. Uma por vez.
- **Novo ícone do app** (engrenagem com chave no degradê roxo → ciano), no executável, na barra de título e nos relatórios.
- **Cores no estilo do ícone**: fundo escuro levemente arroxeado; destaque roxo (#A647FE) e degradê roxo → ciano (#29B4D9) nos botões principais, chips selecionados, sliders, interruptores e numa barrinha que marca a aba aberta; hover das abas em ciano; anéis de RAM/VRAM/Disco nos tons do ícone. O modo claro ganhou os mesmos tons, em versão clara.
- **Aba Sensores no formato do HWiNFO**: um bloco por dispositivo (CPU, GPU, memória, placa-mãe, discos, rede) na ordem de detecção, com cabeçalho que recolhe e expande; colunas **Sensor · Atual · Mínimo · Máximo · Média**; linhas compactas; botões **Zerar mín./máx./média**, **Recolher tudo** e **Expandir tudo**. A busca procura por sensor, dispositivo ou tipo (ex.: "temperatura").
- Mínimo, máximo e média passam a ser calculados pelo app desde a abertura (ou o último "zerar"), como no HWiNFO.
- Corrigida a aba Sensores sem linhas nem títulos de coluna (bug da 0.3.1): a virtualização com agrupamento deixava todas as colunas com largura mínima quando a aba começava escondida. A tabela agora rola suave, pixel a pixel.
- Versão atualizada para 0.4.0.

## 0.3.1

- **Desempenho e consumo** (o app fica aberto junto com o jogo):
  - A leitura dos sensores (LibreHardwareMonitor) saiu da thread da janela: a tela não engasga mais uma vez por segundo, e a tabela só é avisada dos valores que mudaram (antes eram ~1000 avisos por segundo).
  - Tabela de sensores virtualizada: só as linhas na tela são desenhadas.
  - **Minimizado**, o app lê os sensores a cada 3 s e não desenha nada (a não ser durante um teste ou análise), e o monitoramento do Overclock só segue se a curva de fans estiver ligada.
  - O painel de gargalo e o limitador da GPU só consultam o driver quando suas abas estão abertas; a escuta de FPS (ETW) só liga quando o Gargalo ou o overlay precisam dela, e esquece processos que pararam de desenhar.
  - Registro de eventos do Windows, varredura de portas COM, placa-mãe (WMI) e lista de programas abertos rodam fora da thread da janela; a checagem de saúde da Visão geral fica guardada por 60 s.
  - Overclock: APLICAR, RESET e a leitura da curva rodam em segundo plano (a janela não congela mais até 3 s), e o monitoramento pausa durante os benchmarks de GPU.
  - Vazamentos corrigidos: um token do Windows por segundo (checagem de administrador), handles de processos e uma chave de registro.
  - Controladoras HID (hubs de fan/RGB) não são mais varridas a cada segundo; isso também evita disputa com o iCUE.
- Versão atualizada para 0.3.1.

## 0.3.0

- **Aba Overclock refeita no estilo do MSI Afterburner** (placas NVIDIA):
  - **Sliders nos blocos do Afterburner**: **Clock** (Mem e Core, lado a lado), **Voltage** (Core % e o botão **Curve Editor**) e **Fan** (Fan speed com Auto; Temp Limit e Power Limit). Cada slider mostra o valor numa caixa à esquerda (clicar seleciona o valor para digitar por cima); botão direito no slider zera o offset. Botões **RESET** (padrão de fábrica) e **APLICAR**.
  - **Curva V/F (Ctrl+F do Afterburner)**: gráfico de tensão × frequência com a curva de fábrica (cinza) e a editada (azul). Overclock + undervolt juntos como no Afterburner: Shift + arrastar (ou o slider Core Clock) sobe a curva; clique num ponto e aperte **L** (ou botão direito) para travar o teto ali; arraste o ponto travado ou use ↑/↓ para escolher o clock do teto. Destaque do teto com "mV · MHz", ponto em uso agora e botão **LER DA PLACA**, que traz o ajuste aplicado (inclusive pelo MSI Afterburner).
  - **Editor de curva em janela própria**: Ctrl+F (ou o botão **CURVE EDITOR**) abre o "Editor da Curva Tensão/Frequência" como no Afterburner, sincronizado com o gráfico da aba. Visual igual ao do Afterburner: escala começando onde a curva sai do piso (ex.: 700–1250 mV, 500–3500 MHz), grade de 25 mV × 100 MHz, títulos "Frequência, MHz" e "Tensão, mV", curva de fábrica em linha fina e a editada com quadradinhos; o ponto selecionado mostra "mV · MHz". **Ctrl+D** volta a curva ao padrão. Um clique só seleciona o ponto; ele muda quando é arrastado.
  - Depois de aplicar, o app confirma no driver os pontos gravados e mantém a curva de fábrica na tela.
  - **Undervolt aplicado como no Afterburner**: os pontos depois do teto recebem um único offset bem negativo e o driver os segura no clock do teto, então a curva aplicada fica reta até o fim (sem subir clock nem tensão), mesmo quando o teto está mais de 1000 MHz abaixo da curva de fábrica. Com curva V/F o Core Clock não é mais gravado também na P0 (isso somava o offset duas vezes e levantava a parte plana).
  - **Curva de fans** controlada pelo app: pontos arrastáveis, duplo clique cria ponto, botão direito apaga, histerese ajustável, temperatura e fan atuais no gráfico. Ao fechar o app, os fans voltam para o automático.
  - **Monitoramento** dos últimos 2 minutos: GPU clock, memory clock, temperatura, consumo × limite (curva de potência), fan e voltagem, mais o que está limitando o clock (energia, temperatura etc.).
  - **5 perfis** (SALVAR e escolher 1–5; clicar num perfil carrega e aplica) e opção de reaplicar os últimos ajustes ao abrir o PcismoMaster.
- A camada NVAPI (curva V/F, offsets de clock e memória, tensão) foi portada do projeto **simple-nvidia-undervolt** (licença MIT, créditos em `licenses/`). Energia, temperatura e fans usam a NVML oficial.
- Memory Clock: a tabela da curva V/F também guarda a entrada da memória; o app limpa a curva antes de gravar a memória e grava só os pontos do core depois, para o offset valer de fato. Depois de aplicar, o app confere o clock real e avisa se o driver não ativou.
- O verificador de estabilidade e o guia de overclock na BIOS continuam na aba, abaixo dos novos controles.
- Novos testes automáticos da curva V/F, do undervolt, da curva de fans e dos perfis (sem tocar na GPU).
- Versão atualizada para 0.3.0.

## 0.2.5

- Visão geral com mais cor:
  - **CPU · TEMPERATURA**: chips de seleção (período e métrica) e a legenda da CPU no nosso azul; **curva da CPU em azul** e **curva da GPU em verde**.
  - O **valor da temperatura muda de cor por faixa**: azul abaixo de 60 °C, amarelo de 60 a 79, laranja de 80 a 89 e vermelho a partir de 90 °C.
  - **Memória e disco**: a quantidade de RAM em uso (GB) no nosso azul; os anéis de **RAM, VRAM e Disco** em tonalidades de roxo indo para o nosso azul.
  - As **sub-abas** (itens dentro das categorias) também acendem no nosso azul ao passar o mouse, como as abas principais.
- Versão atualizada para 0.2.5.

## 0.2.4

- Abas de navegação (MASTER, JOGOS, DESEMPENHO, FERRAMENTAS, MAKER, OTIMIZAÇÃO) com fonte nova em **negrito**, clean e legível no estilo de apps de tecnologia (Inter, com queda para Segoe UI Variable Display e Segoe UI).
- Ao passar o mouse, a aba fica em **azul claro iluminado** (com leve brilho).
- Abas padronizadas em CAIXA ALTA; a aba **PcismoMaster** passou a se chamar **MASTER**.
- O rótulo de versão no menu lateral agora é lido do próprio assembly, então nunca mais fica desatualizado.
- Documentação do projeto em `docs/projeto/` (fluxo de colaboração, planejamento e padrão de versionamento SemVer) para Bryan, Pedro, Claude e Codex seguirem o mesmo padrão.
- Versão atualizada para 0.2.4.

## 0.2.3

- Corrigido o erro "Could not find a part of the path ...\ModuleBackups\updates-pause.json" ao clicar em **Restaurar configuração anterior** no Windows Update sem ter pausado antes. Agora aparece um aviso de que não há nada para restaurar, e nenhum valor é alterado.
- O motor de backup recusa restaurar sem backup com uma mensagem clara, coberta por um novo teste automático.
- Versão atualizada para 0.2.3.

## 0.2.2

- Removidas as ferramentas Background Apps e Store Settings da aba OTIMIZAÇÃO, junto com o script `Scripts/StoreSettings.ps1`. Fica somente a pausa do Windows Update.
- "Updates Pause" passa a se chamar **Windows Update**.
- OTIMIZAÇÃO agora é um grupo da navegação com a mesma fonte e o mesmo estilo de JOGOS, DESEMPENHO, FERRAMENTAS e MAKER, e não mais um item avulso em maiúsculas.
- Nova sub-aba **Inicialização do Windows** dentro de OTIMIZAÇÃO, onde fica o Windows Update.
- Layout do Windows Update refeito no padrão das outras páginas: título, subtítulo, cartão, rótulos e botões (principal e contorno) iguais aos de MAKER e FERRAMENTAS.
- Como não há mais scripts externos, o `.exe` avulso da Release volta a ter todas as funções.
- Versão atualizada para 0.2.2.

## 0.2.1

- Executável único: `PcismoMaster.exe` agora é publicado como arquivo único autocontido (não precisa instalar o .NET), com as bibliotecas nativas embutidas.
- Lançamento automático: cada tag `vX.Y.Z` compila, testa e publica uma Release no GitHub com o `.exe`, um `.zip` (exe + Scripts, licenças e documentação) e as notas desta seção do CHANGELOG.
- `publicar.bat` passa a gerar o mesmo executável único da Release.
- Versão atualizada para 0.2.1.

Download recomendado: o `.zip`. O `.exe` avulso funciona, mas Store Settings precisa da pasta `Scripts` que vem no `.zip`.

## 0.2.0

- Sub-abas verticais Updates Pause, Background Apps e Store Settings dentro de OTIMIZAÇÃO.
- Background Apps com backup e restauração individual.
- Store Settings executa o script Ultimate com seleção pela interface; redefinição não equivale a backup. Efeitos na Store ainda requerem validação manual.

## 0.1.0

- Primeira versão PcismoMaster derivada do ResetExplode, com identidade própria e licenças preservadas.
- Abertura direta na interface de hardware e diagnóstico da base.
- Remoção do prefixo RESET nos nomes da navegação.
- Nova aba OTIMIZAÇÃO abaixo de MAKER: pausa do Windows Update por 365 dias, restauração dos valores anteriores e acesso às configurações do Windows Update.
- Motor de backup e recuperação de valores do registro com testes de falhas.
- Testes e compilação automatizados no GitHub Actions.

A pausa depende da versão do Windows e inclui correções de segurança. O efeito após reinicialização e os demais recursos herdados ainda precisam de validação específica.
