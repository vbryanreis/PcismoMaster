using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResetExplode.Modular;

/// <summary>Selo de risco: SEGURO, ATENÇÃO (algo pode depender dele) ou PERIGOSO (pode quebrar o Windows).</summary>
public enum ScriptRisk { Seguro, Atencao, Perigoso }

public enum BloatKind { App, Biblioteca, Recurso, RecursoAntigo, Programa }

/// <summary>Um item que o "13 Bloatware" do Ultimate removeria, para escolher um a um.</summary>
/// <param name="Essential">Motivo pelo qual o script NÃO remove (o item aparece travado); null = pode remover.</param>
public sealed record BloatItem(BloatKind Kind, string Id, string Name, string Description, ScriptRisk Risk, string? Essential, string? Family = null);

/// <summary>
/// TWEAKING · Windows · Bloatware app por app: a mesma lista do "13 Bloatware.ps1" (apps da Store, recursos opcionais,
/// recursos antigos e programas), mas cada item com nome em português, selo de risco, botão de remover e de voltar.
/// Os itens que o script preserva aparecem travados, com o motivo.
/// </summary>
public static class Bloatware
{
    // -------- O que o script preserva (os mesmos padrões -notlike '*x*', com o motivo dos comentários do autor) --------

    private static readonly (string Pattern, string Why)[] KeepApps =
    {
        ("CBS", "parte do Windows: sem ele o Explorador quebra"),
        ("Microsoft.AV1VideoExtension", "codec de vídeo"), ("Microsoft.AVCEncoderVideoExtension", "codec de vídeo"),
        ("Microsoft.HEIFImageExtension", "codec de imagem"), ("Microsoft.HEVCVideoExtension", "codec de vídeo"),
        ("Microsoft.MPEG2VideoExtension", "codec de vídeo"), ("Microsoft.Paint", "o script mantém o Paint"),
        ("Microsoft.RawImageExtension", "codec de imagem"), ("Microsoft.SecHealthUI", "tela da Segurança do Windows"),
        ("Microsoft.VP9VideoExtensions", "codec de vídeo"), ("Microsoft.WebMediaExtensions", "codec de vídeo"),
        ("Microsoft.WebpImageExtension", "codec de imagem"), ("Microsoft.Windows.Photos", "o script mantém o Fotos"),
        ("Microsoft.Windows.ShellExperienceHost", "barra de tarefas"), ("Microsoft.Windows.StartMenuExperienceHost", "menu Iniciar"),
        ("Microsoft.WindowsNotepad", "o script mantém o Bloco de Notas"), ("NVIDIACorp.NVIDIAControlPanel", "Painel de Controle da NVIDIA"),
        ("windows.immersivecontrolpanel", "app Configurações"),
    };

    private static readonly (string Pattern, string Why)[] KeepCaps =
    {
        ("Microsoft.Windows.Ethernet", "driver de rede cabeada"), ("Microsoft.Windows.MSPaint", "Paint (Windows 10)"),
        ("Microsoft.Windows.Notepad", "Bloco de Notas"), ("Microsoft.Windows.Wifi", "driver de Wi-Fi"), ("NetFX3", ".NET 3.5 (jogos e programas antigos)"),
        ("VBSCRIPT", "sem ele os instaladores .msi param"), ("WMIC", "programas de monitoramento usam"),
        ("Windows.Client.ShellComponents", "Ferramenta de Captura (Windows 10)"),
    };

    private static readonly (string Pattern, string Why)[] KeepFeatures =
    {
        ("DirectPlay", "jogos antigos"), ("LegacyComponents", "jogos antigos"), ("NetFx3", ".NET 3.5"), ("NetFx4", ".NET 4"),
        ("SearchEngine-Client-Package", "pesquisa do Windows"), ("Server-Shell", "área de trabalho (Server)"), ("Windows-Defender", "Defender"),
        ("Server-Drivers-General", "internet (Server)"), ("ServerCore-Drivers-General", "internet (Server)"),
        ("Server-Gui-Mgmt", "recursos do Windows (Server)"), ("WirelessNetworking", "NVIDIA App (Server)"),
    };

    private static string? Keep((string Pattern, string Why)[] list, string name) =>
        list.FirstOrDefault(k => name.Contains(k.Pattern, StringComparison.OrdinalIgnoreCase)).Why;

    // -------- Nomes em português --------

    private static readonly Dictionary<string, (string Name, string Desc)> AppNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Clipchamp.Clipchamp"] = ("Clipchamp", "Editor de vídeo da Microsoft."),
        ["Microsoft.549981C3F5F10"] = ("Cortana", "Assistente antiga da Microsoft."),
        ["Microsoft.BingNews"] = ("Notícias", "Notícias do MSN."),
        ["Microsoft.BingWeather"] = ("Clima", "Previsão do tempo."),
        ["Microsoft.BingSearch"] = ("Pesquisa do Bing", "Pesquisa na web pelo Iniciar."),
        ["Microsoft.Copilot"] = ("Copilot", "Assistente de IA da Microsoft."),
        ["Microsoft.Windows.Ai.Copilot.Provider"] = ("Copilot (integração)", "Liga o Copilot ao Windows."),
        ["Microsoft.GamingApp"] = ("Xbox", "App Xbox (Game Pass). Sem ele, jogos do Game Pass não abrem."),
        ["Microsoft.GamingServices"] = ("Serviços de Jogos", "Necessário para jogos do Game Pass e da Microsoft Store."),
        ["Microsoft.GetHelp"] = ("Obter Ajuda", "Suporte da Microsoft."),
        ["Microsoft.Getstarted"] = ("Introdução / Dicas", "Dicas do Windows."),
        ["Microsoft.MicrosoftOfficeHub"] = ("Microsoft 365 (Office)", "Atalho para o Office online."),
        ["Microsoft.MicrosoftSolitaireCollection"] = ("Paciência (Solitaire)", "Jogos de cartas com anúncios."),
        ["Microsoft.MicrosoftStickyNotes"] = ("Notas Autoadesivas", "Post-its na área de trabalho."),
        ["Microsoft.MixedReality.Portal"] = ("Portal de Realidade Misturada", "Óculos de realidade virtual Windows."),
        ["Microsoft.Office.OneNote"] = ("OneNote", "Caderno de anotações."),
        ["Microsoft.OutlookForWindows"] = ("Outlook (novo)", "E-mail e calendário."),
        ["Microsoft.People"] = ("Pessoas", "Contatos."),
        ["Microsoft.PowerAutomateDesktop"] = ("Power Automate", "Automação de tarefas."),
        ["Microsoft.ScreenSketch"] = ("Ferramenta de Captura", "Print da tela (Win+Shift+S)."),
        ["Microsoft.SkypeApp"] = ("Skype", "Chamadas de vídeo."),
        ["Microsoft.StorePurchaseApp"] = ("Compras da Store", "Janela de pagamento da Microsoft Store."),
        ["Microsoft.Todos"] = ("Microsoft To Do", "Lista de tarefas."),
        ["Microsoft.WindowsAlarms"] = ("Relógio", "Alarmes, timer e cronômetro."),
        ["Microsoft.WindowsCalculator"] = ("Calculadora", "Calculadora do Windows."),
        ["Microsoft.WindowsCamera"] = ("Câmera", "App da webcam."),
        ["microsoft.windowscommunicationsapps"] = ("Email e Calendário", "Apps antigos de e-mail e agenda."),
        ["Microsoft.WindowsFeedbackHub"] = ("Hub de Comentários", "Enviar opiniões para a Microsoft."),
        ["Microsoft.WindowsMaps"] = ("Mapas", "Mapas offline."),
        ["Microsoft.WindowsSoundRecorder"] = ("Gravador de Som", "Gravar áudio do microfone."),
        ["Microsoft.WindowsStore"] = ("Microsoft Store", "Loja de apps. Sem ela, fica difícil reinstalar qualquer app daqui."),
        ["Microsoft.DesktopAppInstaller"] = ("Instalador de Apps (winget)", "O PcismoMaster usa o winget para instalar programas."),
        ["Microsoft.WindowsTerminal"] = ("Terminal", "Prompt de comando moderno."),
        ["Microsoft.Xbox.TCUI"] = ("Xbox (janelas de amigos)", "Convites e perfil Xbox dentro dos jogos."),
        ["Microsoft.XboxGameOverlay"] = ("Xbox Game Bar (overlay antigo)", "Sobreposição de jogos."),
        ["Microsoft.XboxGamingOverlay"] = ("Xbox Game Bar", "Win+G: gravação, desempenho e chat nos jogos."),
        ["Microsoft.XboxIdentityProvider"] = ("Login Xbox", "Entrar na conta Xbox nos jogos."),
        ["Microsoft.XboxSpeechToTextOverlay"] = ("Xbox (voz para texto)", "Legendas do chat de voz Xbox."),
        ["Microsoft.YourPhone"] = ("Vincular ao Celular", "Ligar o celular ao PC."),
        ["Microsoft.ZuneMusic"] = ("Reprodutor Multimídia", "Player de música e vídeo."),
        ["Microsoft.ZuneVideo"] = ("Filmes e TV", "Player de vídeo antigo."),
        ["Microsoft.Windows.DevHome"] = ("Dev Home", "Painel para programadores."),
        ["Microsoft.Windows.Teams"] = ("Teams", "Chat e reuniões."),
        ["MSTeams"] = ("Microsoft Teams", "Chat e reuniões."),
        ["MicrosoftTeams"] = ("Microsoft Teams (pessoal)", "Chat do Windows 11."),
        ["MicrosoftCorporationII.QuickAssist"] = ("Assistência Rápida", "Acesso remoto para suporte."),
        ["MicrosoftCorporationII.MicrosoftFamily"] = ("Microsoft Family", "Controle dos pais."),
        ["MicrosoftWindows.Client.WebExperience"] = ("Widgets", "Painel de notícias e clima (Win+W)."),
        ["MicrosoftWindows.CrossDevice"] = ("Experiência entre dispositivos", "Celular conectado no Windows."),
        ["Microsoft.MicrosoftEdge.Stable"] = ("Microsoft Edge", "Navegador. A Microsoft não deixa remover por aqui."),
        ["Microsoft.MicrosoftEdgeDevToolsClient"] = ("Ferramentas de dev do Edge", ""),
        ["Microsoft.Win32WebViewHost"] = ("WebView (apps antigos)", "Mostra páginas web dentro de apps."),
        ["Microsoft.Windows.ContentDeliveryManager"] = ("Sugestões e anúncios do Windows", "Apps sugeridos, tela de bloqueio com fotos e dicas."),
        ["Microsoft.Windows.ParentalControls"] = ("Controle dos pais", "Tela de limites de uso."),
        ["Microsoft.Windows.PeopleExperienceHost"] = ("Pessoas na barra de tarefas", ""),
        ["Microsoft.Windows.SecureAssessmentBrowser"] = ("Fazer um teste", "Navegador trancado para provas."),
        ["Microsoft.Windows.NarratorQuickStart"] = ("Narrador (introdução)", ""),
        ["Microsoft.Windows.XGpuEjectDialog"] = ("Ejetar placa de vídeo externa", ""),
        ["Microsoft.Windows.CapturePicker"] = ("Escolher janela para capturar", "Usado por apps que gravam/compartilham a tela."),
        ["Microsoft.Windows.Apprep.ChxApp"] = ("SmartScreen", "Aviso de downloads e sites perigosos."),
        ["Microsoft.BioEnrollment"] = ("Windows Hello (cadastro)", "Cadastrar rosto ou digital."),
        ["Microsoft.ECApp"] = ("Controle com os olhos", "Acessibilidade."),
        ["Microsoft.LockApp"] = ("Tela de bloqueio", "Sem ela a tela de bloqueio pode ficar preta."),
        ["Microsoft.AAD.BrokerPlugin"] = ("Login de contas Microsoft/escola", "Entrar em contas de trabalho/escola e em apps da Microsoft."),
        ["Microsoft.AccountsControl"] = ("Contas", "Janela de login de contas Microsoft."),
        ["Microsoft.CredDialogHost"] = ("Janela de credenciais", "Pedidos de senha/PIN do Windows."),
        ["Microsoft.Windows.CloudExperienceHost"] = ("Primeira configuração", "Telas de configuração e login do Windows."),
        ["Microsoft.Windows.OOBENetworkCaptivePortal"] = ("Portal de rede (instalação)", ""),
        ["Microsoft.Windows.OOBENetworkConnectionFlow"] = ("Conexão de rede (instalação)", ""),
        ["Microsoft.Windows.AssignedAccessLockApp"] = ("Acesso atribuído", "Modo quiosque."),
        ["Microsoft.Windows.PinningConfirmationDialog"] = ("Confirmar fixar no Iniciar", ""),
        ["Microsoft.Windows.PrintQueueActionCenter"] = ("Fila de impressão", ""),
        ["Windows.PrintDialog"] = ("Janela de impressão", "Sem ela, imprimir de apps modernos falha."),
        ["Windows.CBSPreview"] = ("Prévia de componentes", ""),
        ["NcsiUwpApp"] = ("Aviso de rede", "Detecta login em Wi-Fi público."),
        ["Microsoft.Windows.Search"] = ("Pesquisa do Windows", "Caixa de pesquisa da barra de tarefas."),
        ["MicrosoftWindows.Client.Core"] = ("Núcleo do Windows", "Parte do Windows 11."),
        ["MicrosoftWindows.Client.FileExp"] = ("Explorador de Arquivos (moderno)", "Parte do Windows 11."),
        ["MicrosoftWindows.Client.AIX"] = ("Recursos de IA do Windows", ""),
        ["MicrosoftWindows.Client.Photon"] = ("Componente do Windows", ""),
        ["MicrosoftWindows.Client.OOBE"] = ("Configuração inicial", ""),
        ["Microsoft.ApplicationCompatibilityEnhancements"] = ("Compatibilidade de apps", ""),
        ["Microsoft.WidgetsPlatformRuntime"] = ("Plataforma dos Widgets", ""),
        ["Microsoft.Edge.GameAssist"] = ("Assistente de Jogo do Edge", "Navegador dentro da Game Bar."),
        ["Microsoft.StartExperiencesApp"] = ("Recomendações do Iniciar", ""),
        ["Microsoft.WindowsAppRuntime.CBS"] = ("Runtime de apps do Windows", ""),
        ["Microsoft.Winget.Source"] = ("Catálogo do winget", "Lista de programas que o PcismoMaster usa para instalar apps."),
        ["Microsoft.XboxGameCallableUI"] = ("Xbox (janelas do sistema)", "Convites e perfil Xbox chamados pelos jogos."),
        ["Microsoft.AsyncTextService"] = ("Serviço de mensagens de texto", ""),
        ["MicrosoftWindows.UndockedDevKit"] = ("Componente do Windows", ""),
    };

    // Itens que quebram login, Iniciar ou o próprio Windows se saírem.
    private static readonly string[] DangerApps =
    {
        "Microsoft.AAD.BrokerPlugin", "Microsoft.AccountsControl", "Microsoft.CredDialogHost", "Microsoft.Windows.CloudExperienceHost",
        "Microsoft.LockApp", "Microsoft.Windows.OOBENetwork", "MicrosoftWindows.Client.Core", "MicrosoftWindows.Client.FileExp",
        "MicrosoftWindows.Client.OOBE", "Microsoft.Windows.Search", "Microsoft.UI.Xaml", "Microsoft.VCLibs", "Microsoft.NET.Native",
        "Microsoft.WindowsAppRuntime",
    };

    // Itens que muita gente usa sem perceber.
    private static readonly string[] CareApps =
    {
        "Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "Microsoft.StorePurchaseApp", "Microsoft.GamingApp", "Microsoft.GamingServices",
        "Microsoft.XboxIdentityProvider", "Microsoft.Xbox.TCUI", "Microsoft.Win32WebViewHost", "Windows.PrintDialog", "Microsoft.BioEnrollment",
        "Microsoft.Windows.Apprep.ChxApp", "Microsoft.Windows.CapturePicker", "Microsoft.MicrosoftEdge", "Microsoft.WindowsCalculator",
        "Microsoft.ScreenSketch", "Microsoft.WindowsTerminal", "Microsoft.YourPhone", "MicrosoftWindows.CrossDevice", "Microsoft.Winget.Source",
    };

    private static readonly (string Prefix, string Name, string Desc, ScriptRisk Risk)[] CapNames =
    {
        ("App.StepsRecorder", "Gravador de Passos", "Grava cliques para relatar problemas.", ScriptRisk.Seguro),
        ("App.Support.QuickAssist", "Assistência Rápida (antiga)", "", ScriptRisk.Seguro),
        ("Browser.InternetExplorer", "Internet Explorer", "Navegador antigo.", ScriptRisk.Seguro),
        ("DirectX.Configuration.Database", "Banco de configurações do DirectX", "Ajustes de compatibilidade de jogos.", ScriptRisk.Atencao),
        ("Hello.Face", "Windows Hello · rosto", "Entrar com o rosto.", ScriptRisk.Atencao),
        ("Language.", "Pacote de idioma", "Teclado, voz, escrita à mão ou texto de um idioma. Tirar o do português pode deixar o Windows em inglês em partes.", ScriptRisk.Atencao),
        ("MathRecognizer", "Reconhecedor de matemática", "Escrita de fórmulas à mão.", ScriptRisk.Seguro),
        ("Media.WindowsMediaPlayer", "Windows Media Player (antigo)", "", ScriptRisk.Seguro),
        ("Microsoft.Windows.PowerShell.ISE", "PowerShell ISE", "Editor de scripts antigo.", ScriptRisk.Seguro),
        ("Microsoft.Windows.WordPad", "WordPad", "", ScriptRisk.Seguro),
        ("Microsoft.Windows.MSPaint", "Paint (antigo)", "", ScriptRisk.Seguro),
        ("Microsoft.Wallpapers.Extended", "Papéis de parede extras", "", ScriptRisk.Seguro),
        ("OneCoreUAP.OneSync", "Sincronização de Email/Calendário", "", ScriptRisk.Seguro),
        ("OpenSSH.Client", "Cliente SSH", "Comando ssh no terminal.", ScriptRisk.Seguro),
        ("OpenSSH.Server", "Servidor SSH", "", ScriptRisk.Seguro),
        ("Print.Fax.Scan", "Fax e Scanner do Windows", "", ScriptRisk.Atencao),
        ("Print.Management.Console", "Gerenciamento de Impressão", "", ScriptRisk.Seguro),
        ("Windows.Kernel.LA57", "Memória de 5 níveis (servidores)", "", ScriptRisk.Seguro),
        ("Microsoft.Windows.SnippingTool", "Ferramenta de Captura (antiga)", "", ScriptRisk.Seguro),
        ("Microsoft.Windows.Sense.Client", "Defender for Endpoint (empresas)", "", ScriptRisk.Atencao),
        ("Rsat.", "Ferramentas de administração remota", "", ScriptRisk.Seguro),
        ("VBSCRIPT", "VBScript", "", ScriptRisk.Atencao),
        ("XPS.Viewer", "Visualizador de XPS", "", ScriptRisk.Seguro),
        ("WMIC", "WMIC", "", ScriptRisk.Atencao),
    };

    private static readonly (string Prefix, string Name, string Desc, ScriptRisk Risk)[] FeatureNames =
    {
        ("Printing-PrintToPDFServices", "Imprimir em PDF", "A impressora \"Microsoft Print to PDF\".", ScriptRisk.Atencao),
        ("Printing-XPSServices", "Imprimir em XPS", "", ScriptRisk.Seguro),
        ("Printing-Foundation", "Impressão (base)", "Sem ela, impressoras podem parar.", ScriptRisk.Atencao),
        ("Printing-", "Impressão", "", ScriptRisk.Atencao),
        ("MSRDC-Infrastructure", "Área de Trabalho Remota (base)", "", ScriptRisk.Atencao),
        ("MediaPlayback", "Recursos de mídia", "Windows Media Player antigo e codecs ligados a ele.", ScriptRisk.Atencao),
        ("WindowsMediaPlayer", "Windows Media Player (antigo)", "", ScriptRisk.Seguro),
        ("SmbDirect", "SMB Direct", "Compartilhamento de rede rápido (placas RDMA).", ScriptRisk.Seguro),
        ("SMB1Protocol", "SMB 1.0", "Compartilhamento antigo e inseguro.", ScriptRisk.Seguro),
        ("WorkFolders-Client", "Pastas de Trabalho", "", ScriptRisk.Seguro),
        ("Internet-Explorer", "Internet Explorer", "", ScriptRisk.Seguro),
        ("Microsoft-Hyper-V", "Hyper-V", "Máquinas virtuais. Desligar acelera alguns jogos, mas quebra o WSL 2 e emuladores.", ScriptRisk.Atencao),
        ("VirtualMachinePlatform", "Plataforma de Máquina Virtual", "Usada pelo WSL 2 e pelo Android no Windows.", ScriptRisk.Atencao),
        ("Microsoft-Windows-Subsystem-Linux", "Subsistema Linux (WSL)", "", ScriptRisk.Atencao),
        ("HypervisorPlatform", "Plataforma do Hipervisor", "Emuladores (BlueStacks, LDPlayer) usam.", ScriptRisk.Atencao),
        ("Containers-DisposableClientVM", "Área Restrita do Windows (Sandbox)", "", ScriptRisk.Seguro),
        ("Recall", "Recall", "Capturas de tela com IA.", ScriptRisk.Seguro),
        ("MicrosoftWindowsPowerShellV2", "PowerShell 2.0", "Versão antiga do PowerShell.", ScriptRisk.Seguro),
        ("TelnetClient", "Cliente Telnet", "", ScriptRisk.Seguro),
        ("TFTP", "Cliente TFTP", "", ScriptRisk.Seguro),
        ("Microsoft-RemoteDesktopConnection", "Conexão de Área de Trabalho Remota", "", ScriptRisk.Atencao),
        ("Windows-Identity-Foundation", "Windows Identity Foundation", "", ScriptRisk.Seguro),
        ("SearchEngine-Client", "Pesquisa do Windows", "", ScriptRisk.Perigoso),
    };

    public static readonly (string Id, string Name, string Desc, ScriptRisk Risk, bool CanReinstall)[] Programs =
    {
        ("onedrive", "OneDrive", "Desinstala o OneDrive e as tarefas dele. Seus arquivos na nuvem continuam online.", ScriptRisk.Seguro, true),
        ("rdp", "Conexão de Área de Trabalho Remota", "O mstsc, para acessar outro PC.", ScriptRisk.Seguro, true),
        ("snip", "Ferramenta de Captura antiga", "A versão clássica (Windows 10).", ScriptRisk.Seguro, true),
        ("gameinput", "Microsoft GameInput", "Biblioteca de controles usada por alguns jogos novos; eles podem pedir para instalar de novo.", ScriptRisk.Atencao, false),
        ("brltty", "BRLTTY (Braille)", "Serviço de linha braille do Narrador.", ScriptRisk.Seguro, false),
        ("kb", "Update for x64-based Windows Systems", "Atualização de instalação do Windows 10.", ScriptRisk.Seguro, false),
        ("uhs", "Microsoft Update Health Tools", "Ferramenta que força atualizações do Windows 10.", ScriptRisk.Seguro, false),
    };

    // -------- Montagem da lista a partir do que o PowerShell leu --------

    /// <summary>Script de leitura (só lê): devolve JSON com apps, recursos, recursos antigos e programas presentes.</summary>
    public const string ListScript = """
        $ErrorActionPreference = 'SilentlyContinue'
        $ProgressPreference = 'SilentlyContinue'
        $pk = @(Get-AppxPackage -AllUsers)
        if ($pk.Count -eq 0) { $pk = @(Get-AppxPackage) }
        $apps = @($pk | ForEach-Object { [pscustomobject]@{ n = $_.Name; f = $_.PackageFamilyName; fw = [bool]$_.IsFramework; nr = [bool]$_.NonRemovable; s = "$($_.SignatureKind)" } })
        $caps = @(Get-WindowsCapability -Online | Where-Object State -eq 'Installed' | ForEach-Object Name)
        $feats = @(Get-WindowsOptionalFeature -Online | Where-Object State -eq 'Enabled' | ForEach-Object FeatureName)
        $un = @(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' | ForEach-Object DisplayName)
        $p = @()
        if ((Test-Path "$env:SystemRoot\System32\OneDriveSetup.exe") -or (Test-Path "$env:SystemRoot\SysWOW64\OneDriveSetup.exe") -or (Test-Path "$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe") -or (Test-Path "$env:ProgramFiles\Microsoft OneDrive")) { $p += 'onedrive' }
        if (Test-Path "$env:SystemRoot\System32\mstsc.exe") { $p += 'rdp' }
        if (Test-Path "$env:SystemRoot\System32\SnippingTool.exe") { $p += 'snip' }
        if ($un -like '*Microsoft GameInput*') { $p += 'gameinput' }
        if (Test-Path "$env:SystemRoot\brltty") { $p += 'brltty' }
        if ($un -like '*Update for x64-based Windows Systems*') { $p += 'kb' }
        if ($un -like '*Microsoft Update Health Tools*') { $p += 'uhs' }
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        [pscustomobject]@{ apps = $apps; caps = $caps; feats = $feats; progs = $p } | ConvertTo-Json -Depth 3 -Compress
        """;

    private sealed record RawApp(string n, string? f, bool fw, bool nr, string? s);
    private sealed record Raw(RawApp[]? apps, string[]? caps, string[]? feats, string[]? progs);

    public static List<BloatItem> Parse(string json)
    {
        var raw = JsonSerializer.Deserialize<Raw>(json) ?? new Raw(null, null, null, null);
        var list = new List<BloatItem>();
        foreach (var a in (raw.apps ?? Array.Empty<RawApp>()).DistinctBy(a => a.n, StringComparer.OrdinalIgnoreCase)) list.Add(App(a.n, a.f, a.fw, a.nr, a.s));
        foreach (var c in (raw.caps ?? Array.Empty<string>()).Distinct()) list.Add(Capability(c));
        foreach (var f in (raw.feats ?? Array.Empty<string>()).Distinct()) list.Add(Feature(f));
        foreach (var p in Programs.Where(p => raw.progs?.Contains(p.Id) == true))
            list.Add(new BloatItem(BloatKind.Programa, p.Id, p.Name, p.Desc, p.Risk, null));
        return list.OrderBy(i => i.Kind).ThenBy(i => i.Essential != null).ThenBy(i => i.Risk).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static BloatItem App(string name, string? family, bool framework, bool nonRemovable, string? signature)
    {
        string? keep = Keep(KeepApps, name) ?? (nonRemovable ? "o Windows não deixa remover" : null);
        var (title, desc) = AppNames.TryGetValue(name, out var n) ? n : (Pretty(name), "");
        bool guid = Guid.TryParse(name, out _);
        var ai = Regex.Match(name, @"^MicrosoftWindows\.\d+\.(\w+)$");
        bool thirdParty = !guid && !Regex.IsMatch(name, @"^(Microsoft|MicrosoftWindows|MicrosoftCorporationII|Windows|Clipchamp|NVIDIACorp|AppUp|RealtekSemiconductorCorp|AdvancedMicroDevicesInc|Intel)\b", RegexOptions.IgnoreCase)
                          && !string.Equals(signature, "System", StringComparison.OrdinalIgnoreCase);
        if (guid) (title, desc) = ("Componente interno do Windows", "Janelas do sistema sem nome público (abrir arquivo, escolher pasta…).");
        else if (ai.Success || name.StartsWith("MicrosoftWindows.Client.CoreAI", StringComparison.OrdinalIgnoreCase))
            (title, desc) = ($"Componente de IA do Windows ({(ai.Success ? ai.Groups[1].Value : "CoreAI")})", "Recursos de IA do Windows 11 (Copilot+, legendas, pesquisa).");
        else if (thirdParty) desc = "Programa de terceiros que você instalou (vem empacotado como app). O script do autor remove junto: confira se não é algo que você usa.";
        var risk = guid || DangerApps.Any(d => name.StartsWith(d, StringComparison.OrdinalIgnoreCase)) ? ScriptRisk.Perigoso
            : framework || thirdParty || CareApps.Any(c => name.StartsWith(c, StringComparison.OrdinalIgnoreCase)) || string.Equals(signature, "System", StringComparison.OrdinalIgnoreCase) ? ScriptRisk.Atencao
            : ScriptRisk.Seguro;
        if (framework && desc.Length == 0) desc = "Biblioteca usada por outros apps: se algum depender dela, o Windows não remove.";
        else if (risk == ScriptRisk.Atencao && desc.Length == 0) desc = "Parte do Windows que vem instalada.";
        return new BloatItem(framework ? BloatKind.Biblioteca : BloatKind.App, name, title, desc, risk, keep, family);
    }

    public static BloatItem Capability(string name)
    {
        string shortName = name.Split('~')[0];
        var hit = CapNames.FirstOrDefault(c => shortName.StartsWith(c.Prefix, StringComparison.OrdinalIgnoreCase));
        string title = hit.Name ?? Pretty(shortName);
        if (hit.Prefix == "Language.") title = $"Idioma · {LanguagePart(name)}";
        return new BloatItem(BloatKind.Recurso, name, title, hit.Desc ?? "", hit.Name == null ? ScriptRisk.Seguro : hit.Risk, Keep(KeepCaps, name));
    }

    public static BloatItem Feature(string name)
    {
        var hit = FeatureNames.FirstOrDefault(c => name.StartsWith(c.Prefix, StringComparison.OrdinalIgnoreCase));
        return new BloatItem(BloatKind.RecursoAntigo, name, hit.Name ?? Pretty(name), hit.Desc ?? "", hit.Name == null ? ScriptRisk.Atencao : hit.Risk, Keep(KeepFeatures, name));
    }

    private static string LanguagePart(string n)
    {
        // Language.Speech~~~pt-BR → "voz (pt-BR)"
        var parts = n.Split('.');
        string kind = parts.Length > 1 ? parts[1].Split('~')[0] switch
        {
            "Basic" => "teclado e correção", "Handwriting" => "escrita à mão", "OCR" => "reconhecer texto", "Speech" => "voz",
            "TextToSpeech" => "leitura em voz alta", "Fonts" => "fontes", _ => parts[1].Split('~')[0],
        } : n;
        var loc = Regex.Match(n, @"[a-z]{2,3}-[A-Z][A-Za-z]{1,3}").Value;
        return loc.Length > 0 ? $"{kind} ({loc})" : kind;
    }

    /// <summary>"Microsoft.WindowsFeedbackHub" → "Windows Feedback Hub".</summary>
    public static string Pretty(string name)
    {
        string s = Regex.Replace(name, @"^(Microsoft(Windows|Corporation(II)?)?|Windows)\.", "");
        s = s.Replace('.', ' ').Replace('-', ' ').Replace('_', ' ');
        s = Regex.Replace(s, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    // -------- Comandos (os mesmos do script, um item por vez) --------

    private static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    public static string RemoveCommand(BloatItem i) => i.Kind switch
    {
        BloatKind.App or BloatKind.Biblioteca =>
            $"Get-AppxPackage -AllUsers -Name {Q(i.Id)} | Remove-AppxPackage -AllUsers -ErrorAction Stop",
        BloatKind.Recurso => $"Remove-WindowsCapability -Online -Name {Q(i.Id)} -ErrorAction Stop | Out-Null",
        BloatKind.RecursoAntigo => $"Disable-WindowsOptionalFeature -Online -FeatureName {Q(i.Id)} -NoRestart -WarningAction SilentlyContinue -ErrorAction Stop | Out-Null",
        _ => ProgramRemove(i.Id),
    };

    /// <summary>Comando para voltar; null quando a volta é pela Store (apps) ou por outro script.</summary>
    public static string? RestoreCommand(BloatItem i) => i.Kind switch
    {
        BloatKind.Recurso => $"Add-WindowsCapability -Online -Name {Q(i.Id)} -ErrorAction Stop | Out-Null",
        BloatKind.RecursoAntigo => $"Enable-WindowsOptionalFeature -Online -FeatureName {Q(i.Id)} -All -NoRestart -WarningAction SilentlyContinue -ErrorAction Stop | Out-Null",
        _ => null,
    };

    public static string ProgramRemove(string id) => id switch
    {
        "onedrive" => """
            Stop-Process -Force -Name OneDrive -ErrorAction SilentlyContinue | Out-Null
            cmd /c "$env:SystemRoot\System32\OneDriveSetup.exe -uninstall >nul 2>&1"
            Get-ChildItem -Path "C:\Program Files*\Microsoft OneDrive", "$env:LOCALAPPDATA\Microsoft\OneDrive" -Filter "OneDriveSetup.exe" -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object { Start-Process -Wait $_.FullName -ArgumentList "/uninstall /allusers" -WindowStyle Hidden -ErrorAction SilentlyContinue }
            cmd /c "$env:SystemRoot\SysWOW64\OneDriveSetup.exe -uninstall >nul 2>&1"
            Get-ScheduledTask | Where-Object {$_.Taskname -match 'OneDrive'} | Unregister-ScheduledTask -Confirm:$false
            """,
        "rdp" => """
            Start-Process "mstsc" -ArgumentList "/Uninstall" -ErrorAction SilentlyContinue
            for ($t = 0; $t -lt 100; $t++) { $m = Get-Process -Name mstsc -ErrorAction SilentlyContinue; if (!$m) { Start-Sleep -Milliseconds 100; continue }; if ($m.MainWindowHandle -ne 0) { break }; Start-Sleep -Milliseconds 100 }
            Stop-Process -Force -Name mstsc -ErrorAction SilentlyContinue
            """,
        "snip" => """
            Start-Process "$env:SystemRoot\System32\SnippingTool.exe" -ArgumentList "/Uninstall" -ErrorAction SilentlyContinue
            for ($t = 0; $t -lt 100; $t++) { $m = Get-Process -Name SnippingTool -ErrorAction SilentlyContinue; if (!$m) { Start-Sleep -Milliseconds 100; continue }; if ($m.MainWindowHandle -ne 0) { break }; Start-Sleep -Milliseconds 100 }
            Stop-Process -Force -Name SnippingTool -ErrorAction SilentlyContinue
            """,
        "gameinput" => MsiUninstall("*Microsoft GameInput*"),
        "kb" => MsiUninstall("*Update for x64-based Windows Systems*"),
        "uhs" => MsiUninstall("*Microsoft Update Health Tools*") + "\r\ncmd /c \"reg delete `\"HKLM\\SYSTEM\\ControlSet001\\Services\\uhssvc`\" /f >nul 2>&1\"\r\nUnregister-ScheduledTask -TaskName PLUGScheduler -Confirm:$false -ErrorAction SilentlyContinue | Out-Null",
        "brltty" => """
            cmd /c "sc stop `"brlapi`" >nul 2>&1"
            cmd /c "sc delete `"brlapi`" >nul 2>&1"
            cmd /c "takeown /f `"$env:SystemRoot\brltty`" /r /d y >nul 2>&1"
            cmd /c "icacls `"$env:SystemRoot\brltty`" /grant *S-1-5-32-544:F /t >nul 2>&1"
            Remove-Item "$env:SystemRoot\brltty" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
            """,
        _ => throw new ArgumentException("programa desconhecido: " + id),
    };

    /// <summary>Reinstalar um programa removido (OneDrive pelo instalador do Windows; mstsc e Ferramenta de Captura pelos
    /// instaladores que o Ultimate, MIT, publica). null = sem volta automática.</summary>
    public static string? ProgramReinstall(string id) => id switch
    {
        "onedrive" => """
            Write-Output "Reinstalando o OneDrive..."
            if (Test-Path "$env:SystemRoot\System32\OneDriveSetup.exe") { Start-Process -Wait "$env:SystemRoot\System32\OneDriveSetup.exe" }
            elseif (Test-Path "$env:SystemRoot\SysWOW64\OneDriveSetup.exe") { Start-Process -Wait "$env:SystemRoot\SysWOW64\OneDriveSetup.exe" }
            else { winget install --id Microsoft.OneDrive -e --accept-package-agreements --accept-source-agreements --silent }
            """,
        "rdp" => Download("remotedesktopconnection.exe", "Conexão de Área de Trabalho Remota"),
        "snip" => Download("snippingtool.exe", "Ferramenta de Captura") +
            "\r\nGet-AppxPackage -AllUsers *Microsoft.ScreenSketch* | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" -ErrorAction SilentlyContinue }",
        _ => null,
    };

    private static string Download(string file, string name) => $$"""
        Write-Output "Baixando {{name}}..."
        $f = "$env:SystemRoot\Temp\{{file}}"
        Invoke-WebRequest "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/{{file}}" -OutFile $f -UseBasicParsing -ErrorAction Stop
        Start-Process -Wait $f
        Remove-Item $f -Force -ErrorAction SilentlyContinue
        """;

    private static string MsiUninstall(string like) => $$"""
        $u = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '{{like}}' }
        foreach ($x in @($u)) { Start-Process "msiexec.exe" -ArgumentList "/x $($x.PSChildName) /qn /norestart" -Wait -NoNewWindow }
        """;

    /// <summary>Um script só para vários itens: cada um avisa o que está fazendo e um erro não para os outros.</summary>
    public static string BatchScript(IEnumerable<BloatItem> items, bool restore)
    {
        var sb = new StringBuilder("$ProgressPreference = 'SilentlyContinue'\r\n$falhas = 0\r\n");
        foreach (var i in items)
        {
            string? cmd = restore ? RestoreCommand(i) : RemoveCommand(i);
            if (cmd == null) continue;
            sb.Append("Write-Host ").Append(Q((restore ? "Voltando: " : "Removendo: ") + i.Name)).Append("\r\n");
            sb.Append("try {\r\n").Append(cmd).Append("\r\nWrite-Host '  ok'\r\n} catch { $falhas++; Write-Host ('  não deu: ' + $_.Exception.Message) }\r\n");
        }
        sb.Append("if ($falhas -gt 0) { Write-Host \"$falhas item(ns) não saíram (o Windows pode estar usando ou não deixa).\" }\r\n");
        return sb.ToString();
    }

    public static string KindTitle(BloatKind k) => k switch
    {
        BloatKind.App => "APPS DA STORE E DO WINDOWS",
        BloatKind.Biblioteca => "BIBLIOTECAS (usadas por outros apps)",
        BloatKind.Recurso => "RECURSOS OPCIONAIS",
        BloatKind.RecursoAntigo => "RECURSOS DO WINDOWS (painel antigo)",
        _ => "PROGRAMAS QUE VÊM COM O WINDOWS",
    };
}
