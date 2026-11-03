namespace ResetExplode.Modular;

/// <summary>Um ajuste da aba TWEAKING: interruptor na tela, valores de registro com backup e desfazer.</summary>
public sealed record SetupTweak(string Id, string Title, string Description, bool Advanced, bool DefaultOn, string Source, Setting[] Settings)
{
    public Module Module => new(Id, Title, "Tweaking", Description, Source, Settings);
}

/// <summary>
/// "3 Setup" do Ultimate (MIT) em ajustes com backup e desfazer. Ficam de fora o que não tem volta no script:
/// apagar serviços, tarefas, Active Setup e RunOnce do Edge (aqui os serviços de atualização são só desativados),
/// reinstalar o Edge de um espelho, mexer no settings.dat da Store e gravar o setupcomplete.cmd em pendrive.
/// </summary>
public static class SetupTweaks
{
    private const string WuPolicy = @"Software\Policies\Microsoft\Windows\WindowsUpdate";
    private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";
    /// <summary>Servidor de atualização que não existe: o Windows Update deixa de achar atualizações (o script usava um site qualquer).</summary>
    public const string NoUpdateServer = "http://127.0.0.1/";
    /// <summary>Serviços de atualização do Edge (desativados com Start = 4 só se existirem no PC).</summary>
    public static readonly string[] EdgeServices = { "edgeupdate", "edgeupdatem", "MicrosoftEdgeElevationService" };

    private static Setting Lm(string path, string name, int value) => new("HKLM", path, name, value);

    public static readonly SetupTweak[] Static =
    {
        new("tw-background-apps", "Apps em segundo plano desligados",
            "Apps da Microsoft Store param de rodar escondidos. Pode atrasar notificações e sincronização desses apps; programas comuns não mudam.",
            false, true, "3 Setup/9 Background Apps.ps1",
            new[] { Lm(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2),
                    new Setting("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0),
                    new Setting("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1) }),
        new("tw-driver-updates", "Bloquear drivers pelo Windows Update",
            "O Windows para de baixar e trocar drivers sozinho (inclusive o de vídeo depois do DDU). Os drivers passam a ser instalados por você. Vale para Pro, LTSC, IoT e Server.",
            false, true, "3 Setup/5 Updates Drivers Block.ps1",
            new[] { Lm(@"Software\Policies\Microsoft\Windows\Device Metadata", "PreventDeviceMetadataFromNetwork", 1),
                    Lm(@"Software\Policies\Microsoft\Windows\DeviceInstall\Settings", "DisableSendGenericDriverNotFoundToWER", 1),
                    Lm(@"Software\Policies\Microsoft\Windows\DeviceInstall\Settings", "DisableSendRequestAdditionalSoftwareToWER", 1),
                    Lm(@"Software\Policies\Microsoft\Windows\DriverSearching", "SearchOrderConfig", 0),
                    Lm(WuPolicy, "SetAllowOptionalContent", 0),
                    Lm(WuPolicy, "AllowTemporaryEnterpriseFeatureControl", 0),
                    Lm(WuPolicy, "ExcludeWUDriversInQualityUpdate", 1),
                    Lm(WuPolicy + @"\AU", "IncludeRecommendedUpdates", 0),
                    Lm(WuPolicy + @"\AU", "EnableFeaturedSoftware", 0) }),
        new("tw-block-updates", "Bloquear o Windows Update",
            "Nenhuma atualização é baixada, nem as de segurança, e a página do Windows Update fica bloqueada. Use só se souber o que está fazendo; desmarque e aplique para voltar.",
            true, false, "3 Setup/5 Updates Drivers Block.ps1",
            new[] { Lm(WuPolicy, "DoNotConnectToWindowsUpdateInternetLocations", 1),
                    new Setting("HKLM", WuPolicy, "UpdateServiceUrlAlternate", 0, NoUpdateServer),
                    new Setting("HKLM", WuPolicy, "WUStatusServer", 0, NoUpdateServer),
                    new Setting("HKLM", WuPolicy, "WUServer", 0, NoUpdateServer),
                    Lm(WuPolicy, "SetDisableUXWUAccess", 1),
                    Lm(WuPolicy + @"\AU", "NoAutoUpdate", 1),
                    Lm(WuPolicy + @"\AU", "UseWUServer", 1) }),
        new("tw-edge-background", "Edge sem segundo plano e sem inicialização rápida",
            "O Edge fecha de verdade ao fechar a janela e não abre escondido junto com o Windows (Startup Boost).",
            false, true, "3 Setup/10 Edge Settings.ps1",
            new[] { Lm(Edge, "BackgroundModeEnabled", 0), Lm(Edge, "StartupBoostEnabled", 0) }),
        new("tw-edge-ublock", "Edge com o uBlock Origin",
            "Instala o bloqueador de anúncios uBlock Origin pela loja do Edge, como política (aparece \"gerenciado pela organização\").",
            false, true, "3 Setup/10 Edge Settings.ps1",
            new[] { new Setting("HKLM", Edge + @"\ExtensionInstallForcelist", "1", 0,
                        "odfafepnkmbhccpbejgmiehpchacaeak;https://edge.microsoft.com/extensionwebstorebase/v1/crx") }),
        new("tw-edge-hwaccel", "Edge sem aceleração de hardware",
            "Tira o Edge da placa de vídeo. Ajuda se ele atrapalha o jogo, mas vídeos no navegador passam a gastar mais CPU.",
            true, false, "3 Setup/10 Edge Settings.ps1",
            new[] { Lm(Edge, "HardwareAccelerationModeEnabled", 0) }),
        new("tw-store-updates", "Store sem atualização automática de apps",
            "Os apps da Microsoft Store só atualizam quando você abrir a Store e pedir. Apps desatualizados podem ficar sem correções.",
            true, false, "3 Setup/11 Store Settings.ps1",
            new[] { Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload", 2) }),
    };

    /// <summary>Desativar os serviços de atualização do Edge que existem no PC (o script apagava os serviços).</summary>
    public static SetupTweak EdgeUpdate(Func<string, bool> serviceExists) => new("tw-edge-update", "Edge sem serviços de atualização",
        "Desativa os serviços de atualização do Edge (o Edge para de se atualizar sozinho e de rodar o atualizador no fundo). O Edge fica sem correções de segurança até você desfazer.",
        true, false, "3 Setup/10 Edge Settings.ps1",
        EdgeServices.Where(serviceExists).Select(s => Lm(@"SYSTEM\CurrentControlSet\Services\" + s, "Start", 4)).ToArray());
}
