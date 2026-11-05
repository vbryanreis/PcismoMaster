# Histórico de versões

A numeração segue **SemVer** (`MAJOR.MINOR.PATCH`). Regras e processo em [docs/projeto/ATUALIZACOES.md](docs/projeto/ATUALIZACOES.md).

## 0.10.2

- **Overlay · cores de FPS, FRAME e 1% LOW separadas**: na 0.10.1 os três ficaram juntos numa sub-opção só. Agora a linha **FPS** (em Valores) tem a cor do FPS, como antes, e o **triângulo ▸** dela abre **FRAME** e **1% LOW**, cada um com a sua cor. Enquanto não ganham cor própria, os dois seguem a do FPS. O gráfico de frametime usa a cor do FRAME, e o de FPS usa a do FPS.
- A cor do grupo **VALORES** (fechado) agora pinta também o FPS, além de GPU, CPU e RAM. O nome do jogo e a hora seguem a cor do grupo Valores/Rótulos.
- Versão atualizada para 0.10.2.

## 0.10.1

- **Cores do Overlay por grupo** (JOGOS · Overlay · Aparência): a seção de cores agora tem dois grupos, **Valores** e **Rótulos**. Fechados (o padrão), cada um tem uma cor só, que vale para tudo; o **triângulo ▸** ao lado abre uma cor para **FPS, GPU, CPU e RAM** — dá para pintar, por exemplo, tudo da GPU de verde e da CPU de azul. Dentro de **FPS** (Valores), outro triângulo abre a sub-opção **FPS, FRAME e 1% LOW**, que define a cor desses três juntos. Com o grupo fechado e cores próprias escolhidas, aparece o aviso "cores próprias por grupo"; escolher a cor do grupo volta todos para ela. A VRAM acompanha a cor da GPU e o nome do jogo/hora acompanha a de FPS. A cor automática por temperatura (verde/âmbar/vermelho) continua valendo por cima dos valores. Quem já tinha cores salvas as mantém; "Voltar ao padrão" zera tudo.
- Versão atualizada para 0.10.1.

## 0.10.0

- **Interface mais limpa e legível**:
  - Textos de apoio mais claros (contraste de leitura) e as letras de 11 px passaram para 12 px.
  - Títulos das páginas todos no mesmo estilo (sem CAIXA ALTA); Biblioteca e FPS estimado não se chamam mais os dois "Jogos"; "Desinstalar apps" com a mesma grafia de "Instalar apps"; siglas como IA, CSV e PNG ficam em maiúsculas.
  - No modo avançado sai a faixa de explicação do topo, que repetia o subtítulo da página (no modo simples continua).
  - A caixa de andamento dos ajustes só aparece quando algo roda, em vez de ocupar espaço vazio no meio da página.
  - Overlay dividido em sub-abas: Overlay, Limitador de FPS, Benchmark, Captura e vídeo e Perfis por jogo.
  - Sistema, Discos, Energia, Rede e Utilidades com largura máxima de 1400 px; a lista do Desinstalar apps cresce com a janela.
  - Abrir a categoria Manutenção já abre a página (ela só tem uma); títulos sem o ícone solto que só duas páginas tinham.
- **Limpeza com a memória RAM em gráfico**: a limpeza de memória e o fechar programas saíram de Ferramentas › Otimização e foram para TWEAKING › Limpeza, com uma rosca da RAM (em uso, em espera, modificada e livre), o uso dos últimos 60 segundos e o resultado de cada limpeza.
- **Aba Otimização removida**: os ajustes para jogos dela já existem nas outras abas; o Game DVR, que só existia lá, entrou em TWEAKING › Windows. Quem alterou algo pela aba antiga ainda pode restaurar em TWEAKING › Windows.
- **Desinstalar apps**: programas já removidos que deixaram a entrada no registro (desinstalador e pasta sumiram, ou MSI que o Windows não conhece mais) não aparecem mais na lista; um botão apaga essas entradas órfãs com backup. Depois do desinstalador oficial, a entrada só é oferecida como sobra se o programa sumiu de verdade (se o desinstalador foi cancelado, o app continua instalado). Programas com o desinstalador sumido mas a pasta ainda lá mostram "use a remoção forçada".
- **Drivers**:
  - No topo, a versão do driver de vídeo instalada x a mais recente e **o que mudou** em cada versão nova, das notas oficiais da NVIDIA: jogos com suporte Game Ready, tecnologias, correções em jogos e gerais; versões anteriores sob demanda e link para as notas completas.
  - **Bibliotecas**: botões para baixar e instalar o Visual C++ (pacote AIO do abbodi1406/vcredist, conferido pelo SHA-256 do GitHub) e o DirectX de junho de 2010 (site da Microsoft, assinatura conferida).
  - O cartão "Placa de vídeo" de baixo saiu (repetia o de cima); o Driver Studio e os botões NVIDIA/AMD/Intel foram para o topo.
  - **Placa-mãe**: BIOS instalada x mais recente e todos os drivers do modelo (chipset, áudio, rede, Wi-Fi, Bluetooth, SATA/RAID, USB, utilitários) comparados ao instalado, com download do site oficial — automático na ASUS. Gigabyte, MSI e ASRock bloqueiam programas no site: o app mostra os drivers da placa instalados e abre direto as páginas de BIOS e drivers do modelo. A BIOS nunca é gravada pelo app.
- **Drivers foi para TWEAKING e ganhou visual novo**: no topo, um resumo com o logo de cada fabricante (placa de vídeo, placa-mãe/BIOS e chipset) e se cada um está em dia; cartões com logos (NVIDIA, AMD, Intel, ASUS, Gigabyte, MSI, ASRock, Microsoft), Windows Update e backup lado a lado e a lista completa de drivers recolhida.
  - **Ajustes novos da placa NVIDIA** (todos com desfazer): desligar os logs do driver, o write combining, a preempção, a economia de energia escondida e o clock gating, e desligar serviços da NVIDIA que não servem para jogar (telemetria, FrameView, contêineres do NVIDIA App/Broadcast; o do Painel de Controle fica). O "P0" que já existia agora se chama "Fixar a placa no estado P0".
  - **Painel NVIDIA e Profile Inspector saíram de dentro do app**: toda instalação do driver NVIDIA por esta página instala o Painel de Controle NVIDIA pela Microsoft Store (se faltar) e baixa o NVIDIA Profile Inspector mais novo do GitHub oficial (SHA-256 conferido), com atalhos na Área de Trabalho. Também dá para fazer isso com um botão.
  - **Chipset AMD comparado com o site da AMD**: a versão do AMD Chipset Software instalada x a mais nova publicada na página oficial do chipset da placa, com link direto.
- **Cabeçalho com ícone em todas as páginas**: os cartões principais (TWEAKING, Integração, Limpeza, Placa de vídeo, Gargalo, Overlay, Benchmark, Inicialização, Utilidades, Desinstalar apps, Maker, Overclock e Stress test) seguem o padrão da aba Drivers: ícone, título sem caixa alta e uma linha explicando o cartão. Na aba Drivers, a limpeza com o DDU passou a ser o primeiro cartão.
- **Desempenho**: Inicialização, Portas COM e Biblioteca de jogos não travam mais a tela ao abrir (leituras em segundo plano); código sem uso removido (janela antiga do ResetExplode, o Profile Inspector embutido e as funções NVAPI/NVML e auxiliares que ninguém mais chamava).

## 0.9.0

- **TWEAKING refeito do zero** no estilo da aba Otimizar do Galdino Control (escrito do zero: as funções vêm das configurações documentadas do Windows, sem copiar código). As sub-abas antigas (Iniciar, Windows, Hardware, Avançado) e os scripts completos do Ultimate saíram; ficou o bloatware app por app, agora dentro de Debloat.
  - Páginas novas: **Formatação**, **Debloat** (Essencial, Recomendado e Avançado), **Windows**, **Mouse e teclado**, **GPU**, **Perfis** e **Limpeza**.
  - Cerca de 150 ajustes. Registro pelo mesmo motor de antes (guarda o valor exato e desfaz); o que não é registro (tarefas agendadas, recursos opcionais, plano de energia, hibernação, compressão de memória, bcdedit, taxa do monitor, Discord, chaves apagadas com cópia .reg) guarda o estado lido antes de mudar e desfaz com ele. Apps removidos voltam pela Microsoft Store.
  - O estado de cada ajuste é lido em segundo plano, num PowerShell só para tudo que não é registro. Serviços que não existem no PC aparecem como "não se aplica" em vez de criar chave falsa.
  - Não repete o que já existe em outras abas (Game DVR, aceleração do mouse e plano de energia em Ferramentas › Otimização; limpar RAM e DNS; Inicialização; Instalar apps) e não desliga proteções do Windows (Defender, UAC, Isolamento de Núcleo, firewall).
  - Ponto de restauração uma vez por sessão, antes da primeira mudança. O Explorer reinicia sozinho quando algum ajuste precisa.
- **Overlay com as funções do RivaTuner** (JOGOS · Overlay), sem injetar nada no jogo (seguro com anti-cheat):
  - **Limitador de FPS** pelo driver NVIDIA: global, em segundo plano e por jogo. Fica gravado no driver; "Desligado" desfaz.
  - Itens novos: **0,1% low**, **FPS mínimo, médio e máximo** desde que o jogo abriu, **variação do frametime** e **gráfico de FPS**.
  - **Benchmark** por botão ou atalho, com duração fixa ou até parar: FPS médio, mínimo e máximo de 1 s, 1% e 0,1% low, e um **CSV por frame** (formato do PresentMon/CapFrameX) em DocumentosPcismoMasterBenchmarks. Os últimos resultados ficam na aba.
  - **Captura de tela** (PNG, em ImagensPcismoMaster) e **gravação de vídeo** (MP4, em VídeosPcismoMaster) do monitor do jogo, pelo ffmpeg com NVENC na NVIDIA. O overlay some das capturas e transmissões, a não ser que você marque para aparecer.
  - **Mais atalhos**: benchmark, ligar/desligar o limitador, captura e gravação (sem atalho até você escolher, para não roubar teclas de outros programas).
  - **Aparência**: cor do FPS, dos valores e dos rótulos, fonte e contorno no texto.
  - **Perfis por jogo**: itens, posição e formato salvos para cada executável, aplicados quando o jogo fica na tela; cada perfil pode ligar o overlay sozinho e ter o próprio limite de FPS.
  - Fora do escopo (precisariam injetar código no jogo): scanline sync, limitador Reflex, limitador em AMD/Intel e overlay sobre tela cheia exclusiva antiga.
  - A aba Overlay aberta não procura mais o ffmpeg nem consulta o driver NVIDIA a cada meio segundo (em PC sem NVIDIA, cada consulta falhava com erro).

## 0.8.2

- **Favoritos nas Promoções** (JOGOS · Promoções): todo cartão de jogo e todo resultado da Pesquisa ganhou um **coração**. Tocou, o jogo vai para a nova sub-aba **Favoritos**, que mostra o preço mais barato de agora em cada um, com o menor preço da história; os que estão em promoção vêm primeiro, pelo maior desconto. A sub-aba avisa quantos favoritos estão em promoção ("Favoritos · 3 em promoção") assim que a aba abre. Para acompanhar um jogo que ainda não está em promoção, é só procurar por ele em Pesquisar e tocar no coração. Os favoritos ficam salvos no próprio app (não precisa de conta no IsThereAnyDeal).
- **Filtro "Onde o jogo roda" (DRM)**: caixinhas para Steam, Epic Games, Xbox / Microsoft Store, EA app, Ubisoft Connect, Battle.net e Sem DRM (GOG e outros), em qualquer combinação. O jogo só aparece se rodar apenas nos DRMs marcados — um jogo da Steam que pede Ubisoft Connect só aparece com os dois marcados. Nada marcado = qualquer. A escolha fica salva e vale também para o preço dos Favoritos.
- Os cartões e a comparação de lojas mostram **onde o jogo ativa** quando não é na própria loja (ex.: "FANATICAL · ativa na Steam", "GOG · sem DRM").
- **Logos da ArtCraft em Instalar apps**: os cinco apps da ArtCraft (Launcher, PhotoCraft, PdfCraft, FigCraft e Craft Anything) agora mostram o ícone oficial de cada um, baixado do repositório do próprio app no GitHub (antes ficavam só com a inicial).
- Versão atualizada para 0.8.2.

## 0.8.1

- **Botão de modo mais fácil de achar** (barra de título): agora com borda e realce coloridos — **azul no Modo Simples** e **roxo no Modo Avançado** — e o destaque ao passar o mouse na mesma cor. O azul usa o mesmo tom do indicador de temperatura (mais azul e menos esverdeado que o ciano). O texto ganhou a fonte **Orbitron** (futurista, embutida no app, licença SIL OFL) e a inicial maiúscula na segunda palavra: **Modo Simples** / **Modo Avançado**. A cor acompanha o tema claro/escuro.
- Versão atualizada para 0.8.1.

## 0.8.0

Junta a 0.6.3, a 0.6.4 (ArtCraft) e a 0.7.0 (Promoções) do Bryan com as novidades abaixo.

- **Overlay completo** (JOGOS · Overlay):
  - **Prévia ao vivo** na própria aba, com os números reais deste PC, no formato, na posição e na opacidade escolhidos.
  - **Formato horizontal (barra)**: tudo numa linha só (FPS, frame, 1% low, uso/temperatura/clock de CPU e GPU, VRAM, RAM, jogo).
  - **Formato vertical de cara nova**: tabela com cantos arredondados, rótulo à esquerda e valor em negrito à direita, um item por linha (FPS em roxo, temperaturas coloridas) e o gráfico de frametime embaixo, se escolhido.
  - **Centro superior e centro inferior**, além dos quatro cantos.
  - **Opacidade do fundo** e **tamanho** em controles deslizantes (o tamanho agora vai de 70% a 200%).
  - **Tecla de liga/desliga à sua escolha**: clique na caixa e aperte a tecla (F5, por exemplo) ou uma combinação; se outro programa já usa, avisa e mantém a anterior. O padrão continua Ctrl + Shift + O.
  - **Temperaturas coloridas**: verde até 74 °C, âmbar de 75 a 84 °C e vermelho de 85 °C em diante.
  - A leitura do overlay só roda com ele ligado ou com a aba aberta (para a prévia); fora disso, não gasta nada.

- **Interface repaginada, mais fácil de usar**:
  - **Início**: ao abrir, a tela "O que você quer fazer?" mostra as tarefas mais comuns em blocos grandes (ver como o PC está, deixar mais rápido, remover apps do Windows, consertar e limpar, desinstalar, instalar, driver de vídeo, jogos, inicialização, promoções) e uma **busca por tudo que o app faz**, sem diferenciar acentos (Enter abre o primeiro resultado).
  - **Modo simples e modo avançado** (botão na barra de título, lembrado entre aberturas). O app abre no **simples**, que mostra só o essencial e seguro; o avançado libera sensores detalhados, placa de vídeo, energia, rede, as sub-abas Hardware e Avançado do TWEAKING, benchmark, stress test, overclock, gargalo, integrações, MAKER, o Profile Inspector, os scripts completos e os itens PERIGOSOS do bloatware.
  - **Explicação curta no topo de cada página** (dá para esconder no X e mostrar de novo pelo Início) e a mesma explicação ao parar o mouse em cada item do menu.
  - **Botões e chips em texto normal** ("Remover os marcados" em vez de "REMOVER OS MARCADOS"), mantendo siglas como PC, GPU, NVIDIA e DDU; letras um pouco maiores nos títulos de seção, no menu e nas descrições. Cores, temas claro/escuro e cantos arredondados continuam os mesmos.
- **Mais leve com o app aberto**: fora das páginas Sensores, Sistema, Placa de vídeo, Discos, Energia e Rede, os sensores dos discos (SMART) e da rede são lidos a cada 10 s em vez de todo segundo (o painel e a barra lateral só usam CPU, GPU e memória, que continuam a cada segundo). O Overclock não consulta mais o driver da placa a cada segundo com a aba fechada (só se a curva de fan estiver ligada). A tela Início é montada uma vez só e a lista de bloatware só lê o PC quando você pede.
- **TWEAKING · Windows · Bloatware app por app**: lista o que o script Bloatware removeria (apps, bibliotecas, recursos opcionais, recursos do Windows e programas como OneDrive, GameInput e Área de Trabalho Remota) com **nome em português**, o que é e selo **SEGURO / ATENÇÃO / PERIGOSO**. Marque só o que quer tirar (com busca e "marcar os seguros"); os itens que o script mantém (codecs, Fotos, Paint, Bloco de Notas, Segurança do Windows, Iniciar, barra de tarefas, Configurações…) ficam travados com o motivo. Remover pede confirmação com os avisos e cria ponto de restauração; o que saiu vai para **Removidos por aqui**, com o caminho de volta de cada um (Store para apps, religar o recurso, ou o script de reinstalar o OneDrive, a Área de Trabalho Remota e a Ferramenta de Captura). Programas de terceiros empacotados (que o script do autor também removeria) aparecem como ATENÇÃO.


- **Navegação**: o grupo OTIMIZAÇÃO foi para dentro de **FERRAMENTAS** (Desinstalar Apps logo abaixo de Instalar apps) e a página **Inicialização do Windows** (pausa do Windows Update por 365 dias) saiu do app. Quem pausou por ela pode voltar ao normal em Configurações → Windows Update → Retomar atualizações.
- Novo grupo **TWEAKING** logo abaixo de MANUTENÇÃO, com quatro sub-abas, nesta ordem: **Iniciar**, **Windows**, **Hardware** e **Avançado** (as pastas "3 Setup", "Windows", "Hardware" e "Advanced" do Ultimate). Nada é aplicado ao abrir; os ajustes de registro guardam o valor anterior e desmarcar + APLICAR devolve; um mesmo valor nunca fica em dois ajustes.
- **TWEAKING · Iniciar** (o "3 Setup" do Ultimate):
  - **Ajustes com desfazer** (interruptores com selo SEGURO/AVANÇADO, backup de cada valor): apps em segundo plano desligados, bloquear drivers pelo Windows Update, Edge sem segundo plano e sem Startup Boost, Edge com o uBlock Origin; AVANÇADOS (desligados por padrão): bloquear o Windows Update (pede confirmação), Edge sem aceleração de hardware, Edge sem serviços de atualização (desativados, não apagados) e Store sem atualização automática de apps.
  - **Compressão de memória**: mostra o estado e liga/desliga (vale depois de reiniciar).
  - **BitLocker**: estado de cada disco e desligar (descriptografa, com aviso); para ligar, abre as configurações do BitLocker.
  - **Atalhos**: data e hora, idioma, apps de inicialização, Gerenciador de Tarefas na inicialização, apps em segundo plano e configurações da Store.
  - Os scripts completos do 3 Setup (Edge com os serviços apagados, Store pelo settings.dat, setupcomplete.cmd no pendrive) ficam no fim da página, com aviso.
- **TWEAKING · Windows**:
  - **Ponto de restauração** num clique.
  - **Ajustes com desfazer**: as configurações do Painel de Controle/Configurações do script (cerca de 470 valores de privacidade, notificações, anúncios, Explorador, mouse sem aceleração, som, telemetria; chaves protegidas pelo Windows e a escala da tela ficam de fora), menu de contexto limpo e clássico (as chaves apagadas são exportadas antes e voltam no desfazer), Widgets e Copilot desligados, sem hibernação/inicialização rápida/limitação de energia, timer global, barra de tarefas e Iniciar limpos, Game Bar desligada, placas de rede e dispositivos sem economia de energia e sem acordar o PC (montado com o que existe no PC), tema preto, tela de bloqueio e papel de parede pretos, Menu Iniciar novo (25H2); AVANÇADOS: sem esvaziar o cache de gravação dos discos e UAC desligado.
  - **Plano de energia Ultimate**: cópia do Desempenho Máximo com os valores do script, ativada; desfazer volta o plano anterior.
  - **Rede só com IPv4** (com RELIGAR TUDO), **Limpeza** de temporários e do cache do Windows Update, e atalhos (Modo de Jogo, precisão do ponteiro, vídeo, som, isolamento de núcleo, recursos do Windows…).
- **TWEAKING · Hardware**: mouse sem o limite de 125 Hz em segundo plano; **escala maior sem aceleração** (100% a 350%, com a curva MarkC certa: no script as curvas de 200% e 350% vinham com erro de digitação) com desfazer; testes (UFO, bufferbloat, polling de controle), guia de peças e dicas.
- **TWEAKING · Avançado** (tudo desligado por padrão, com aviso ao aplicar): driver NVMe nativo mais rápido, sem MPO, tela cheia exclusiva, composed flip, pesquisa do Windows desligada, atalhos do teclado desligados, proteções Spectre/Meltdown, firewall, aviso de downloads, drivers sem WHQL, AMD ULPS; **Resizable BAR forçado** no perfil NVIDIA; **recursos de memória (MMAgent)** e **DEP** com volta ao padrão; **por jogo (teste)**: prioridade, sem SMT/HT e sem o núcleo 0 no processo escolhido, só enquanto ele está aberto.
- **TWEAKING · Scripts completos do Ultimate** (no fim de cada sub-aba): todos os scripts das pastas 3 Setup, Windows, Hardware e Advanced, cada opção do menu original num botão (137 ao todo), rodando o texto do próprio script sem o menu e as pausas. Entram também os que antes ficavam de fora: bloatware, Edge e WebView, Defender otimizado e Defender desligado (pelo Modo de Segurança), serviços em massa, Iniciar/Pesquisa/Shell, Autoruns, limpeza completa, fotos da conta, atalhos do Iniciar, Bloco de Notas, serviço de timer, EQ de volume, hidusbf, Mouse Movement Recorder, Polling, Home para Pro e o setupcomplete.cmd no pendrive.
  - **Segurança**: selo SEGURO / ATENÇÃO / PERIGOSO em cada script e botão; antes de rodar, uma janela explica o que muda e avisa se o PC reinicia, se passa pelo Modo de Segurança, se baixa programas de terceiros, se não tem desfazer e qual botão volta ao padrão. Os PERIGOSOS só rodam marcando "Li os avisos e assumo o risco", e o ponto de restauração vem marcado. Dá para ver o script antes (VER O SCRIPT), a saída aparece ao vivo e tem PARAR. Um script por vez; os cartões só são montados ao clicar em MOSTRAR.
  - Ficam de fora só o "4 Keys" (abre um ativador não oficial do Windows), o "12 Updates Pause" (a pausa de 365 dias saiu do app a pedido) e o modo interativo de Prioridade/SMT/Núcleo 1, que agora é o **ABRIR UM JOGO ASSIM…** do cartão Por jogo (abre o .exe com a prioridade e os núcleos escolhidos, fechando os launchers se pedir).
- **Instalar apps de cara nova**: cartões do mesmo tamanho em grade, agrupados por categoria e com o **ícone de cada app** num quadrado claro (do site do app, guardado no PC depois de baixar uma vez; plano B: o ícone do app instalado; sem ícone, a inicial). Clique no cartão para marcar, círculo de seleção e selo INSTALADO, busca, **Esconder instalados**, contador de selecionados e progresso no cartão.
- **Desinstalar Apps · desinstalação profunda (estilo Revo/Geek Uninstaller)**:
  - **Varredura Segura, Moderada ou Avançada** depois do desinstalador oficial. Procura pastas e **subpastas** (Fabricante\App) em Program Files, Common Files, AppData (Roaming, Local, LocalLow, Programs), ProgramData, Menu Iniciar, Documentos, perfil do usuário (pastas `.app`) e Temp, **contando arquivos ocultos e de sistema**; arquivos soltos com o nome do app; atalhos (inclusive quebrados) na Área de Trabalho, no Menu Iniciar e na Inicialização; registro em Software\App e Software\Fabricante\App (usuário, 64 e 32 bits); a entrada que sobra em Programas e Recursos; App Paths; entradas de inicialização (Run/RunOnce); e tarefas agendadas que rodam o app.
  - Cada sobra mostra o porquê (pasta de instalação, nome do app, fabricante), quantos arquivos/ocultos/subpastas ou valores tem, e o botão **ABRIR** (Explorador, Editor do Registro na chave ou Agendador de Tarefas). Só o que tem ligação clara vem marcado; o resto vem com **CONFIRA**.
  - **Remoção com volta**: pastas e arquivos vão para a **Lixeira** (somente leitura e ocultos não travam mais a remoção); registro, inicialização e tarefas ganham **backup** e o botão **RESTAURAR ÚLTIMA LIMPEZA** devolve.
  - **Remoção forçada** para programas cujo desinstalador sumiu ou dá erro, **ponto de restauração** antes de desinstalar (opcional, ligado por padrão), espera os desinstaladores que se copiam para o Temp terminarem (ou JÁ TERMINEI) e acha a pasta de instalação pelo desinstalador quando o registro não diz.
  - Lista de programas com o **ícone** de cada um e a data de instalação.
  - Guardas reforçadas: nunca o Windows, a raiz dos locais, pastas pessoais (Documentos, Downloads, Desktop…), `.ssh`, Microsoft/Common Files/Packages, nem chaves do sistema; exceções só em formatos exatos (a entrada de desinstalação, App Paths de um .exe e valores das chaves Run). Testes novos com pastas temporárias, sem tocar no PC.
- **Utilidades · Finder mais leve**: o índice da MFT guarda os nomes num buffer único e liga cada item ao pai por posição (busca binária no número do registro), sem milhões de strings soltas nem dicionário. Num teste com 3,5 milhões de itens, a memória do índice caiu de ~480 MB para ~130 MB (processo: ~530 MB → ~190 MB) e a coleta completa do GC de ~40 ms para menos de 1 ms, com a mesma velocidade de busca.
- O índice sai da memória **5 minutos depois de sair do Finder** (outra sub-aba ou outra página) e é refeito ao voltar.

- **Drivers · nova sub-aba Painel NVIDIA**: o "Gerenciar as configurações em 3D" do Painel de Controle NVIDIA dentro do app, com os mesmos nomes e opções (em português), gravando direto nos perfis do driver.
  - **Configurações globais** e **de programa** (lista com busca); cada linha mostra de onde vem o valor (você, NVIDIA, global ou padrão) e "Usar a configuração global"/"Padrão do driver" tira a alteração.
  - Oclusão de ambiente, filtragem anisotrópica, suavização (FXAA, gama, modo, configuração, transparência), FPS máximo e em segundo plano, CUDA Sysmem Fallback, modo de baixa latência (Desligado/Ligado/Ultra), tecnologia do monitor, MFAA, GDI do OpenGL, gerenciamento de energia, taxa de atualização preferida, cache de sombreador, filtragem de textura, otimização encadeada, buffer triplo, sincronização vertical e método de apresentação Vulkan/OpenGL; em notebooks híbridos, processador gráfico preferido.
  - Também: Nitidez da imagem (a clássica), CUDA - GPUs e GPU com renderização OpenGL (mostram a placa e voltam à seleção automática), quadros pré-renderizados e superamostragem de taxa variável de Realidade Virtual; DSR fica na configuração do monitor, então a linha explica e abre o Painel de Controle. Nomes e ordem iguais aos do Painel em português; "Anti-aliasing - modo" ganhou "Desligado".
  - Aplicar só o que mudou, Descartar e Restaurar (global: padrão do driver; programa: padrão da NVIDIA).
- **Drivers · Bibliotecas para jogos**: verificação (sem baixar nada) do Visual C++ 2005 a 2015-2022 e do DirectX de junho de 2010, em 32 e 64 bits. A instalação vai ficar na aba Instalar apps.
- **Drivers · driver e configurações ajustados à placa de cada PC** (os scripts do Ultimate foram feitos para a RTX 4090 de mesa do autor):
  - O app identifica a placa NVIDIA pelo código PCI (funciona até sem driver, depois do DDU): geração (Kepler até Blackwell), RTX ou GTX, memória, notebook ou PC de mesa e gráfico híbrido. O cartão mostra "Sua placa: …" e o que foi ajustado.
  - **Driver certo**: sem driver, a busca usa a geração da placa e se é notebook, em vez de uma RTX de mesa qualquer. Assim GTX 900/10 recebem o último driver que as suporta e notebooks recebem o driver de notebook (inclusive 900M/700M e MX).
  - **Perfil global ajustado**: notebook mantém a energia do driver (desempenho máximo gasta bateria e esquenta parado); placa de até 8 GB mantém o Sysmem Fallback (CUDA/IA não trava por falta de memória); notebook com gráfico híbrido usa a NVIDIA como placa preferida, o equivalente do "Preferred OpenGL GPU" fixo da 4090.
  - **Instalação enxuta ajustada**: DLDSR sai em placas GTX (só existe em RTX), o Platform Controller sai em PC de mesa e fica em notebook (Dynamic Boost), e a porta USB-C fica nas RTX 20 de mesa.
- **Drivers · NVIDIA Settings** (o "5 Graphics/5 Nvidia Settings" do Ultimate; as configurações dele já estavam no cartão de configurações recomendadas, com desfazer):
  - Botões **Abrir Painel de Controle NVIDIA** (sem ele instalado, abre a página na Microsoft Store em vez de instalar um .appx de espelho) e **Atalho na Área de Trabalho**.
  - **Perfil global NVIDIA no padrão do driver** (a opção "Default" do script), com aviso de que tira tudo o que foi mudado no perfil global e não tem desfazer.
  - O Profile Inspector separado do script não é baixado: o app já tem o seu.
- **Perfil global recomendado**: Modo de latência baixa em **Ligado** (em vez de Ultra) e **GPU com renderização OpenGL em Seleção automática** (tira a placa fixa; o Desfazer devolve).
- **Drivers · um cartão só para instalar o driver**: escolha **Normal** (driver completo, silencioso e limpo) ou **Enxuta (debloat)**; nas duas, as configurações marcadas no mesmo cartão (Painel NVIDIA/Profile Inspector, registro e Windows) são **aplicadas logo depois de instalar**, como no script. Ajustes que a reinstalação do driver desfez deixam de aparecer como APLICADO e voltam marcados para aplicar de novo. A limpeza com o DDU agora só faz a parte dela (não baixa mais o driver NVIDIA), corrige a segunda tentativa (Settings.xml somente leitura), usa o 7-Zip se o instalador do DDU não extrair sozinho e guarda o registro de cada etapa em `%LocalAppData%\PcismoMaster\ddu-log.txt`.
- **Drivers · Instalação enxuta do driver (debloat)** (o "5 Graphics/4 Driver Debloat Install & Settings" do Ultimate; os ajustes do script já estão no cartão de configurações recomendadas):
  - NVIDIA: abre o pacote com o 7-Zip, tira os componentes marcados (telemetria, NVIDIA App, ShadowPlay, containers, nView, FrameView…) e instala em silêncio e limpo. Baixa o mais recente (assinatura conferida) ou usa um instalador seu.
  - AMD e Intel: instalador escolhido por você (assinatura da AMD/Intel conferida, fabricante detectado pelo pacote), extras desligados no pacote e limpezas depois de instalar.
  - **Cada componente é opcional** e o que quebra algo vem desmarcado com o motivo: áudio HDMI/DisplayPort, PhysX, DLDSR, Dynamic Boost de notebooks, microfone/caixas de notebooks AMD, HDCP e firmware da Intel.
  - Serviços são **desativados com backup** em vez de apagados (`sc delete` do script) e entradas de inicialização saem com backup: botão **Desfazer as limpezas**.
  - Sem 7-Zip, oferece instalar pelo winget. Painel de Controle NVIDIA continua vindo da Microsoft Store. Sem reinício automático.
- **Drivers · Instalar driver e configurações recomendadas** (o "5 Graphics/3 Driver Updated Install & Settings" do Ultimate):
  - **Instalar driver NVIDIA (silencioso e limpo)**: download oficial com assinatura conferida, instalação sem telas (`-s -noreboot -noeula -clean`), sem 7-Zip; se faltar o Painel de Controle NVIDIA, abre a página dele na Microsoft Store.
  - **Configurações recomendadas** em interruptores com selo SEGURO/AVANÇADO, só dos fabricantes presentes no PC, **cada uma com backup e desfazer** (o script não guardava nada): NVIDIA (PhysX na GPU, menu de desenvolvedor, contadores de desempenho, sem ícone na bandeja, nitidez clássica), AMD (Anti-Lag, VSync off, texturas em desempenho, tesselação, Vari-Bright, Adrenalin sem notificações), Intel (VSync off, baixa latência, VRR global off) e Windows (gerenciamento automático de cor off, modo MSI, todos os ícones da bandeja).
  - **Perfil global recomendado** do script aplicado pelo nosso Profile Inspector, guardando o valor anterior de cada configuração; **G-SYNC desligado** virou opção separada.
  - Desligados por padrão (AVANÇADO): P-state dinâmico off (mais consumo em repouso), **HDCP off** (quebra Netflix/Disney+/Blu-ray, pede confirmação) e G-SYNC off. Fora do app: aceitar o EULA da AMD por você e o "Preferred OpenGL GPU" fixo de uma RTX 4090.
  - Sem reinício automático: botão **Reiniciar agora** quando precisa.
- **Drivers · Instalar driver atualizado** (o "5 Graphics/2 Driver Updated Install" do Ultimate, aproveitando o que a aba já fazia):
  - A placa de vídeo é reconhecida **mesmo sem driver** (pelo código PCI do fabricante), como logo depois da limpeza com o DDU; antes ela sumia da lista e não dava para instalar.
  - Novos botões **Instalar o driver mais recente de: NVIDIA · AMD · Intel** para escolher o fabricante à mão.
  - NVIDIA: se o modelo não for identificado, consulta o driver GeForce unificado mais recente (a mesma consulta do Ultimate); download oficial com assinatura conferida.
  - AMD: sem modelo reconhecido, procura o instalador que detecta a placa sozinho; se o site não entregar o link, oferece a página oficial.
  - Intel: abre a lista de drivers gráficos para Windows 11. Depois de instalar o driver NVIDIA, dicas do NVIDIA App (desligar Estatísticas, Filtros de jogo e otimização automática).
- **Drivers · Limpeza completa do driver de vídeo (DDU)** (versão do PcismoMaster do "5 Graphics/1 Driver Clean" do Ultimate):
  - Modos **Automático** (o DDU limpa sozinho no modo de segurança e o PC reinicia normal) e **Manual** (abre o DDU no modo de segurança); escolha da placa (NVIDIA, AMD, Intel ou todas).
  - Baixa o instalador oficial do DDU e **confere a assinatura da Wagnardsoft**; extrai sem precisar do 7-Zip (o script original falhava sem ele). Também aceita um DDU baixado por você.
  - Ponto de restauração, **tudo preparado e conferido antes de mexer no boot**, atalho de emergência "Sair do modo de segurança" na Área de Trabalho e reinício com **contagem regressiva e Cancelar** (desfaz o modo de segurança).
  - Bloqueio do driver automático do Windows Update **com botão para desfazer** (o script deixava permanente). Remover áudio Realtek/Sound Blaster virou opção, **desligada por padrão**.
- **FERRAMENTAS · Drivers ganhou sub-abas**: **Drivers** (o que já existia, sem mudanças) e o novo **Profile Inspector**.
- **Profile Inspector** (no estilo do NVIDIA Profile Inspector): lê e grava os perfis do driver NVIDIA (DRS da NVAPI), o mesmo banco do Painel de Controle NVIDIA, inclusive o que o painel esconde.
  - Lista de todos os perfis do driver (perfil global + milhares de jogos), com busca.
  - Configurações agrupadas como no Profile Inspector (sincronia, antisserrilhado, texturas, DLSS/upscaling, energia etc.), com nome, valor editável (lista de valores conhecidos ou número em hexadecimal/decimal) e a origem de cada valor: **você**, **NVIDIA** (predefinido para o jogo), **global** (herdado) ou **padrão** do driver. Passe o mouse para ver a descrição e o ID.
  - Alterações ficam pendentes (em amarelo) até **Aplicar alterações**; **Desfazer** descarta; ↺ tira a configuração do perfil (volta a herdar).
  - **Restaurar padrão do perfil**, **Novo perfil…**, **Excluir perfil**, executáveis do perfil (adicionar/tirar) e **Exportar/Importar .nip** compatível com o Profile Inspector.
  - Filtros: buscar configuração, **Só alteradas** e **Mostrar tudo** (configurações escondidas e as que o driver conhece sem nome).
  - Catálogo de configurações do nvidiaProfileInspector (Orbmu2k, licença MIT, em `licenses/`).
- Novo ComboBox escuro no tema do app.
- Fluidez: o .exe publicado já vem compilado (ReadyToRun + TieredPGO), sem engasgo ao abrir cada aba pela primeira vez.
- Versão atualizada para 0.8.0.

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
