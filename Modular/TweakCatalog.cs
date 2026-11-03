using System.IO;
namespace ResetExplode.Modular;

/// <summary>
/// Ajustes de registro das sub-abas Windows, Hardware e Avançado do TWEAKING (pastas "Windows", "Hardware" e "Advanced"
/// do Ultimate, MIT). Só o que tem volta exata pelo backup; o que depende do PC (placas, discos, dispositivos) é montado
/// na hora pela tela. Os .reg grandes dos scripts ficam em Tweaks\*.reg e são lidos pelo RegFile.
/// </summary>
public static class TweakCatalog
{
    private static Setting Lm(string path, string name, int value) => new("HKLM", path, name, value);
    private static Setting Cu(string path, string name, int value) => new("HKCU", path, name, value);
    private static Setting LmText(string path, string name, string text) => new("HKLM", path, name, 0, text);
    private static Setting CuText(string path, string name, string text) => new("HKCU", path, name, 0, text);

    private const string Classes = @"SOFTWARE\Classes\";
    private const string Blocked = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
    private const string Power = @"SYSTEM\CurrentControlSet\Control\Power";
    public const string BlackImage = @"C:\Windows\Black.jpg";

    // ---------------- Windows ----------------

    public static readonly SetupTweak[] Windows =
    {
        new("tw-win-context-menu", "Menu de contexto limpo",
            "Volta o menu do botão direito clássico do Windows 10 e tira Personalizar pasta, Fixar no Acesso rápido, Favoritos, Solucionar compatibilidade, Abrir no Terminal, Verificar com o Defender, Conceder acesso, Incluir na biblioteca, Compartilhar, Versões anteriores e Enviar para.",
            false, true, "Windows/4 Context Menu.ps1",
            new[] { CuText(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", ""),
                    Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoCustomizeThisFolder", 1),
                    LmText(Blocked, "{9F156763-7844-4DC4-B2B1-901F640F5155}", ""),
                    LmText(Blocked, "{09A47860-11B0-4DA5-AFA5-26D86198A780}", ""),
                    LmText(Blocked, "{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}", ""),
                    Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "NoPreviousVersionsPage", 1) },
            new[] { @"HKLM\" + Classes + @"Folder\shell\pintohome", @"HKLM\" + Classes + @"*\shell\pintohomefile",
                    @"HKLM\" + Classes + @"exefile\shellex\ContextMenuHandlers\Compatibility",
                    @"HKLM\" + Classes + @"Folder\ShellEx\ContextMenuHandlers\Library Location",
                    @"HKLM\" + Classes + @"AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing",
                    @"HKLM\" + Classes + @"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo",
                    @"HKLM\" + Classes + @"UserLibraryFolder\shellex\ContextMenuHandlers\SendTo" }),
        new("tw-win-widgets", "Widgets desligados",
            "Tira os Widgets (notícias e clima) da barra de tarefas e impede que rodem.",
            false, true, "Windows/8 Widgets.ps1",
            new[] { Lm(@"SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests", "value", 0),
                    Lm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0) }),
        new("tw-win-copilot", "Copilot desligado (política)",
            "Desliga o Copilot pela política do Windows. O app Copilot não é desinstalado (use Desinstalar Apps).",
            false, true, "Windows/9 Copilot.ps1",
            new[] { Cu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                    Lm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1) }),
        new("tw-win-lockscreen-black", "Tela de bloqueio e papel de parede pretos",
            "Usa uma imagem preta (C:\\Windows\\Black.jpg) na tela de bloqueio/saída e no papel de parede. Desfazer volta o papel de parede anterior.",
            false, false, "Windows/6 Signout Lockscreen Wallpaper Black.ps1",
            new[] { LmText(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", "LockScreenImagePath", BlackImage),
                    Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", "LockScreenImageStatus", 1),
                    CuText(@"Control Panel\Desktop", "Wallpaper", BlackImage) }),
        new("tw-win-power", "Sem hibernação, inicialização rápida e limitação de energia",
            "Desliga a hibernação e a inicialização rápida (o desligar passa a ser completo), tira Bloquear/Suspender do menu de energia e desliga a limitação de energia dos programas em segundo plano.",
            false, true, "Windows/29 Power Plan.ps1",
            new[] { Lm(Power, "HibernateEnabled", 0), Lm(Power, "HibernateEnabledDefault", 0),
                    Lm(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings", "ShowLockOption", 0),
                    Lm(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings", "ShowSleepOption", 0),
                    Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0),
                    Lm(Power + @"\PowerThrottling", "PowerThrottlingOff", 1) }),
        new("tw-win-timer", "Resolução do timer global",
            "Faz o pedido de timer de alta resolução de um programa valer para o sistema todo (como no Windows 10). O serviço de timer do script não é instalado.",
            false, true, "Windows/30 Timer Resolution.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1) }),
        new("tw-win-uac", "UAC desligado",
            "Programas deixam de pedir permissão de administrador. Qualquer programa passa a rodar com acesso total e alguns apps da Microsoft Store podem parar de abrir. Vale depois de reiniciar.",
            true, false, "Windows/31 UAC.ps1",
            new[] { Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0) }),
    };

    /// <summary>Ajustes da pasta Windows que vêm de .reg (nome do recurso em Tweaks\, id, título, descrição, avançado, padrão).</summary>
    public static readonly (string File, string Id, string Title, string Description, bool Advanced, bool DefaultOn, string Source)[] WindowsReg =
    {
        ("windows-control-panel.reg", "tw-win-control-panel", "Configurações do Windows otimizadas",
            "As centenas de ajustes do Painel de Controle e das Configurações do script: privacidade e permissões de apps, notificações, sugestões e anúncios, Explorador, mouse sem aceleração, som, sincronização, telemetria e mais. Chaves protegidas pelo Windows ficam de fora.",
            false, true, "Windows/22 Control Panel Settings.ps1"),
        ("windows-theme-black.reg", "tw-win-theme-black", "Tema preto",
            "Modo escuro do Windows e dos apps, sem transparência e com cores de destaque pretas.",
            false, false, "Windows/5 Theme Black.ps1"),
        ("windows-taskbar-clean.reg", "tw-win-taskbar", "Barra de tarefas e Menu Iniciar limpos",
            "Tira pesquisa, Visão de Tarefas, Widgets, Chat e recomendações da barra e do Iniciar. Os ícones fixados não são mexidos.",
            false, true, "Windows/1 Start Menu Taskbar.ps1"),
        ("windows-start-layout.reg", "tw-win-start-layout", "Menu Iniciar novo (25H2)",
            "Ativa o layout novo do Menu Iniciar do Windows 11 25H2.",
            false, false, "Windows/2 Start Menu Layout.ps1"),
        ("windows-gamebar-off.reg", "tw-win-gamebar", "Game Bar desligada",
            "Desliga a Xbox Game Bar e a gravação em segundo plano e evita o aviso \"ms-gamingoverlay\" ao apertar Win+G. Os apps da Xbox não são desinstalados.",
            false, true, "Windows/19 Gamebar.ps1"),
    };

    /// <summary>Plano de energia do script (cópia do Desempenho Máximo com estes valores, em tomada e bateria). Fora: os de bateria crítica.</summary>
    public const string PowerPlanGuid = "99999999-9999-9999-9999-999999999999";
    public const string UltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public static readonly (string Sub, string Setting, string Value)[] PowerPlan =
    {
        ("0012ee47-9041-4b5d-9b77-535fba8b1442", "6738e2c4-e8a5-4a42-b16a-e040e769756e", "0"),   // desligar o disco: nunca
        ("0d7dbae2-4294-402a-ba8e-26777e8488cd", "309dce9b-bef4-4119-9921-a851fb12f0f4", "1"),   // apresentação de slides pausada
        ("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1", "12bbebe6-58d6-4636-95bb-3217ef867c1a", "0"),   // Wi-Fi: desempenho máximo
        ("238c9fa8-0aad-41ed-83f4-97be242c8f20", "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", "0"),   // suspender: nunca
        ("238c9fa8-0aad-41ed-83f4-97be242c8f20", "94ac6d29-73ce-41a6-809f-6363ba21b47e", "0"),   // suspensão híbrida: não
        ("238c9fa8-0aad-41ed-83f4-97be242c8f20", "9d7815a6-7ee4-497e-8888-515a05f02364", "0"),   // hibernar: nunca
        ("238c9fa8-0aad-41ed-83f4-97be242c8f20", "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d", "0"),   // timers de ativação: não
        ("2a737441-1930-4402-8d77-b2bebba308a3", "0853a681-27c8-4100-a2fd-82013e970683", "0"),   // suspensão do hub USB: 0
        ("2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", "0"),   // suspensão seletiva USB: não
        ("2a737441-1930-4402-8d77-b2bebba308a3", "d4e98f31-5ffe-4ce1-be31-1b38b384c009", "0"),   // energia do link USB 3: não
        ("44f3beca-a7c0-460e-9df2-bb8b99e0cba6", "3619c3f2-afb2-4afc-b0e9-e7fef372de36", "2"),   // Intel Graphics: desempenho máximo
        ("4f971e89-eebd-4455-a8de-9e59040e7347", "a7066653-8d6c-40a8-910e-a1f54b84c7e5", "2"),   // botão de energia do Iniciar: desligar
        ("501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", "0"),   // PCI Express ASPM: não
        ("54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", "100"), // núcleos sem estacionar (mín.)
        ("54533251-82be-4824-96c1-47b60b740d00", "893dee8e-2bef-41e0-89c6-b55d0929964c", "100"), // processador mínimo 100%
        ("54533251-82be-4824-96c1-47b60b740d00", "94d3a615-a899-4ac5-ae2b-e4d8f634367f", "1"),   // resfriamento ativo
        ("54533251-82be-4824-96c1-47b60b740d00", "bc5038f7-23e0-4960-96da-33abaf5935ec", "100"), // processador máximo 100%
        ("54533251-82be-4824-96c1-47b60b740d00", "ea062031-0e34-4ff1-9b6d-eb1059334028", "100"), // núcleos sem estacionar (máx.)
        ("7516b95f-f776-4464-8c53-06167f40cc99", "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", "600"), // desligar a tela: 10 min
        ("7516b95f-f776-4464-8c53-06167f40cc99", "aded5e82-b909-4619-9949-f5d71dac0bcb", "100"), // brilho 100%
        ("7516b95f-f776-4464-8c53-06167f40cc99", "f1fbfde2-a960-4165-9f88-50667911ce96", "100"), // brilho esmaecido 100%
        ("7516b95f-f776-4464-8c53-06167f40cc99", "fbd9aa66-9553-4097-ba44-ed6e9d65eab8", "0"),   // brilho adaptável: não
        ("9596fb26-9850-41fd-ac3e-f7c3c00afd4b", "10778347-1370-4ee0-8bbd-33bdacaade49", "1"),   // vídeo: desempenho
        ("9596fb26-9850-41fd-ac3e-f7c3c00afd4b", "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4", "0"),   // vídeo: qualidade
        ("c763b4ec-0e50-4b6b-9bed-2b92a6ee884e", "7ec1751b-60ed-4588-afb5-9819d3d77d90", "3"),
        ("de830923-a562-41af-a086-e3a2c6bad2da", "13d09884-f74e-474a-a852-b6bde8ad03a8", "100"), // economia de bateria: brilho 100%
        ("de830923-a562-41af-a086-e3a2c6bad2da", "e69653ca-cf7f-4f05-aa73-cb833fa90ad4", "0"),   // economia de bateria: nunca ligar
        ("e276e160-7cb0-43c6-b20b-73f5dce39954", "a1662ab2-9d34-4e53-ba8b-2639b9e20857", "3"),   // gráficos alternáveis: desempenho
        ("f693fb01-e858-4f00-b20f-f30e12ac06d6", "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6", "1"),
    };

    // ---------------- Hardware ----------------

    public static readonly SetupTweak[] Hardware =
    {
        new("tw-hw-polling-cap", "Sem limite de 125 Hz do mouse em segundo plano",
            "Programas em segundo plano recebem o mouse na taxa real (1000 Hz ou mais), em vez de 125 Hz.",
            false, true, "Hardware/2 Background Polling Rate Cap.ps1",
            new[] { Cu(@"Control Panel\Mouse", "RawMouseThrottleEnabled", 0) }),
    };

    private static Setting S(string path, string name, string text) => CuText(path, name, text);
    private static Setting D(string path, string name, int value) => Cu(path, name, value);
    private static Setting B(string path, string name, string hex) => new("HKCU", path, name, 0, Hex: hex);

    /// <summary>"Escala maior sem aceleração": escala do Windows e curva do mouse (MarkC) que mantém o movimento 1:1 em cada escala.</summary>
    public static readonly SortedDictionary<int, Setting[]> Scaling = new()
    {
        [100] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "0"), S("Control Panel\\Mouse", "MouseThreshold1", "0"), S("Control Panel\\Mouse", "MouseThreshold2", "0"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "0000000000000000C0CC0C0000000000809919000000000040662600000000000033330000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "0000000000000000000038000000000000007000000000000000a800000000000000e00000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 96), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 0) },
        [125] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "00000000000000000000100000000000000020000000000000003000000000000000400000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 120), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [150] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "0000000000000000303313000000000060662600000000009099390000000000C0CC4C0000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "0000000000000000000038000000000000007000000000000000A800000000000000E00000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 144), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [175] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "00000000000000006066160000000000C0CC2C000000000020334300000000008099590000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 168), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        // 200% e 350%: no script a curva X vem deslocada meio byte (erro de digitação); aqui vai a curva MarkC certa (passo × 1..4).
        [200] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "000000000000000090991900000000002033330000000000B0CC4C00000000004066660000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 192), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [225] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "0000000000000000C0CC1C0000000000809939000000000040665600000000000033730000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "0000000000000000000038000000000000007000000000000000A800000000000000E00000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 216), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [250] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "00000000000000000000200000000000000040000000000000006000000000000000800000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 240), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [300] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "00000000000000006066260000000000C0CC4C000000000020337300000000008099990000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 288), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
        [350] = new[] { S("Control Panel\\Mouse", "MouseSensitivity", "10"), S("Control Panel\\Mouse", "MouseSpeed", "1"), S("Control Panel\\Mouse", "MouseThreshold1", "6"), S("Control Panel\\Mouse", "MouseThreshold2", "10"), B("Control Panel\\Mouse", "SmoothMouseXCurve", "0000000000000000C0CC2C0000000000809959000000000040668600000000000033B30000000000"), B("Control Panel\\Mouse", "SmoothMouseYCurve", "00000000000000000000380000000000000070000000000000A800000000000000E0000000000000"), D("Control Panel\\Desktop", "Win8DpiScaling", 1), D("Control Panel\\Desktop", "LogPixels", 336), D("Control Panel\\Desktop", "EnablePerProcessSystemDPI", 1) },
    };

    // ---------------- Avançado ----------------

    private const string GameConfig = @"System\GameConfigStore";
    private const string Overrides = @"SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides";

    public static readonly SetupTweak[] Advanced =
    {
        new("tw-adv-nvme", "Driver NVMe mais rápido",
            "Liga o driver NVMe nativo novo do Windows (menos latência no SSD). Recomendado pelo script, mas é recurso escondido: se o PC não iniciar, entre no modo de segurança (já fica liberado) e desfaça.",
            true, false, "Advanced/19 NVME Faster Driver.ps1",
            new[] { Lm(Overrides, "735209102", 1), Lm(Overrides, "3244671118", 1), Lm(Overrides, "1853569164", 1), Lm(Overrides, "156965516", 1),
                    LmText(@"SYSTEM\CurrentControlSet\Control\SafeBoot\Network\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}", "", "Storage disks"),
                    LmText(@"SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}", "", "Storage disks") }),
        new("tw-adv-mpo", "Sem Multiplane Overlay e otimizações para jogos em janela",
            "Para cintilação/tela piscando com duas telas ou vídeo aberto. Pode aumentar a latência em jogos em janela sem borda.",
            true, false, "Advanced/11 Mpo.ps1",
            new[] { Lm(@"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5),
                    CuText(@"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", "VRROptimizeEnable=0;SwapEffectUpgradeEnable=0;") }),
        new("tw-adv-fse", "Tela cheia exclusiva (Hardware Legacy Flip)",
            "Jogos em tela cheia voltam ao modo exclusivo em vez das otimizações de tela cheia. Alt+Tab fica mais lento.",
            true, false, "Advanced/12 Hardware Legacy Flip.ps1",
            new[] { Cu(GameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1), Cu(GameConfig, "GameDVR_FSEBehaviorMode", 2),
                    Cu(GameConfig, "GameDVR_FSEBehavior", 2), Cu(GameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1) }),
        new("tw-adv-composed-flip", "Hardware Composed Independent Flip",
            "Força o modo de apresentação imediato no agendador da GPU. Teste: se piorar, desfaça.",
            true, false, "Advanced/13 Hardware Composed Independent Flip.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler", "ForceFlipTrueImmediateMode", 1) }),
        new("tw-adv-search", "Pesquisa do Windows desligada",
            "Desliga a pesquisa do Iniciar e o serviço de indexação. A busca do Iniciar e do Explorador deixa de achar arquivos (use o Finder do app). As pastas do sistema não são movidas.",
            true, false, "Advanced/18 Start Search Shell Mobsync.ps1",
            new[] { Lm(@"SOFTWARE\Microsoft\PolicyManager\default\Search\DisableSearch", "value", 1),
                    Lm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableSearch", 1),
                    Lm(@"SYSTEM\CurrentControlSet\Services\WSearch", "Start", 4) }),
        new("tw-adv-keyboard", "Atalhos do teclado desligados",
            "Desliga a tecla Windows, as teclas de mídia/atalhos (serviço HID) e os atalhos Win+… Bom para não minimizar o jogo sem querer; Ctrl+Alt+Del continua.",
            true, false, "Advanced/16 Keyboard Shortcuts.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Services\hidserv", "Start", 4),
                    Cu(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoWinKeys", 1),
                    Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisabledHotkeys", 1),
                    new Setting("HKLM", @"SYSTEM\CurrentControlSet\Control\Keyboard Layout", "Scancode Map", 0, Hex: "00000000000000000700000000005be000005ce000003800000038e00000010001000d0000000000") }),
        new("tw-adv-spectre", "Proteções Spectre/Meltdown desligadas",
            "Pode dar alguns % a mais em CPUs antigas, mas deixa o PC vulnerável a ataques por esses defeitos do processador. Vale depois de reiniciar.",
            true, false, "Advanced/3 Spectre Meltdown.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 3),
                    Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 3) }),
        new("tw-adv-firewall", "Firewall do Windows desligado",
            "Desliga o firewall nas redes públicas e privadas. Só use atrás de um roteador e se souber o que está fazendo.",
            true, false, "Advanced/2 Firewall.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "EnableFirewall", 0),
                    Lm(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "EnableFirewall", 0) }),
        new("tw-adv-download-warning", "Sem aviso de segurança ao abrir downloads",
            "Arquivos baixados abrem sem a janela \"Deseja executar este arquivo?\". Fica mais fácil abrir algo perigoso sem perceber.",
            true, false, "Advanced/5 File Download Security Warning.ps1",
            new[] { Lm(@"SOFTWARE\Policies\Microsoft\Internet Explorer\Security", "DisableSecuritySettingsCheck", 1),
                    Cu(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3", "1806", 0),
                    Lm(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3", "1806", 0) }),
        new("tw-adv-whql", "Drivers sem assinatura WHQL com Secure Boot",
            "Deixa instalar drivers sem a assinatura da Microsoft mesmo com o Secure Boot ligado. Só para drivers modificados que você confia.",
            true, false, "Advanced/15 Driver Whql Secure Boot Bypass.ps1",
            new[] { Lm(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "WHQLSettings", 1) }),
    };

    /// <summary>NVIDIA "rBAR - Enable" (Resizable BAR forçado em todos os jogos) no perfil global.</summary>
    public const uint RebarEnable = 983226;

    // ---------------- .reg embutidos e sobreposição ----------------

    /// <summary>Lê um .reg embutido (Tweaks\arquivo.reg).</summary>
    public static RegContent ReadReg(string file)
    {
        using var stream = typeof(TweakCatalog).Assembly.GetManifestResourceStream("Tweaks/" + file)
            ?? throw new FileNotFoundException("Recurso não encontrado: Tweaks/" + file);
        using var reader = new StreamReader(stream);
        return RegFile.Parse(reader.ReadToEnd());
    }

    /// <summary>Monta os ajustes .reg da sub-aba Windows (sem as chaves protegidas, se canWrite for dado).</summary>
    public static IEnumerable<SetupTweak> WindowsRegTweaks(Func<Setting, bool>? canWrite = null) => WindowsReg.Select(r =>
    {
        var reg = ReadReg(r.File);
        return new SetupTweak(r.Id, r.Title, r.Description, r.Advanced, r.DefaultOn, r.Source,
            canWrite == null ? reg.Settings : reg.Settings.Where(canWrite).ToArray(), reg.DeleteKeys.Length > 0 ? reg.DeleteKeys : null);
    });

    /// <summary>Valores da escala da tela (LogPixels etc.), que o .reg do Painel de Controle força em 100%.</summary>
    public static bool IsDisplayScale(Setting s) => s.Hive == "HKCU" && s.Path.Equals(@"Control Panel\Desktop", StringComparison.OrdinalIgnoreCase)
        && s.Name is "LogPixels" or "Win8DpiScaling" or "EnablePerProcessSystemDPI";

    private static (string, string, string) Key(Setting s) => (s.Hive, s.Path.ToLowerInvariant(), s.Name.ToLowerInvariant());

    /// <summary>
    /// Um mesmo valor só pode estar num ajuste (senão desfazer fora de ordem deixa o valor errado): o primeiro da lista
    /// fica com ele e os seguintes perdem. "taken" são valores já usados por outras listas (ex.: a sub-aba Iniciar).
    /// </summary>
    public static SetupTweak[] Dedupe(IEnumerable<SetupTweak> tweaks, IEnumerable<Setting>? taken = null)
    {
        var used = new HashSet<(string, string, string)>((taken ?? Enumerable.Empty<Setting>()).Select(Key));
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SetupTweak>();
        foreach (var t in tweaks)
        {
            var own = t.Settings.Where(s => used.Add(Key(s))).ToArray();
            var del = t.DeleteKeys?.Where(keys.Add).ToArray();
            result.Add(t with { Settings = own, DeleteKeys = del is { Length: > 0 } ? del : null });
        }
        return result.ToArray();
    }
}
