using System.IO;

namespace ResetExplode.Modular;

/// <summary>Nível de risco mostrado no selo e na confirmação.</summary>
public enum ScriptRisk { Seguro, Atencao, Perigoso }

/// <summary>Uma opção de um script (um botão): o .ps1 embutido em Tweaks\Scripts e o que avisar antes de rodar.</summary>
public sealed record ScriptOption(string Id, string Label, ScriptRisk Risk, string? Warning = null,
    bool Restarts = false, bool SafeMode = false, bool Downloads = false, bool NeedsUsb = false, bool IsDefault = false);

/// <summary>Um script do Ultimate inteiro: um cartão com um botão por opção do menu original.</summary>
public sealed record ScriptCard(string Page, string Title, string Description, string Source, ScriptOption[] Options)
{
    public ScriptRisk MaxRisk => Options.Max(o => o.Risk);
}

/// <summary>
/// Os scripts completos do Ultimate (FR33THY, MIT), cada opção do menu num botão. O texto do .ps1 é o do script,
/// sem o menu, as pausas e a autoelevação (o app já roda como administrador). Rodam só quando o usuário confirma.
/// Fora: "4 Keys" (abre um ativador não oficial do Windows), "12 Updates Pause" (página removida a pedido) e as
/// opções interativas de Prioridade/SMT/Núcleo 1 (pedem PID ou arquivo no console; o app faz isso no cartão Por jogo).
/// </summary>
public static class ScriptCatalog
{
    private const ScriptRisk S = ScriptRisk.Seguro, A = ScriptRisk.Atencao, P = ScriptRisk.Perigoso;
    private static ScriptOption O(string id, string label, ScriptRisk risk, string? warn = null, bool restarts = false, bool safeMode = false,
        bool downloads = false, bool usb = false, bool def = false) => new(id, label, risk, warn, restarts, safeMode, downloads, usb, def);
    private static ScriptCard C(string page, string title, string desc, string source, params ScriptOption[] options) => new(page, title, desc, source, options);

    private const string SafeModeWarn = "Reinicia o PC em Modo de Segurança, aplica lá e reinicia de novo sozinho (dois reinícios; a tela pode ficar preta por alguns minutos). Não desligue o PC no meio.";

    public static readonly ScriptCard[] All =
    {
        // ---------------- Iniciar (3 Setup) ----------------
        C("setup", "BitLocker", "Desliga ou liga a criptografia BitLocker do disco do sistema.", "3 Setup/1 BitLocker.ps1",
            O("setup-1-1", "Desligar", A, "O disco é descriptografado: se o PC ou o disco for roubado, os dados ficam legíveis."),
            O("setup-1-2", "Ligar", A, "Guarde a chave de recuperação que o Windows mostrar: sem ela você pode perder acesso ao disco.", def: true)),
        C("setup", "Compressão de memória", "Liga, desliga ou mostra o estado da compressão de memória (MMAgent).", "3 Setup/2 Memory Compression.ps1",
            O("setup-2-1", "Desligar", S), O("setup-2-2", "Ligar", S, def: true), O("setup-2-3", "Ver estado", S)),
        C("setup", "Home para Pro", "Abre a troca de edição do Windows com a chave genérica do Pro (copiada para a área de transferência).", "3 Setup/3 Convert Home To Pro.ps1",
            O("setup-3", "Trocar edição", A, "A chave genérica só troca a edição: o Windows Pro fica sem ativar até você comprar e digitar uma licença Pro. O script sugere desligar a internet antes.")),
        C("setup", "Ativação", "Abre a página de ativação do Windows.", "3 Setup/5 Activation.ps1", O("setup-5", "Abrir", S)),
        C("setup", "Bloquear drivers e atualizações", "Políticas do Windows Update para drivers ou para todas as atualizações; também grava o setupcomplete.cmd num pendrive de instalação do Windows (vale na instalação limpa). Só Pro, LTSC, IoT e Server.", "3 Setup/5 Updates Drivers Block.ps1",
            O("setup-5-1", "Bloquear drivers", A, "O Windows para de instalar drivers sozinho; você instala cada driver."),
            O("setup-5-2", "Bloquear drivers (pendrive)", A, "Grava no pendrive de instalação escolhido; não muda este PC.", usb: true),
            O("setup-5-3", "Desbloquear drivers", S, def: true),
            O("setup-5-4", "Bloquear atualizações", P, "Nenhuma atualização do Windows é instalada, nem as de segurança, até você desbloquear."),
            O("setup-5-5", "Bloquear atualizações (pendrive)", P, "O Windows instalado por esse pendrive fica sem atualizações de segurança até você desbloquear. Não muda este PC.", usb: true),
            O("setup-5-6", "Desbloquear atualizações", S, def: true)),
        C("setup", "Data, idioma e região", "Abre as configurações de data e hora.", "3 Setup/6 Date Language Region Time.ps1", O("setup-6", "Abrir", S)),
        C("setup", "Apps de inicialização", "Abre os apps de inicialização (Configurações e Gerenciador de Tarefas).", "3 Setup/7 e 8 Startup Apps.ps1",
            O("setup-7", "Configurações", S), O("setup-8", "Gerenciador de Tarefas", S)),
        C("setup", "Apps em segundo plano", "Apps da Microsoft Store rodando escondidos.", "3 Setup/9 Background Apps.ps1",
            O("setup-9-1", "Desligar", S), O("setup-9-2", "Padrão", S, def: true)),
        C("setup", "Edge (script completo)", "Otimizar: políticas do Edge, uBlock Origin e APAGA os serviços, as tarefas agendadas, o Active Setup e o RunOnce do Edge. Padrão: tira as políticas e reinstala o Edge.", "3 Setup/10 Edge Settings.ps1",
            O("setup-10-1", "Otimizar", P, "Os serviços e as tarefas do Edge são apagados, não desativados: o Edge deixa de se atualizar (sem correções de segurança) até ser reinstalado pelo botão Padrão."),
            O("setup-10-2", "Padrão", A, "Baixa e abre o instalador do Edge (edge.exe) do GitHub do autor do Ultimate.", downloads: true, def: true)),
        C("setup", "Store (script completo)", "Otimizar: sem atualização automática de apps, sem vídeo automático, sem avisos de instalação e sem experiências personalizadas (edita o settings.dat da Store). Padrão: volta e redefine a Store (wsreset).", "3 Setup/11 Store Settings.ps1",
            O("setup-11-1", "Otimizar", S), O("setup-11-2", "Padrão", A, "Redefine a Store (apaga o cache dela).", def: true)),

        // ---------------- Windows ----------------
        C("win", "Barra de tarefas e Menu Iniciar (script completo)", "Limpar: além dos valores de registro, DESAFIXA todos os itens da barra e do Iniciar e troca o layout do Iniciar. Padrão: volta o layout do Windows.", "Windows/1 Start Menu Taskbar.ps1",
            O("win-1-1", "Limpar", A, "Tudo o que está fixado na barra de tarefas e no Menu Iniciar sai (os programas continuam instalados). O Explorador reinicia."),
            O("win-1-2", "Padrão", A, "Volta os fixados padrão do Windows (os seus somem). O Explorador reinicia.", def: true)),
        C("win", "Layout do Menu Iniciar", "Menu Iniciar novo (25H2) ou o anterior (24H2).", "Windows/2 Start Menu Layout.ps1",
            O("win-2-1", "25H2", S), O("win-2-2", "24H2", S, def: true)),
        C("win", "Atalhos no Menu Iniciar", "Cria atalhos para as pastas do Menu Iniciar, da Inicialização e da Lixeira e abre as pastas do Menu Iniciar.", "Windows/3 Start Menu Shortcuts.ps1",
            O("win-3", "Criar", S)),
        C("win", "Menu de contexto (script completo)", "Menu clássico e sem os itens extras. Padrão devolve tudo.", "Windows/4 Context Menu.ps1",
            O("win-4-1", "Limpar", A, "Apaga do registro as chaves de Fixar no Acesso rápido, Compatibilidade, Biblioteca, Compartilhar e Enviar para. O Padrão recria."),
            O("win-4-2", "Padrão", S, def: true)),
        C("win", "Tema preto", "Modo escuro, sem transparência e cores pretas.", "Windows/5 Theme Black.ps1",
            O("win-5-1", "Preto", S), O("win-5-2", "Padrão", S, def: true)),
        C("win", "Tela de bloqueio e papel de parede pretos", "Imagem preta na tela de bloqueio/saída e no papel de parede.", "Windows/6 Signout Lockscreen Wallpaper Black.ps1",
            O("win-6-1", "Preto", S), O("win-6-2", "Padrão", S, def: true)),
        C("win", "Fotos das contas pretas", "Troca as imagens padrão de conta do Windows por imagens pretas (guarda uma cópia antes).", "Windows/7 User Account Pictures Black.ps1",
            O("win-7-1", "Pretas", A, "Sobrescreve as imagens em ProgramData\\Microsoft\\User Account Pictures. A cópia fica em ProgramData\\User Account Pictures; Padrão devolve."),
            O("win-7-2", "Padrão", S, def: true)),
        C("win", "Widgets (script completo)", "Desliga os Widgets e fecha os processos deles.", "Windows/8 Widgets.ps1",
            O("win-8-1", "Desligar", S), O("win-8-2", "Padrão", S, def: true)),
        C("win", "Copilot (script completo)", "Desligar: política e DESINSTALA o app Copilot.", "Windows/9 Copilot.ps1",
            O("win-9-1", "Desligar", A, "Desinstala o app Copilot (reinstale pela Store se quiser de volta)."), O("win-9-2", "Padrão", S, def: true)),
        C("win", "Atalhos do script", "Modo de Jogo, precisão do ponteiro, escala, som, isolamento de núcleo e Gerenciador de Tarefas.", "Windows/10, 11, 12, 18, 23, 32",
            O("win-10", "Modo de Jogo", S), O("win-11", "Precisão do ponteiro", S), O("win-12", "Escala", S), O("win-18", "Gerenciador de Tarefas", S),
            O("win-23", "Som", S), O("win-32", "Isolamento de núcleo", S)),
        C("win", "Bloatware", "Remove todos os apps da Store, recursos opcionais e programas que vêm com o Windows (menos os essenciais da lista do script), OneDrive, Área de Trabalho Remota, Ferramenta de Captura antiga e GameInput. Os outros botões reinstalam partes.", "Windows/13 Bloatware.ps1",
            O("win-13-2", "Remover tudo", P, "Desinstala de uma vez quase todos os apps da Microsoft (Fotos e Bloco de Notas ficam), os recursos opcionais do Windows, o OneDrive (seus arquivos na nuvem continuam online) e outros programas. Não tem desfazer automático: alguns voltam pelos botões ao lado, outros só reinstalando o Windows. Leva vários minutos."),
            O("win-13-3", "Reinstalar Store", A), O("win-13-4", "Reinstalar todos os apps", A, "Reinstala os apps que ainda estão no disco; os removidos de vez não voltam."),
            O("win-13-5", "Lista de recursos", S), O("win-13-6", "Lista de recursos antigos", S),
            O("win-13-7", "Reinstalar OneDrive", S),
            O("win-13-8", "Reinstalar Área de Trabalho Remota", A, downloads: true), O("win-13-9", "Reinstalar Ferramenta de Captura", A, downloads: true)),
        C("win", "Conferir o que está instalado", "Lista programas, recursos e apps instalados (para ver o que sobrou depois do Bloatware).", "Windows/14 a 17",
            O("win-14", "Programas", S), O("win-15", "Recursos antigos", S), O("win-16", "Apps da Store", S), O("win-17", "Recursos opcionais", S)),
        C("win", "Game Bar (script completo)", "Desligar: valores de registro e DESINSTALA os apps Xbox/Game Bar e o GameInput. Padrão: reinstala (baixa o WebView e a ferramenta de reparo do Gaming Services).", "Windows/19 Gamebar.ps1",
            O("win-19-1", "Desligar", A, "Desinstala os apps Xbox e Game Bar: jogos do Game Pass/Xbox app podem parar de abrir até o Padrão."),
            O("win-19-2", "Padrão", A, downloads: true, def: true)),
        C("win", "Edge e WebView", "Desinstala o Microsoft Edge e o WebView2 à força, apaga os serviços, pastas e atalhos dele. Padrão reinstala os dois.", "Windows/20 Edge & WebView.ps1",
            O("win-20-1", "Desinstalar", P, "Muitos programas usam o WebView2 para abrir telas (Teams, Widgets, launchers de jogos, instaladores): eles podem parar de funcionar. Apaga a pasta Program Files (x86)\\Microsoft inteira. Para voltar, use Padrão."),
            O("win-20-2", "Padrão", A, "Baixa e abre os instaladores do Edge e do WebView2.", downloads: true, def: true)),
        C("win", "Bloco de Notas", "Abre sempre em janela nova, sem guardar abas e sem o reescrever com IA (edita o settings.dat do Bloco de Notas).", "Windows/21 Notepad Settings.ps1",
            O("win-21-1", "Otimizar", S), O("win-21-2", "Padrão", A, "Apaga as configurações do Bloco de Notas (volta tudo ao de fábrica).", def: true)),
        C("win", "Configurações do Painel de Controle (script completo)", "Importa o .reg inteiro do script, inclusive as chaves protegidas (o ajuste com desfazer acima pula essas) e a escala de 100%.", "Windows/22 Control Panel Settings.ps1",
            O("win-22-1", "Otimizar", A, "Centenas de configurações de uma vez, sem backup individual; põe a escala da tela em 100%. Padrão volta os valores padrão do Windows (não os seus)."),
            O("win-22-2", "Padrão", A, def: true)),
        C("win", "EQ de volume (Loudness)", "Liga o equalizador de volume em todos os dispositivos de som (reinicia o serviço de áudio e abre o Som).", "Windows/24 Loudness EQ.ps1",
            O("win-24", "Ligar", A, "O som para por alguns segundos. Se ficar ruim, desmarque Equalização de volume em Som → Propriedades → Aprimoramentos ou reinstale o driver de áudio.")),
        C("win", "Dispositivos sem economia de energia (script completo)", "Todos os dispositivos ACPI, HID, PCI e USB.", "Windows/25 Device Manager Power Savings & Wake.ps1",
            O("win-25-1", "Desligar economia", A, "Notebooks gastam mais bateria; o PC não acorda mais pelo mouse/teclado."), O("win-25-2", "Padrão", S, def: true)),
        C("win", "Placas de rede sem economia (script completo)", "Todas as placas de rede.", "Windows/26 Network Adapter Power Savings & Wake.ps1",
            O("win-26-1", "Desligar economia", S), O("win-26-2", "Padrão", S, def: true)),
        C("win", "Rede só IPv4 (script completo)", "Desliga IPv6, descoberta de rede, QoS e compartilhamento de arquivos.", "Windows/27 Network IPv4 Only.ps1",
            O("win-27-1", "Só IPv4", A, "Deixa de ver e compartilhar pastas e impressoras com outros PCs da rede."), O("win-27-2", "Padrão", S, def: true)),
        C("win", "Cache de gravação (script completo)", "Todos os discos SCSI/NVMe.", "Windows/28 Write Cache Buffer Flushing.ps1",
            O("win-28-1", "Desligar esvaziamento", A, "Numa queda de energia sem nobreak pode perder ou corromper o que estava sendo gravado."), O("win-28-2", "Padrão", S, def: true)),
        C("win", "Plano de energia (script completo)", "Cria o plano Ultimate, APAGA todos os outros planos e desliga a hibernação. Padrão restaura os planos de fábrica.", "Windows/29 Power Plan.ps1",
            O("win-29-1", "Ultimate", A, "Os outros planos de energia (inclusive os da fabricante do notebook) são apagados; o Padrão recria só os de fábrica do Windows."),
            O("win-29-2", "Padrão", A, "Restaura os planos de fábrica do Windows (apaga os personalizados).", def: true)),
        C("win", "Serviço de resolução do timer", "Compila e instala um serviço que mantém o timer do Windows em 0,5 ms (Set Timer Resolution Service).", "Windows/30 Timer Resolution.ps1",
            O("win-30-1", "Instalar", A, "Instala um serviço que roda sempre com o Windows. Pode aumentar um pouco o consumo de energia."),
            O("win-30-2", "Remover", S, def: true)),
        C("win", "UAC (script completo)", "Controle de Conta de Usuário.", "Windows/31 UAC.ps1",
            O("win-31-1", "Desligar", P, "Qualquer programa roda com acesso total de administrador sem perguntar; alguns apps da Store deixam de abrir."),
            O("win-31-2", "Padrão", S, def: true)),
        C("win", "Defender otimizado", "Desliga proteções do Windows Security que custam desempenho: isolamento de núcleo (integridade de memória, VBS), SmartScreen, proteção contra exploits, PUA, LSA e lista de drivers vulneráveis. A proteção antivírus em tempo real continua.", "Windows/33 Defender Optimize.ps1",
            O("win-33-1", "Otimizar", P, "Desliga proteções contra ataques avançados. Vanguard (Valorant) e FACEIT exigem o isolamento de núcleo e podem não abrir. " + SafeModeWarn, restarts: true, safeMode: true),
            O("win-33-2", "Padrão", A, SafeModeWarn, restarts: true, safeMode: true, def: true)),
        C("win", "Autoruns (inicialização e tarefas)", "Cria um ponto de restauração e APAGA todos os itens de inicialização (Run/RunOnce, pastas Inicializar) e TODAS as tarefas agendadas que não são da Microsoft; depois baixa e abre o Autoruns.", "Windows/34 Autoruns Startup Tasks & Apps Check.ps1",
            O("win-34", "Limpar e abrir Autoruns", P, "Antivírus, drivers de som/RGB, launchers, atualizadores e qualquer programa que abre com o Windows deixam de abrir sozinhos, e as tarefas deles são apagadas sem volta (só pelo ponto de restauração). Mexe temporariamente no serviço TrustedInstaller para apagar tarefas protegidas.", downloads: true)),
        C("win", "Limpeza (script completo)", "Temporários, cache do Windows Update, DumpStack, PerfLogs e TAMBÉM as pastas Windows.old, XboxGames e inetpub; abre a Limpeza de Disco.", "Windows/35 Cleanup.ps1",
            O("win-35", "Limpar tudo", P, "Apaga a pasta C:\\XboxGames (JOGOS DO GAME PASS INSTALADOS ali), C:\\Windows.old (impede voltar para a versão anterior do Windows) e C:\\inetpub, sem passar pela Lixeira.")),
        C("win", "Ponto de restauração (script)", "Cria um ponto de restauração do jeito do script (libera criar mais de um por dia).", "Windows/36 Restore Point.ps1",
            O("win-36", "Criar", S)),

        // ---------------- Hardware ----------------
        C("hw", "Escala sem aceleração (script completo)", "Grava a escala da tela e a curva do mouse do script para a escala escolhida (as curvas de 200% e 350% do script têm erro de digitação; o cartão acima usa as certas).", "Hardware/1 Scaling Higher No Accel.ps1",
            O("hw-1-1", "100%", A, "Vale depois de sair da conta."), O("hw-1-2", "125%", A, "Vale depois de sair da conta."), O("hw-1-3", "150%", A, "Vale depois de sair da conta."),
            O("hw-1-4", "175%", A, "Vale depois de sair da conta."), O("hw-1-5", "200%", A, "Curva do script com erro de digitação: o mouse pode ficar estranho. Prefira o cartão acima."),
            O("hw-1-6", "225%", A, "Vale depois de sair da conta."), O("hw-1-7", "250%", A, "Vale depois de sair da conta."), O("hw-1-8", "300%", A, "Vale depois de sair da conta."),
            O("hw-1-9", "350%", A, "Curva do script com erro de digitação: o mouse pode ficar estranho. Prefira o cartão acima.")),
        C("hw", "Limite de polling em segundo plano", "Mouse a 125 Hz para programas em segundo plano.", "Hardware/2 Background Polling Rate Cap.ps1",
            O("hw-2-1", "Sem limite", S), O("hw-2-2", "Padrão (125 Hz)", S, def: true)),
        C("hw", "Teste de polling do mouse", "Baixa e abre o Mouse Movement Recorder e mostra as dicas de mouse do script.", "Hardware/3 Mouse Polling Rate Test.ps1",
            O("hw-3", "Baixar e abrir", A, "Baixa um programa de terceiros.", downloads: true)),
        C("hw", "Overclock do controle (hidusbf)", "Baixa o hidusbf e instala o driver filtro dele para aumentar a taxa de polling de controles USB.", "Hardware/4 Controller Overclock.ps1",
            O("hw-4", "Baixar e instalar", P, "Instala um driver de terceiros (não é da Microsoft nem do fabricante do controle). Pode fazer o controle ou o USB pararem de responder; anti-cheats podem reclamar. Para tirar, use o Setup do hidusbf.", downloads: true)),
        C("hw", "Teste de polling do controle", "Baixa e abre o Polling (gamepadla).", "Hardware/5 Controller Polling Rate Test.ps1",
            O("hw-5", "Baixar e abrir", A, "Baixa um programa de terceiros.", downloads: true)),
        C("hw", "Testes e guias (script)", "Teste do monitor (UFO, com dicas), teste de bufferbloat e guia de peças.", "Hardware/6, 7 e 8",
            O("hw-6", "Monitor", S), O("hw-7", "Bufferbloat", S), O("hw-8", "Guia de peças", S)),

        // ---------------- Avançado ----------------
        C("adv", "Defender (antivírus) desligado", "Desliga o Microsoft Defender inteiro: antivírus em tempo real, proteção na nuvem, SmartScreen, firewall de exploits, serviços e drivers do Defender.", "Advanced/1 Defender.ps1",
            O("adv-1-1", "Desligar", P, "O PC fica SEM ANTIVÍRUS. Qualquer arquivo malicioso roda sem aviso. Use só se tiver outro antivírus ou souber exatamente o que está fazendo. " + SafeModeWarn, restarts: true, safeMode: true),
            O("adv-1-2", "Ligar (padrão)", A, SafeModeWarn, restarts: true, safeMode: true, def: true)),
        C("adv", "Firewall (script)", "Firewall do Windows.", "Advanced/2 Firewall.ps1",
            O("adv-2-1", "Desligar", P, "O PC fica exposto na rede; use só atrás de um roteador."), O("adv-2-2", "Ligar (padrão)", S, def: true)),
        C("adv", "Spectre e Meltdown (script)", "Proteções contra os defeitos Spectre/Meltdown do processador.", "Advanced/3 Spectre Meltdown.ps1",
            O("adv-3-1", "Desligar", P, "Deixa o PC vulnerável a ataques por esses defeitos. Vale depois de reiniciar."), O("adv-3-2", "Ligar (padrão)", S, def: true)),
        C("adv", "DEP (script)", "Prevenção de Execução de Dados no boot.", "Advanced/4 Data Execution Prevention.ps1",
            O("adv-4-1", "Desligar", P, "Mais exposto a ataques de memória; anti-cheats modernos podem não abrir."), O("adv-4-2", "Ligar (padrão)", S, def: true)),
        C("adv", "Aviso de segurança de downloads (script)", "Janela de \"Deseja executar este arquivo?\".", "Advanced/5 File Download Security Warning.ps1",
            O("adv-5-1", "Desligar", A, "Fica mais fácil abrir algo perigoso sem perceber."), O("adv-5-2", "Ligar (padrão)", S, def: true)),
        C("adv", "Recursos de memória MMAgent (script)", "Prefetch, pré-lançamento de apps, combinação de páginas e API de operações.", "Advanced/6 MMAgent Features.ps1",
            O("adv-6-1", "Desligar", A), O("adv-6-2", "Padrão", S, def: true), O("adv-6-3", "Ver estado", S)),
        C("adv", "Resizable BAR NVIDIA (script)", "Baixa o NVIDIA Profile Inspector do autor e importa o perfil de ReBAR; TO BIOS reinicia direto na BIOS para ligar o ReBAR lá.", "Advanced/7 ReBar Force.ps1",
            O("adv-7-1", "Padrão do driver", S, downloads: true, def: true), O("adv-7-2", "Forçar ligado", A, downloads: true), O("adv-7-3", "Forçar desligado", A, downloads: true),
            O("adv-7-4", "Reiniciar na BIOS", A, "O PC reinicia NA HORA direto na BIOS/UEFI. Salve tudo antes.", restarts: true, downloads: true)),
        C("adv", "MPO (script)", "Multiplane Overlay.", "Advanced/11 Mpo.ps1",
            O("adv-11-1", "Ligado (padrão)", S, def: true), O("adv-11-2", "Desligado", A, "Pode aumentar a latência em jogos em janela sem borda.")),
        C("adv", "Tela cheia (script)", "Otimizações de tela cheia (padrão) ou tela cheia exclusiva.", "Advanced/12 Hardware Legacy Flip.ps1",
            O("adv-12-1", "Otimizações (padrão)", S, def: true), O("adv-12-2", "Exclusiva", A)),
        C("adv", "Composed Independent Flip (script)", "Modo de apresentação da GPU.", "Advanced/13 Hardware Composed Independent Flip.ps1",
            O("adv-13-1", "Composed Independent Flip", A), O("adv-13-2", "Independent Flip (padrão)", S, def: true)),
        C("adv", "AMD ULPS (script)", "Estado de consumo ultrabaixo da placa AMD.", "Advanced/14 Ulps.ps1",
            O("adv-14-1", "Ligado (padrão)", S, def: true), O("adv-14-2", "Desligado", A)),
        C("adv", "Drivers sem WHQL com Secure Boot (script)", "Instalar drivers sem assinatura da Microsoft.", "Advanced/15 Driver Whql Secure Boot Bypass.ps1",
            O("adv-15-1", "Desligado (padrão)", S, def: true), O("adv-15-2", "Ligado", P, "Drivers não verificados rodam no núcleo do Windows: um driver malicioso tem controle total do PC.")),
        C("adv", "Atalhos do teclado (script)", "Tecla Windows, teclas de mídia e atalhos Win+.", "Advanced/16 Keyboard Shortcuts.ps1",
            O("adv-16-1", "Padrão", S, def: true), O("adv-16-2", "Desligar", A, "A tecla Windows e os atalhos param de funcionar até o Padrão.")),
        C("adv", "Serviços do Windows", "Desliga em massa a maior parte dos serviços do Windows (lista do script) pelo Modo de Segurança, com ponto de restauração antes. Padrão volta os valores de fábrica.", "Advanced/17 Services.ps1",
            O("adv-17-1", "Desligar", P, "Muita coisa para de funcionar: impressora, Bluetooth, Wi-Fi em alguns PCs, Xbox/Game Pass, Store, atualizações, áudio de alguns dispositivos, VPN e mais. Use só num PC dedicado a jogos e saiba voltar. " + SafeModeWarn, restarts: true, safeMode: true),
            O("adv-17-2", "Padrão", A, SafeModeWarn, restarts: true, safeMode: true, def: true)),
        C("adv", "Iniciar, Pesquisa, Shell e Mobsync (script completo)", "Desligar: além da pesquisa, MOVE as pastas do Menu Iniciar, da Pesquisa e do ShellExperienceHost para fora de SystemApps (deixam de funcionar) e reinicia o Explorador.", "Advanced/18 Start Search Shell Mobsync.ps1",
            O("adv-18-1", "Desligar", P, "O Menu Iniciar, a pesquisa, a central de ações/notificações e o relógio do calendário param de abrir. Para voltar, use Padrão."),
            O("adv-18-2", "Padrão", A, "O Explorador reinicia.", def: true)),
        C("adv", "Driver NVMe (script)", "Driver NVMe nativo novo do Windows.", "Advanced/19 NVME Faster Driver.ps1",
            O("adv-19-1", "Mais rápido", P, "Recurso escondido da Microsoft: se o PC não iniciar, entre no modo de segurança e use Padrão."), O("adv-19-2", "Padrão", S, def: true)),
    };

    public static IEnumerable<ScriptCard> For(string page) => All.Where(c => c.Page == page);

    /// <summary>Texto do .ps1 embutido (Tweaks\Scripts\id.ps1).</summary>
    public static string Read(string id)
    {
        using var stream = typeof(ScriptCatalog).Assembly.GetManifestResourceStream("Tweaks/Scripts/" + id + ".ps1")
            ?? throw new FileNotFoundException("Script não encontrado: " + id);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Os avisos que a confirmação mostra para esta opção (do catálogo e do que o script faz).</summary>
    public static List<string> Warnings(ScriptOption o, ScriptCard card)
    {
        var list = new List<string>();
        if (o.Warning != null) list.Add(o.Warning);
        if (o.Restarts && !o.SafeMode && o.Warning?.Contains("reinicia", StringComparison.OrdinalIgnoreCase) != true)
            list.Add("O PC REINICIA sozinho no fim. Salve e feche tudo antes.");
        if (o.Downloads) list.Add("Baixa programa(s) de terceiros do GitHub do autor do Ultimate (FR33THY). Precisa de internet.");
        if (o.NeedsUsb) list.Add("Escolha a letra do pendrive de instalação do Windows na próxima janela.");
        if (o.Risk == ScriptRisk.Perigoso) list.Add("Sem desfazer automático pelo PcismoMaster: crie o ponto de restauração (marcado abaixo).");
        if (!o.IsDefault && card.Options.FirstOrDefault(x => x.IsDefault) is { } def)
            list.Add($"Para voltar, use o botão \"{def.Label}\" deste mesmo cartão.");
        return list;
    }
}
