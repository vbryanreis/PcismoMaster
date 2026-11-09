namespace ResetExplode.Modular;

/// <summary>Página do TWEAKING onde o ajuste aparece.</summary>
public enum OptPage { Debloat, Windows, Mouse, Teclado, Gpu }

/// <summary>Nível no Debloat (Essencial e Recomendado vêm marcados; Avançado nunca). Nas outras páginas é Normal.</summary>
public enum OptLevel { Essencial, Recomendado, Avancado, Normal }

/// <summary>O que um ajuste faz além de valores de registro. Cada tipo guarda o estado de antes e sabe desfazer.</summary>
public enum ExtraKind
{
    Tasks,          // desligar tarefas agendadas (Args = caminhos completos)
    Capabilities,   // remover recursos opcionais (Args = nomes com curinga)
    FeatureOn,      // ligar recurso do Windows (Args[0] = nome)
    FeatureOff,     // desligar recurso do Windows (Args[0] = nome)
    Power,          // valor do plano ativo, na tomada e na bateria (Args = subgrupo, configuração, valor)
    Hibernate,      // powercfg /hibernate off
    ReservedStorage,// armazenamento reservado desligado
    MemCompression, // compressão de memória desligada
    Appx,           // remover apps (Args = nomes com curinga); sem volta automática, só pela Store
    OneDrive,       // desinstalar o OneDrive
    Mstsc,          // desinstalar a Conexão de Área de Trabalho Remota
    BootName,       // nome no menu de inicialização (Args[0] = nome)
    CreateKey,      // criar chave com valor padrão vazio (Args = hive, caminho, raiz que apaga ao desfazer)
    DeleteKeys,     // apagar chaves com cópia .reg (Args = caminhos completos HKLM\...)
    RefreshMax,     // monitores na maior taxa de atualização
    Discord,        // opção do settings.json do Discord (Args = nome, valor JSON)
    NewUsers,       // copiar os ajustes de usuário aplicados para contas criadas depois
    PageCombining,  // combinação de páginas de memória desligada (Disable-MMAgent -PageCombining)
    NicProp,        // propriedade avançada de todas as placas de rede físicas (Args = palavra-chave, valor)
    NetBios,        // NetBIOS sobre TCP/IP desligado em todas as interfaces (NetbiosOptions = 2)
    Bcd,            // opção do gerenciador de inicialização (Args = nome, valor), ex.: disabledynamictick yes
}

public sealed record OptExtra(ExtraKind Kind, params string[] Args);

/// <summary>
/// Um ajuste do TWEAKING. Os valores de registro passam pelo ModuleEngine (backup exato e desfazer); os Extras guardam o
/// estado de antes num diário próprio. Group: ajustes que mexem no mesmo valor e só um pode ficar ligado (ex.: IPv6).
/// </summary>
public sealed record OptTweak(string Id, OptPage Page, OptLevel Level, string Category, string Title, string Description,
    Setting[] Settings, OptExtra[] Extras, string? Warning = null, bool Recommended = false, bool Restart = false,
    bool Explorer = false, string? Group = null, string[]? StopServices = null, string? UndoNote = null)
{
    public bool DefaultOn => Level is OptLevel.Essencial or OptLevel.Recomendado || (Level == OptLevel.Normal && Recommended);
}

/// <summary>Um pacote pronto de ajustes (Perfis).</summary>
public sealed record OptProfile(string Id, string Title, string[] Lines, string[] Tweaks, bool Cleanup);

/// <summary>
/// Catálogo do TWEAKING: Debloat, Windows, Mouse, Teclado e GPU. Inspirado na aba Otimizar do Galdino Control, escrito do
/// zero (o que cada ajuste muda são configurações documentadas do Windows). Fica de fora o que o PcismoMaster já tem em
/// outra aba (plano de energia por jogo no Launcher; apps pré-instalados no Bloatware) e o que
/// desliga proteções do Windows (Isolamento de Núcleo, UAC).
/// </summary>
public static class OptCatalog
{
    private const OptLevel E = OptLevel.Essencial, R = OptLevel.Recomendado, A = OptLevel.Avancado, N = OptLevel.Normal;
    private static readonly OptExtra[] None = Array.Empty<OptExtra>();

    private static Setting Lm(string p, string n, int v) => new("HKLM", p, n, v);
    private static Setting Cu(string p, string n, int v) => new("HKCU", p, n, v);
    private static Setting LmT(string p, string n, string t) => new("HKLM", p, n, 0, t);
    private static Setting CuT(string p, string n, string t) => new("HKCU", p, n, 0, t);
    private static Setting LmX(string p, string n, string t) => new("HKLM", p, n, 0, Kind: 2, Data: System.Text.Json.JsonSerializer.Serialize(t));
    private static Setting CuB(string p, string n, string hex) => new("HKCU", p, n, 0, Hex: hex);
    private static Setting CuDel(string p, string n) => new("HKCU", p, n, 0, Remove: true);
    private static Setting[] Svc(string name, int start, bool delayed = false)
    {
        string p = @"SYSTEM\CurrentControlSet\Services\" + name;
        return delayed ? new[] { Lm(p, "Start", start), Lm(p, "DelayedAutostart", 1) } : new[] { Lm(p, "Start", start) };
    }
    private static OptExtra X(ExtraKind k, params string[] a) => new(k, a);
    private static Setting[] S(params Setting[] s) => s;
    private static Setting[] SS(params Setting[][] s) => s.SelectMany(x => x).ToArray();

    private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Pol = @"SOFTWARE\Policies\Microsoft\Windows";
    private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";
    private const string Brave = @"SOFTWARE\Policies\BraveSoftware\Brave";
    private const string Mm = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string Blocked = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
    private const string Tcp6 = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters";
    private const string Acc = @"Control Panel\Accessibility";

    /// <summary>Limite para o Windows juntar serviços num svchost só: a RAM do PC em KB (o mesmo que o Windows usa).</summary>
    public static int SvcHostThresholdKb()
    {
        long kb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024;
        return (int)Math.Clamp(kb, 3_670_016, int.MaxValue);
    }

    public static readonly OptTweak[] All = Build();

    private static OptTweak T(string id, OptPage page, OptLevel level, string cat, string title, string desc, Setting[] s, OptExtra[]? x = null,
        string? warn = null, bool rec = false, bool restart = false, bool explorer = false, string? group = null, string[]? stop = null, string? undo = null) =>
        new(id, page, level, cat, title, desc, s, x ?? None, warn, rec, restart, explorer, group, stop, undo);

    private static OptTweak[] Build()
    {
        const OptPage D = OptPage.Debloat, W = OptPage.Windows, M = OptPage.Mouse, K = OptPage.Teclado, G = OptPage.Gpu;
        var photo = PhotoViewer();
        return new[]
        {
            // ===================== DEBLOAT · Essencial =====================
            T("edge_preload", D, E, "Desempenho", "Não deixar o Edge carregado sem uso", "Desliga a inicialização acelerada e o modo em segundo plano do Microsoft Edge.",
                S(Lm(Edge, "StartupBoostEnabled", 0), Lm(Edge, "BackgroundModeEnabled", 0))),
            T("widgets", D, E, "Aparência", "Desligar os Widgets (notícias e interesses)", "Desativa o painel de Widgets por política: ele fica carregando notícias em segundo plano.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)), explorer: true),
            T("chat_off", D, E, "Aparência", "Sem o Chat do Teams na barra de tarefas", "Remove o botão do Chat (Microsoft Teams pessoal) e impede que ele volte.",
                S(Cu(Adv, "TaskbarMn", 0), Lm(Pol + @"\Windows Chat", "ChatIcon", 3)), explorer: true),
            T("silent_apps", D, E, "Privacidade", "Não instalar apps patrocinados sozinho", "Impede que o Windows instale jogos e apps sugeridos sem perguntar, e as \"experiências de consumidor\".",
                S(Cu(Cdm, "SilentInstalledAppsEnabled", 0), Cu(Cdm, "OemPreInstalledAppsEnabled", 0), Cu(Cdm, "PreInstalledAppsEnabled", 0),
                  Lm(Pol + @"\CloudContent", "DisableWindowsConsumerFeatures", 1))),
            T("telemetry", D, E, "Privacidade", "Telemetria no mínimo", "Envia à Microsoft só os dados de diagnóstico obrigatórios para a sua edição do Windows.",
                S(Lm(Pol + @"\DataCollection", "AllowTelemetry", 0))),
            T("diagtrack", D, E, "Privacidade", "Serviço de telemetria (DiagTrack) em Manual", "Coloca o \"Experiências do Usuário Conectado e Telemetria\" em Manual e para o serviço.",
                Svc("DiagTrack", 3), stop: new[] { "DiagTrack" }),
            T("telemetry_tasks", D, E, "Privacidade", "Desligar tarefas agendadas de telemetria", "Avaliador de compatibilidade, programa de experiência do usuário e envio de relatórios, que acordam sozinhos.",
                S(), new[] { X(ExtraKind.Tasks, @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser", @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                    @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                    @"\Microsoft\Windows\Feedback\Siuf\DmClient", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload", @"\Microsoft\Windows\Windows Error Reporting\QueueReporting") }),
            T("data_tasks_off", D, E, "Desempenho", "Desligar tarefas de coleta de dados", "Tarefas que coletam dados de disco, uso de recursos, compatibilidade e economia de energia em segundo plano.",
                S(), new[] { X(ExtraKind.Tasks, @"\Microsoft\Windows\Application Experience\PcaPatchDbTask", @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                    @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReporting", @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem") }),
            T("compat_telemetry", D, E, "Privacidade", "Desligar a telemetria de compatibilidade", "Para o inventário de programas e o rastreador de aplicativos usados pelo avaliador de compatibilidade.",
                S(Lm(Pol + @"\AppCompat", "AITEnable", 0), Lm(Pol + @"\AppCompat", "DisableInventory", 1))),
            T("copilot", D, E, "IA", "Desligar o Copilot do Windows", "Desativa o assistente Copilot por política, para este usuário e para o PC inteiro.",
                S(Cu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1), Lm(Pol + @"\WindowsCopilot", "TurnOffWindowsCopilot", 1))),
            T("recall", D, E, "IA", "Desligar o Recall (capturas da tela)", "Impede que o Windows salve e analise capturas periódicas da sua tela (PCs Copilot+).",
                S(Cu(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1), Lm(Pol + @"\WindowsAI", "DisableAIDataAnalysis", 1),
                  Lm(Pol + @"\WindowsAI", "AllowRecallEnablement", 0))),
            T("click_to_do", D, E, "IA", "Desligar o Clique para Fazer (Click to Do)", "Desativa a sobreposição de IA que analisa o que está na tela para sugerir ações.",
                S(Cu(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1), Lm(Pol + @"\WindowsAI", "DisableClickToDo", 1))),
            T("recall_feature", D, E, "IA", "Remover o componente Recall", "Desinstala o recurso opcional Recall do Windows (vale após reiniciar). Dá para reinstalar desfazendo.",
                S(), new[] { X(ExtraKind.FeatureOff, "Recall") }, warn: "Só existe em versões recentes do Windows 11; nas outras o estado aparece como já aplicado.", restart: true),
            T("delivery_p2p", D, E, "Rede", "Não enviar atualizações para outros PCs", "O Windows para de usar sua internet para distribuir atualizações a outros computadores.",
                S(Lm(Pol + @"\DeliveryOptimization", "DODownloadMode", 0))),
            T("bing", D, E, "Privacidade", "Pesquisa do Iniciar só no PC", "Sem resultados da web nem Bing: o que você digita no Iniciar não sai do computador.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0), Cu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)), explorer: true),
            T("cloud_content_policy", D, E, "Privacidade", "Sem conteúdo de conta e dicas da nuvem (política)", "Para todas as contas: o Windows não mostra avisos sobre o estado da conta Microsoft nem dicas baixadas da internet.",
                S(Lm(Pol + @"\CloudContent", "DisableConsumerAccountStateContent", 1), Lm(Pol + @"\CloudContent", "DisableSoftLanding", 1))),
            T("speech_updates_off", D, E, "Privacidade", "Sem download automático de dados de fala", "O Windows para de baixar em segundo plano modelos de reconhecimento e síntese de voz.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\Speech", "AllowSpeechModelUpdate", 0))),
            T("explorer_insights_off", D, E, "Privacidade", "Explorer sem recomendações da conta", "O Explorer deixa de buscar na nuvem arquivos recentes, favoritos e recomendados da conta Microsoft.",
                S(Lm(Pol + @"\Explorer", "DisableGraphRecentItems", 1))),
            T("settings_agent_off", D, E, "IA", "Sem a pesquisa com IA nas Configurações", "Desliga o agente de IA que interpreta a pesquisa do app Configurações em linguagem natural.",
                S(Lm(Pol + @"\WindowsAI", "DisableSettingsAgent", 1))),
            T("push_install_off", D, E, "Privacidade", "Desligar a Instalação por Push", "Ninguém consegue mandar a Microsoft Store instalar apps neste PC a partir de outro aparelho ou do site.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\PushToInstall", "DisablePushToInstall", 1))),
            T("cloud_search_policy", D, E, "Privacidade", "Pesquisa sem nuvem e sem Cortana (política)", "Para todas as contas: a pesquisa do Windows não consulta OneDrive, Outlook e outros serviços na nuvem, e a Cortana fica desligada.",
                S(Lm(Pol + @"\Windows Search", "AllowCloudSearch", 0), Lm(Pol + @"\Windows Search", "AllowCortana", 0))),
            T("background_motion_off", D, E, "Privacidade", "Apps sem acesso a movimentos em segundo plano", "Apps fechados não leem sensores de movimento e posição do usuário.",
                S(Lm(Pol + @"\AppPrivacy", "LetAppsAccessBackgroundSpatialPerception", 2))),
            T("ad_id_policy", D, E, "Privacidade", "ID de anúncio desligado para todas as contas (política)", "Reforça o ajuste do ID de anúncio para todos os usuários do PC, sem poder ser religado nas Configurações.",
                S(Lm(Pol + @"\AdvertisingInfo", "DisabledByGroupPolicy", 1))),
            T("no_outlook_devhome", D, E, "Privacidade", "Não reinstalar o novo Outlook e o Dev Home", "Impede que o Windows Update instale de novo o novo Outlook e o Dev Home depois que você os removeu.",
                S(), new[] { X(ExtraKind.DeleteKeys, @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate",
                    @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\DevHomeUpdate") }),
            T("cdm_full", D, E, "Privacidade", "Sem entrega de conteúdo da Microsoft (completo)", "Complementa os ajustes de propaganda: desliga de vez o serviço que baixa sugestões, apps e conteúdo patrocinado para o Iniciar, a tela de bloqueio e as Configurações.",
                S(Cu(Cdm, "ContentDeliveryAllowed", 0), Cu(Cdm, "FeatureManagementEnabled", 0), Cu(Cdm, "PreInstalledAppsEverEnabled", 0),
                  Cu(Cdm, "SubscribedContent-353698Enabled", 0), Cu(Cdm, "SubscribedContentEnabled", 0))),
            T("notepad_banner_off", D, E, "Privacidade", "Bloco de Notas sem propaganda da Store", "Remove o aviso que oferece a versão da Microsoft Store do Bloco de Notas.",
                S(Cu(@"Software\Microsoft\Notepad", "ShowStoreBanner", 0))),

            // ===================== DEBLOAT · Recomendado =====================
            T("background_apps", D, R, "Desempenho", "Bloquear apps da Store em segundo plano", "Apps da Microsoft Store param de rodar escondidos consumindo processador, memória e rede.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1),
                  Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0))),
            T("ai_remove", D, R, "IA", "Remover os apps de IA (Copilot e Microsoft 365)", "Desinstala o app Copilot e o Microsoft 365 Copilot (Office Hub), desativa o serviço de IA do Windows e esconde a página de componentes de IA das Configurações.",
                SS(Svc("WSAIFabricSvc", 4), S(LmT(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "hide:aicomponents"))),
                new[] { X(ExtraKind.Appx, "Microsoft.Copilot", "Microsoft.Windows.Ai.Copilot.Provider", "Microsoft.MicrosoftOfficeHub", "Microsoft.Microsoft365Copilot*") },
                warn: "Os apps removidos não voltam ao desfazer: reinstale pela Microsoft Store se precisar.", stop: new[] { "WSAIFabricSvc" },
                undo: "Desfazer religa o serviço e a página de IA; os apps voltam pela Microsoft Store."),
            T("services_manual", D, R, "Desempenho", "Serviços desnecessários em Manual/Desativado", "Arquivos Offline desativado; Mapas e Armazenamento sob demanda. Também agrupa os serviços do svchost conforme a RAM, reduzindo processos. O Hotspot móvel continua funcionando.",
                SS(Svc("CscService", 4), Svc("MapsBroker", 3), Svc("StorSvc", 3), S(Lm(@"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", SvcHostThresholdKb()))),
                warn: "Arquivos Offline (sincronização de pastas de rede de empresa) deixa de funcionar até desfazer.", restart: true),
            T("tracking_services_off", D, R, "Privacidade", "Desligar serviços de rastreamento e diagnóstico", "Rastreamento de links distribuídos, coletor de diagnóstico e Assistente de Compatibilidade de Programas.",
                SS(Svc("TrkWks", 4), Svc("diagnosticshub.standardcollector.service", 4), Svc("PcaSvc", 4)),
                warn: "O Windows deixa de oferecer ajustes de compatibilidade automáticos para programas muito antigos.", stop: new[] { "TrkWks", "diagnosticshub.standardcollector.service", "PcaSvc" }),
            T("clipboard_sync_off", D, R, "Privacidade", "Não sincronizar a área de transferência entre aparelhos", "O que você copia não é enviado à nuvem para colar em outro PC ou no celular. O histórico local (Win+V) continua.",
                S(Lm(Pol + @"\System", "AllowCrossDeviceClipboard", 0)), warn: "Evite se você copia no PC e cola no celular (ou o contrário) pela conta Microsoft."),

            // ===================== DEBLOAT · Avançado =====================
            T("sysmain", D, A, "Desempenho", "Desligar o SysMain (Superfetch)", "Para o pré-carregamento de programas na memória. Em SSD o ganho dele é pequeno e libera RAM e disco.",
                Svc("SysMain", 4), warn: "Evite se o Windows está em HD mecânico: lá o SysMain acelera a abertura de programas.", stop: new[] { "SysMain" }),
            T("search_index", D, A, "Desempenho", "Desligar a indexação de arquivos", "Para o serviço Windows Search, que fica lendo o disco para montar o índice de pesquisa.",
                Svc("WSearch", 4), warn: "Evite se você pesquisa muito arquivos pelo Iniciar ou pelo Explorer: a busca fica lenta.", stop: new[] { "WSearch" }),
            T("spooler_manual", D, A, "Desempenho", "Spooler de impressão sob demanda", "O serviço de impressão passa para Manual: só liga quando algum programa vai imprimir.",
                Svc("Spooler", 3), warn: "Evite se você imprime com frequência e quer a impressora sempre pronta."),
            T("hibernation", D, A, "Geral", "Desligar a hibernação", "Apaga o hiberfil.sys e libera vários GB no disco. Também desliga a Inicialização Rápida.",
                S(), new[] { X(ExtraKind.Hibernate) }, warn: "Evite em notebook se você usa a hibernação para fechar a tampa sem perder o trabalho."),
            T("reserved_storage", D, A, "Geral", "Liberar o armazenamento reservado", "Devolve os GB que o Windows guarda para atualizações. Ele reativa sozinho se precisar.",
                S(), new[] { X(ExtraKind.ReservedStorage) }, warn: "Pode falhar enquanto houver uma atualização pendente; tente depois de reiniciar."),
            T("peripheral_software_off", D, A, "Geral", "Bloquear instalação automática de software de periféricos", "Impede que o Windows instale sozinho programas de fabricantes (Razer etc.) ao conectar aparelhos.",
                S(Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer", "DisableCoInstallers", 1), Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 0)),
                warn: "O Windows Update também deixa de buscar drivers sozinho; atualize drivers pelo site do fabricante."),
            T("device_metadata", D, A, "Privacidade", "Não instalar apps de acompanhamento de dispositivos", "Impede que o Windows baixe programas e propaganda ao conectar monitores, mouses e outros aparelhos.",
                S(Lm(Pol + @"\Device Metadata", "PreventDeviceMetadataFromNetwork", 1)), warn: "Ícones e nomes de alguns aparelhos podem aparecer genéricos em Dispositivos e Impressoras."),
            T("onedrive_remove", D, A, "Desempenho", "Remover o OneDrive", "Desinstala o OneDrive. Arquivos já baixados ficam na pasta do usuário e os da nuvem continuam na sua conta.",
                S(), new[] { X(ExtraKind.OneDrive) }, warn: "Evite se você usa o OneDrive para backup ou para sincronizar arquivos entre aparelhos."),
            T("cap_handwriting", D, A, "Desempenho", "Remover escrita à mão e o Painel de Entrada de Matemática", "Remove o reconhecimento de escrita à mão e de fórmulas, usados com caneta ou tela de toque.",
                S(), new[] { X(ExtraKind.Capabilities, "Language.Handwriting~~~*", "MathRecognizer~~~~*") }, warn: "Evite em notebooks com caneta ou tela de toque. Desfazer reinstala pela internet."),
            T("cap_quickassist", D, A, "Privacidade", "Remover a Assistência Rápida antiga", "Remove o componente antigo da Assistência Rápida, usado para receber ajuda remota e explorado em golpes.",
                S(), new[] { X(ExtraKind.Capabilities, "App.Support.QuickAssist~~~~*") }, warn: "Evite se alguém da família ou o suporte técnico acessa este PC pela Assistência Rápida."),
            T("cap_steps_recorder", D, A, "Privacidade", "Remover o Gravador de Passos", "Remove o programa que grava cliques e telas para relatar problemas (a Microsoft já o descontinuou).",
                S(), new[] { X(ExtraKind.Capabilities, "App.StepsRecorder~~~~*") }, warn: "Se um suporte técnico pedir uma gravação de passos (psr.exe), ela não vai existir até desfazer."),
            T("cap_speech", D, A, "Desempenho", "Remover reconhecimento de fala e vozes do Windows", "Remove os pacotes de reconhecimento de voz e de leitura de texto em voz alta.",
                S(), new[] { X(ExtraKind.Capabilities, "Language.Speech~~~*", "Language.TextToSpeech~~~*") }, warn: "O Narrador e a leitura em voz alta ficam sem voz. Não use se alguém no PC precisa de acessibilidade."),
            T("cap_hello_face", D, A, "Desempenho", "Remover o Windows Hello facial", "Remove o reconhecimento facial de login.",
                S(), new[] { X(ExtraKind.Capabilities, "Hello.Face.*") }, warn: "Evite se você entra no Windows pelo rosto (câmera infravermelha)."),
            T("cap_onesync", D, A, "Desempenho", "Remover a sincronização de Email, Calendário e Pessoas", "Remove o componente que sincroniza contas de email e calendário dos apps antigos do Windows.",
                S(), new[] { X(ExtraKind.Capabilities, "OneCoreUAP.OneSync~~~~*") }, warn: "Os apps antigos de Email e Calendário param de sincronizar. O Outlook e o navegador não são afetados."),
            T("mstsc_remove", D, A, "Geral", "Remover a Conexão de Área de Trabalho Remota", "Remove o programa mstsc, usado para controlar outro PC pela Área de Trabalho Remota.",
                S(), new[] { X(ExtraKind.Mstsc) }, warn: "Evite se você acessa servidores ou o PC do trabalho pela Área de Trabalho Remota."),
            T("pca_policy_off", D, A, "Geral", "Desligar o Assistente de Compatibilidade de Programas (política)", "O Windows para de monitorar programas antigos para sugerir modos de compatibilidade.",
                S(Lm(Pol + @"\AppCompat", "DisablePCA", 1)), warn: "Programas muito antigos deixam de receber a sugestão automática de modo de compatibilidade."),
            T("compat_engine_off", D, A, "Geral", "Desligar o Mecanismo de Compatibilidade de Aplicativos", "Desliga as correções que o Windows aplica sozinho em programas antigos (inclui o SwitchBack).",
                S(Lm(Pol + @"\AppCompat", "DisableEngine", 1)), warn: "Pode fazer jogos e programas antigos pararem de abrir ou travarem. Desfaça se isso acontecer.", restart: true),
            T("find_my_device_off", D, A, "Privacidade", "Desligar o Localizar Meu Dispositivo", "A localização do PC deixa de ser registrada na conta Microsoft.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0)), warn: "Em notebook, você perde como encontrar o aparelho se ele for perdido ou roubado."),
            T("store_autoupdate_off", D, A, "Desempenho", "Microsoft Store sem atualização automática de apps", "Os apps da Store só se atualizam quando você abre a Store e manda atualizar.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2)), warn: "Apps como WhatsApp ficam sem correções de segurança até você atualizar. Algumas edições do Windows ignoram esta política."),
            T("wu_notifications_off", D, A, "Geral", "Sem notificações do Windows Update", "O Windows Update deixa de mostrar notificações, exceto os avisos de reinício.",
                S(Lm(Pol + @"\WindowsUpdate", "SetUpdateNotificationLevel", 1), Lm(Pol + @"\WindowsUpdate", "UpdateNotificationLevel", 1)),
                warn: "Você deixa de ser avisado de atualizações de segurança: confira o Windows Update de vez em quando."),
            T("wer_logging_off", D, A, "Geral", "Desligar o registro do Relatório de Erros", "Os eventos do Relatório de Erros do Windows deixam de ser gravados no Visualizador de Eventos.",
                S(Lm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "LoggingDisabled", 1)), warn: "Fica mais difícil descobrir a causa de travamentos."),
            T("background_apps_policy", D, A, "Desempenho", "Bloquear apps em segundo plano para todas as contas (política)", "Versão forçada do bloqueio: nenhum app da Store roda em segundo plano, e o usuário não consegue liberar.",
                S(Lm(Pol + @"\AppPrivacy", "LetAppsRunInBackground", 2)), warn: "WhatsApp, Spotify, Phone Link e outros apps da Store param de notificar com o app fechado."),

            // ===================== WINDOWS =====================
            T("mem_compression", W, N, "Desempenho", "Desligar a compressão de memória", "Com bastante RAM, comprimir páginas só gasta processador. Desliga o recurso (vale após reiniciar).",
                S(), new[] { X(ExtraKind.MemCompression) }, warn: "Evite com 8 GB de RAM ou menos: a compressão ajuda quando a memória aperta.", rec: true, restart: true),
            T("win32_priority", W, N, "Desempenho", "Priorizar o programa em primeiro plano", "Dá fatias de processador maiores e mais frequentes para a janela ativa (jogo ou programa aberto).",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26)), rec: true),
            T("core_parking", W, N, "Desempenho", "Manter todos os núcleos acordados", "Desliga o estacionamento de núcleos do plano ativo: nenhum núcleo dorme e a resposta é imediata.",
                S(), new[] { X(ExtraKind.Power, "54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", "100") },
                warn: "Evite em notebook: os núcleos dormindo economizam bateria.", rec: true),
            T("ntfs_last_access", W, N, "Desempenho", "Não gravar a data de último acesso", "O NTFS deixa de escrever no disco a cada arquivo aberto só para atualizar a data de acesso.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", unchecked((int)0x80000001))),
                warn: "Evite se você precisa saber quando cada arquivo foi aberto (auditoria).", rec: true, restart: true),
            T("storage_sense", W, N, "Geral", "Desligar o Sensor de Armazenamento automático", "O Windows para de fazer limpezas sozinho em horários aleatórios; você limpa quando quiser.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 0))),
            T("fast_startup", W, N, "Geral", "Desligar a Inicialização Rápida", "O desligamento passa a ser completo: evita problemas com dual boot, drivers e atualizações.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0)), rec: true),
            T("wu_notify", W, N, "Geral", "Windows Update só avisa, não instala sozinho", "O Windows avisa antes de baixar e instalar, em vez de reiniciar o PC por conta própria.",
                S(Lm(Pol + @"\WindowsUpdate\AU", "NoAutoUpdate", 0), Lm(Pol + @"\WindowsUpdate\AU", "AUOptions", 2)),
                warn: "Evite se você esquece de atualizar: as correções de segurança dependem disso."),
            T("game_mode", W, N, "Jogos", "Modo de Jogo do Windows", "Garante o Modo de Jogo ligado: o jogo tem prioridade e o Windows Update não instala durante a partida.",
                S(Cu(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1), Cu(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)), rec: true),
            T("game_dvr", W, N, "Jogos", "Desligar a gravação em segundo plano (Game DVR)", "O Xbox Game Bar para de gravar os últimos minutos de jogo o tempo todo: recupera alguns FPS e alivia GPU e disco.",
                S(Cu(@"System\GameConfigStore", "GameDVR_Enabled", 0), Cu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)),
                warn: "Evite se você usa o \"gravar o que aconteceu\" do Game Bar (Win+Alt+G).", rec: true),
            T("games_priority", W, N, "Jogos", "Prioridade alta para jogos", "Ajusta o perfil \"Games\" do agendador multimídia: mais prioridade de processador e GPU para o jogo.",
                S(Lm(Mm + @"\Tasks\Games", "GPU Priority", 8), Lm(Mm + @"\Tasks\Games", "Priority", 6),
                  LmT(Mm + @"\Tasks\Games", "Scheduling Category", "High"), LmT(Mm + @"\Tasks\Games", "SFIO Priority", "High")), rec: true),
            T("system_responsiveness", W, N, "Jogos", "Menos reserva para tarefas de fundo", "Reduz de 20% para 10% a fatia do processador reservada a tarefas de baixa prioridade.",
                S(Lm(Mm, "SystemResponsiveness", 10)), rec: true),
            T("network_throttling", W, N, "Rede", "Sem limite de pacotes de rede para multimídia", "Remove o NetworkThrottlingIndex, que limita pacotes por milissegundo enquanto há som tocando.",
                S(Lm(Mm, "NetworkThrottlingIndex", -1)), rec: true),
            T("wifi_power", W, N, "Rede", "Wi-Fi sem economia de energia", "O adaptador Wi-Fi fica no desempenho máximo do plano ativo: conexão mais estável e ping menor.",
                S(), new[] { X(ExtraKind.Power, "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1", "12bbebe6-58d6-4636-95bb-3217ef867c1a", "0") },
                warn: "Evite em notebook na bateria: o Wi-Fi gasta mais.", rec: true),
            T("visual_fx", W, N, "Aparência", "Efeitos visuais leves", "Desliga animações de janelas, da barra de tarefas e sombras; menos trabalho para a GPU fora do jogo.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3), CuT(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0"),
                  Cu(Adv, "ListviewShadow", 0), Cu(Adv, "TaskbarAnimations", 0), Cu(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0)), rec: true, explorer: true),
            T("transparency", W, N, "Aparência", "Desligar a transparência", "Remove o efeito acrílico de menus e barra de tarefas; alivia a GPU em máquinas modestas.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0))),
            T("classic_menu", W, N, "Aparência", "Menu de contexto clássico", "O clique direito volta a mostrar todas as opções, sem o \"Mostrar mais opções\". Reinicia o Explorer.",
                S(), new[] { X(ExtraKind.CreateKey, "HKCU", @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}") }, explorer: true),
            T("taskbar_left", W, N, "Aparência", "Barra de tarefas alinhada à esquerda", "Ícones da barra de tarefas à esquerda, como no Windows 10.",
                S(Cu(Adv, "TaskbarAl", 0))),
            T("file_ext", W, N, "Aparência", "Mostrar extensões de arquivo", "Exibe .exe, .pdf, .txt no Explorer: mais fácil identificar arquivos e golpes como foto.jpg.exe.",
                S(Cu(Adv, "HideFileExt", 0)), rec: true, explorer: true),
            T("notifications", W, N, "Geral", "Desligar as notificações do Windows", "Acaba com os balões de notificação (toasts): menos interrupção e menos processos rodando.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0)), warn: "Evite se você depende de lembretes de agenda, e-mail ou mensagens por notificação.", rec: true),
            T("error_reporting", W, N, "Privacidade", "Desligar o relatório de erros", "Para a coleta e o envio de relatórios de falhas à Microsoft.",
                S(Lm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)), warn: "O histórico de confiabilidade fica com menos detalhes sobre travamentos.", rec: true),
            T("tailored", W, N, "Privacidade", "Sem experiências personalizadas", "A Microsoft deixa de usar seus dados de diagnóstico para dicas e anúncios sob medida.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)), rec: true),
            T("feedback", W, N, "Privacidade", "Não pedir feedback", "O Windows para de perguntar sua opinião em notificações.",
                S(Cu(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0), CuDel(@"Software\Microsoft\Siuf\Rules", "PeriodInNanoSeconds")), rec: true),
            T("activity", W, N, "Privacidade", "Desligar o histórico de atividades", "O Windows não guarda nem sincroniza a lista de apps e arquivos que você abriu.",
                S(Lm(Pol + @"\System", "EnableActivityFeed", 0), Lm(Pol + @"\System", "PublishUserActivities", 0), Lm(Pol + @"\System", "UploadUserActivities", 0)), rec: true),
            T("ad_id", W, N, "Privacidade", "Desligar o ID de anúncio", "Apps deixam de usar um identificador seu para mostrar anúncios direcionados.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)), rec: true),
            T("location_off", W, N, "Privacidade", "Desligar o serviço de localização", "Windows e apps deixam de consultar a localização do dispositivo.",
                S(LmT(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny")),
                warn: "Evite em notebook se você usa mapas, clima local ou \"Encontrar meu dispositivo\".", rec: true),
            T("start_suggestions", W, N, "Privacidade", "Sem sugestões e propagandas no Iniciar", "Remove apps sugeridos e anúncios do menu Iniciar.",
                S(Cu(Cdm, "SubscribedContent-338388Enabled", 0), Cu(Cdm, "SystemPaneSuggestionsEnabled", 0)), rec: true),
            T("lock_screen", W, N, "Privacidade", "Sem dicas na tela de bloqueio", "A tela de bloqueio mostra só a imagem, sem curiosidades nem propaganda.",
                S(Cu(Cdm, "RotatingLockScreenOverlayEnabled", 0), Cu(Cdm, "SubscribedContent-338387Enabled", 0)), rec: true),
            T("tips", W, N, "Privacidade", "Sem dicas e sugestões do Windows", "Para as notificações de dicas e o conteúdo sugerido no app Configurações.",
                S(Cu(Cdm, "SoftLandingEnabled", 0), Cu(Cdm, "SubscribedContent-338389Enabled", 0), Cu(Cdm, "SubscribedContent-338393Enabled", 0),
                  Cu(Cdm, "SubscribedContent-353694Enabled", 0), Cu(Cdm, "SubscribedContent-353696Enabled", 0)), rec: true),
            T("welcome", W, N, "Privacidade", "Sem tela de boas-vindas após atualizações", "Pula a tela que apresenta novidades e ofertas depois de grandes atualizações.",
                S(Cu(Cdm, "SubscribedContent-310093Enabled", 0)), rec: true),
            T("onedrive_ads", W, N, "Privacidade", "Sem propagandas do OneDrive no Explorer", "Remove os avisos de provedores de sincronização dentro das pastas.",
                S(Cu(Adv, "ShowSyncProviderNotifications", 0)), rec: true, explorer: true),
            T("search_cloud", W, N, "Privacidade", "Sem destaques e pesquisa na nuvem", "Tira os \"destaques da pesquisa\" e a busca em contas Microsoft/trabalho da caixa de pesquisa.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0), Cu(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsAADCloudSearchEnabled", 0),
                  Cu(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsMSACloudSearchEnabled", 0)), rec: true),
            T("copilot_button", W, N, "IA", "Sem botão do Copilot na barra de tarefas", "Esconde o atalho do Copilot da barra de tarefas.",
                S(Cu(Adv, "ShowCopilotButton", 0)), rec: true, explorer: true),
            T("paint_ai", W, N, "IA", "Sem IA no Paint", "Desativa o Cocreator, o Preenchimento generativo e o Criador de imagens do Paint.",
                S(Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1), Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1),
                  Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1)), rec: true),
            T("notepad_ai", W, N, "IA", "Sem IA no Bloco de Notas", "Desativa os recursos de reescrita e resumo com IA do Bloco de Notas.",
                S(Lm(@"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1)), rec: true),
            T("edge_copilot", W, N, "IA", "Sem Copilot no Microsoft Edge", "Tira a barra lateral do Copilot do Edge e impede que ele leia o conteúdo das páginas.",
                S(Lm(Edge, "HubsSidebarEnabled", 0), Lm(Edge, "CopilotPageContext", 0), Lm(Edge, "CopilotCDPPageContext", 0)), rec: true),
            T("end_task", W, N, "Geral", "Finalizar tarefa no clique direito da barra", "Adiciona \"Finalizar tarefa\" ao clicar com o botão direito num programa na barra de tarefas.",
                S(Cu(Adv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1)), rec: true),
            T("home_gallery", W, N, "Aparência", "Explorer sem Início e Galeria", "Tira \"Início\" e \"Galeria\" do painel do Explorer e abre direto em \"Este Computador\".",
                S(Cu(@"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0),
                  Cu(@"Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}", "System.IsPinnedToNameSpaceTree", 0), Cu(Adv, "LaunchTo", 1)), explorer: true),
            T("hidden_files", W, N, "Aparência", "Mostrar arquivos ocultos", "O Explorer passa a exibir arquivos e pastas ocultos.",
                S(Cu(Adv, "Hidden", 1)), explorer: true),
            T("dark_mode", W, N, "Aparência", "Tema escuro do Windows", "Coloca o Windows e os aplicativos no modo escuro.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0), Cu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0))),
            T("scrollbars", W, N, "Aparência", "Barras de rolagem sempre visíveis", "As barras de rolagem não somem quando você para de mexer o mouse.",
                S(Cu(Acc, "DynamicScrollbars", 0))),
            T("taskbar_search", W, N, "Aparência", "Esconder a pesquisa da barra de tarefas", "Remove a caixa/botão de pesquisa da barra (a pesquisa continua abrindo pelo Iniciar).",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0)), explorer: true),
            T("task_view", W, N, "Aparência", "Esconder o botão Visão de Tarefas", "Remove o botão de Visão de Tarefas da barra (Win+Tab continua funcionando).",
                S(Cu(Adv, "ShowTaskViewButton", 0)), explorer: true),
            T("start_recommended", W, N, "Aparência", "Iniciar sem a seção Recomendados", "Esconde arquivos e apps recomendados do menu Iniciar.",
                S(Lm(Pol + @"\Explorer", "HideRecommendedSection", 1), Lm(@"SOFTWARE\Microsoft\PolicyManager\current\device\Start", "HideRecommendedSection", 1),
                  Lm(@"SOFTWARE\Microsoft\PolicyManager\current\device\Education", "IsEducationEnvironment", 1)),
                warn: "Também desliga o Windows Spotlight (imagens do dia) na tela de bloqueio.", explorer: true),
            T("settings_home", W, N, "Aparência", "Configurações sem a página Início", "O app Configurações abre direto em Sistema, sem a página inicial de sugestões.",
                S(CuT(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "hide:home"))),
            T("lock_screen_off", W, N, "Aparência", "Pular a tela de bloqueio", "Vai direto para a tela de senha ao ligar e ao voltar da suspensão.",
                S(Lm(Pol + @"\Personalization", "NoLockScreen", 1))),
            T("login_blur_off", W, N, "Aparência", "Tela de login sem desfoque", "Mostra a imagem de fundo nítida na tela de login, sem o efeito acrílico.",
                S(Lm(Pol + @"\System", "DisableAcrylicBackgroundOnLogon", 1))),
            T("verbose_status", W, N, "Geral", "Mensagens detalhadas ao ligar e desligar", "Mostra o que o Windows está fazendo durante a inicialização e o desligamento.",
                S(Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1))),
            T("snap_off", W, N, "Geral", "Desligar o encaixe automático de janelas", "Arrastar uma janela para a borda não a redimensiona mais sozinha.",
                S(CuT(@"Control Panel\Desktop", "WindowArrangementActive", "0"))),
            T("long_paths", W, N, "Geral", "Permitir caminhos longos", "Libera caminhos de arquivo com mais de 260 caracteres (útil para desenvolvimento e jogos com mods).",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 1))),
            T("notification_center", W, N, "Geral", "Desligar a central de notificações e o calendário", "Remove a central de notificações e o calendário da bandeja, além dos balões de notificação.",
                S(Cu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter", 1)), warn: "Evite se você usa lembretes, alarmes ou notificações de mensagens.", explorer: true),
            T("wpbt_off", W, N, "Privacidade", "Desligar o WPBT (software da BIOS)", "Impede que o fabricante do PC execute programas gravados na BIOS a cada inicialização.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager", "DisableWpbtExecution", 1)), warn: "Evite se você usa software antirroubo do fabricante que depende da BIOS.", restart: true),
            T("edge_debloat", W, N, "Privacidade", "Microsoft Edge sem propagandas e telemetria", "Desliga recomendações, compras, recompensas, coleções e envio de dados do Edge por política.",
                S(Lm(Edge, "AlternateErrorPagesEnabled", 0), Lm(Edge, "ConfigureDoNotTrack", 1), Lm(Edge, "DefaultBrowserSettingsCampaignEnabled", 0), Lm(Edge, "DiagnosticData", 0),
                  Lm(Edge, "EdgeAssetDeliveryServiceEnabled", 0), Lm(Edge, "EdgeCollectionsEnabled", 0), Lm(Edge, "EdgeShoppingAssistantEnabled", 0), Lm(Edge, "HideFirstRunExperience", 1),
                  Lm(Edge, "MicrosoftEdgeInsiderPromotionEnabled", 0), Lm(Edge, "PersonalizationReportingEnabled", 0), Lm(Edge, "ShowMicrosoftRewards", 0),
                  Lm(Edge, "ShowRecommendationsEnabled", 0), Lm(Edge, "UserFeedbackAllowed", 0), Lm(Edge, "WalletDonationEnabled", 0), Lm(Edge, "WebWidgetAllowed", 0),
                  Lm(@"SOFTWARE\Policies\Microsoft\EdgeUpdate", "CreateDesktopShortcutDefault", 0)), rec: true),
            T("brave_debloat", W, N, "Privacidade", "Brave sem Rewards, carteira, VPN e IA", "Desliga Brave Rewards, carteira cripto, VPN, Leo IA, notícias e coleta de dados por política.",
                S(Lm(Brave, "BraveRewardsDisabled", 1), Lm(Brave, "BraveWalletDisabled", 1), Lm(Brave, "BraveVPNDisabled", 1), Lm(Brave, "BraveAIChatEnabled", 0),
                  Lm(Brave, "BraveNewsDisabled", 1), Lm(Brave, "BraveTalkDisabled", 1), Lm(Brave, "TorDisabled", 1), Lm(Brave, "BraveP3AEnabled", 0),
                  Lm(Brave, "BraveStatsPingEnabled", 0), Lm(Brave, "MetricsReportingEnabled", 0), Lm(Brave, "SafeBrowsingExtendedReportingEnabled", 0),
                  Lm(Brave, "UrlKeyedAnonymizedDataCollectionEnabled", 0))),
            T("ipv4_prefer", W, N, "Rede", "Preferir IPv4 ao IPv6", "O Windows usa IPv4 primeiro. Pode reduzir latência em redes onde o IPv6 não está bem configurado.",
                S(Lm(Tcp6, "DisabledComponents", 0x20)), warn: "Só um dos três ajustes de IPv6 fica ligado: ligar este desliga os outros.", restart: true, group: "ipv6"),
            T("teredo_off", W, N, "Rede", "Desligar o Teredo", "Desativa o túnel Teredo do IPv6, que pode adicionar latência.",
                S(Lm(Tcp6, "DisabledComponents", 0x08)), warn: "Alguns jogos (Xbox Live, chat de grupo) precisam do Teredo; desfaça se o multijogador falhar.", restart: true, group: "ipv6"),
            T("ipv6_off", W, N, "Rede", "Desligar o IPv6", "Desativa o IPv6 em todos os adaptadores de rede.",
                S(Lm(Tcp6, "DisabledComponents", 0xFF)), warn: "Evite se sua operadora usa IPv6 nativo ou se algum serviço só funciona com ele.", restart: true, group: "ipv6"),
            T("utc_clock", W, N, "Geral", "Relógio da BIOS em UTC (dual boot)", "Corrige a hora errada ao alternar entre Windows e Linux no mesmo PC.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", 1)), warn: "Só ative se você tem Linux instalado no mesmo PC."),
            T("telemetry_full", W, N, "Privacidade", "Telemetria desligada (pacote completo)", "Fala online, coleta de digitação e escrita à mão, contatos, apps mais usados e telemetria do PowerShell. Não mexe no Microsoft Defender.",
                S(Cu(@"Software\Microsoft\Input\TIPC", "Enabled", 0), Cu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                  Cu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1), Cu(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                  Cu(@"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0), Cu(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0),
                  Cu(Adv, "Start_TrackProgs", 0), Lm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                  LmT(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "POWERSHELL_TELEMETRY_OPTOUT", "1")),
                warn: "O reconhecimento de fala online (ditado por voz do Windows) fica desligado até desfazer.", rec: true),
            T("boot_name", W, N, "Geral", "Nome \"PcismoMaster\" no menu de inicialização", "Renomeia esta instalação do Windows no gerenciador de inicialização (bcdedit). Desfazer volta o nome anterior.",
                S(), new[] { X(ExtraKind.BootName, "Windows (PcismoMaster)") }),
            T("menu_delay", W, N, "Aparência", "Menus sem atraso", "Submenus e menus de contexto abrem na hora (MenuShowDelay = 0; o padrão do Windows espera 400 ms).",
                S(CuT(@"Control Panel\Desktop", "MenuShowDelay", "0")), rec: true),
            T("discord_tray", W, N, "Geral", "Discord: abrir minimizado na bandeja", "O Discord inicia escondido na bandeja em vez de desenhar a janela inteira.",
                S(), new[] { X(ExtraKind.Discord, "START_MINIMIZED", "true") }, warn: "Feche o Discord antes: ele regrava esse arquivo ao sair."),
            T("discord_skip_update", W, N, "Geral", "Discord: pular a verificação de atualização", "Abre mais rápido, sem checar atualização a cada inicialização.",
                S(), new[] { X(ExtraKind.Discord, "SKIP_HOST_UPDATE", "true") }, warn: "Evite se você quer que o Discord se atualize sozinho; feche-o antes de aplicar."),
            T("office_telemetry", W, N, "Privacidade", "Desligar a telemetria do Office", "Word, Excel e os outros apps do Office param de enviar dados de uso e diagnóstico à Microsoft.",
                S(Cu(@"Software\Policies\Microsoft\office\16.0\common", "qmenable", 0), Cu(@"Software\Policies\Microsoft\office\16.0\common", "sendcustomerdata", 0),
                  Cu(@"Software\Policies\Microsoft\office\common\clienttelemetry", "sendtelemetry", 3)), rec: true),
            T("nvidia_telemetry", W, N, "Privacidade", "Desligar a telemetria do Painel de Controle NVIDIA", "O Painel de Controle NVIDIA deixa de enviar dados de uso.",
                S(Cu(@"Software\NVIDIA Corporation\NVControlPanel2\Client", "OptInOrOutPreference", 0))),
            T("ceip_off", W, N, "Privacidade", "Desligar o Programa de Aperfeiçoamento (CEIP)", "O Windows para de coletar estatísticas de uso para o Programa de Aperfeiçoamento da Experiência do Usuário.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0), Lm(@"SOFTWARE\Policies\Microsoft\AppV\CEIP", "CEIPEnable", 0)), rec: true),
            T("experimentation_off", W, N, "Privacidade", "Sem experimentos da Microsoft", "A Microsoft deixa de ligar recursos de teste no seu Windows para medir reações.",
                S(Lm(@"SOFTWARE\Microsoft\PolicyManager\default\System\AllowExperimentation", "Value", 0)), rec: true),
            T("msrt_report_off", W, N, "Privacidade", "Ferramenta de Remoção sem relatórios", "A Ferramenta de Remoção de Software Mal-Intencionado continua rodando, mas não envia o que encontrou.",
                S(Lm(@"SOFTWARE\Policies\Microsoft\MRT", "DontReportInfectionInformation", 1)), rec: true),
            T("steps_recorder_off", W, N, "Privacidade", "Desligar o Gravador de Passos e a coleta de compatibilidade", "Remove o Gravador de Passos e a telemetria do Assistente de Compatibilidade de Programas.",
                S(Lm(Pol + @"\AppCompat", "DisableUAR", 1)), rec: true),
            T("input_insights_off", W, N, "Privacidade", "Escrita à mão e digitação sem coleta", "Sem estatísticas de digitação e sem envio de amostras de escrita à mão ou relatórios de erro dela.",
                S(Cu(@"Software\Microsoft\Input\Settings", "InsightsEnabled", 0), Lm(Pol + @"\HandwritingErrorReports", "PreventHandwritingErrorReports", 1),
                  Lm(Pol + @"\TabletPC", "PreventHandwritingDataSharing", 1))),
            T("web_search_policy", W, N, "Privacidade", "Pesquisa do Windows sem web (todos os usuários)", "Reforça para todas as contas do PC: a pesquisa não consulta a web, não usa sua localização e não mostra destaques do dia.",
                S(Lm(Pol + @"\Windows Search", "DisableWebSearch", 1), Lm(Pol + @"\Windows Search", "ConnectedSearchUseWeb", 0), Lm(Pol + @"\Windows Search", "EnableDynamicContentInWSB", 0))),
            T("language_list_off", W, N, "Privacidade", "Sites sem acesso à sua lista de idiomas", "Sites deixam de ler os idiomas instalados no Windows, um dado usado para identificar o seu PC.",
                S(Cu(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1))),
            T("settings_sync_off", W, N, "Privacidade", "Não sincronizar configurações com a conta Microsoft", "Tema, senhas, idioma e preferências deixam de ser enviados para a nuvem e copiados para outros PCs.",
                S(Lm(Pol + @"\SettingSync", "DisableSettingSync", 2), Lm(Pol + @"\SettingSync", "DisableSettingSyncUserOverride", 1)),
                warn: "Evite se você quer as mesmas configurações em vários PCs com a mesma conta Microsoft.", rec: true),
            T("spotlight_off", W, N, "Privacidade", "Desligar o Windows Spotlight e conteúdo da nuvem", "Sem imagem do dia com sugestões, sem dicas de terceiros e sem conteúdo da nuvem na barra de tarefas.",
                S(Cu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", 1), Cu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableThirdPartySuggestions", 1),
                  Cu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightOnActionCenter", 1), Cu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightOnSettings", 1),
                  Cu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightWindowsWelcomeExperience", 1), Lm(Pol + @"\CloudContent", "DisableCloudOptimizedContent", 1)),
                warn: "Se você usa a imagem do dia do Spotlight no papel de parede ou na tela de bloqueio, ela para."),
            T("finish_setup_off", W, N, "Privacidade", "Sem \"Vamos concluir a configuração do dispositivo\"", "O Windows para de abrir a tela que oferece Microsoft 365, OneDrive e outros serviços após atualizações.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0)), rec: true),
            T("start_account_notifs", W, N, "Privacidade", "Sem avisos da conta Microsoft no Iniciar", "Remove do menu Iniciar os lembretes de conta e as recomendações de sites e apps.",
                S(Cu(Adv, "Start_AccountNotifications", 0), Cu(Adv, "Start_IrisRecommendations", 0)), rec: true),
            T("autoplay_off", W, N, "Geral", "Desligar a Reprodução Automática", "Pendrives, celulares e CDs não abrem nada sozinhos ao conectar: mais seguro contra vírus de pendrive.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1)), rec: true),
            T("startup_delay_off", W, N, "Desempenho", "Programas da inicialização sem espera", "O Windows abre os programas da inicialização assim que a área de trabalho aparece, sem o atraso padrão.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0), Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "WaitForIdleState", 0))),
            T("fast_shutdown", W, N, "Desempenho", "Desligar mais rápido", "Programas e serviços travados são fechados após 2 segundos em vez de até 20 ao desligar ou reiniciar.",
                S(LmT(@"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000"), CuT(@"Control Panel\Desktop", "WaitToKillAppTimeout", "2000"),
                  CuT(@"Control Panel\Desktop", "HungAppTimeout", "2000"), CuT(@"Control Panel\Desktop", "AutoEndTasks", "1")),
                warn: "Um programa lento para salvar pode ser fechado antes de terminar: salve seu trabalho antes de desligar."),
            T("wu_no_reboot", W, N, "Geral", "Windows Update não reinicia sozinho com você usando o PC", "Atualizações que pedem reinício esperam você reiniciar; o PC não reinicia no meio de um jogo ou trabalho.",
                S(Lm(Pol + @"\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1), Lm(Pol + @"\WindowsUpdate", "AUPowerManagement", 0)), rec: true),
            T("maintenance_wake_off", W, N, "Geral", "Manutenção automática não acorda o PC", "A manutenção do Windows roda quando o PC já está ligado, sem acordá-lo de madrugada.",
                S(Lm(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "WakeUp", 0))),
            T("remote_assistance_off", W, N, "Privacidade", "Desligar a Assistência Remota", "Ninguém consegue pedir para controlar seu PC pela Assistência Remota do Windows, um recurso usado em golpes.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0), Lm(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowFullControl", 0)),
                warn: "Evite se o suporte técnico da sua empresa usa a Assistência Remota do Windows.", rec: true),
            T("ducking_off", W, N, "Geral", "Não abaixar o som durante chamadas", "Jogo e música continuam no volume normal quando o Windows detecta uma chamada (Discord, Teams, WhatsApp).",
                S(Cu(@"Software\Microsoft\Multimedia\Audio", "UserDuckingPreference", 3))),
            T("dynamic_lighting_off", W, N, "Geral", "Desligar a Iluminação Dinâmica do Windows", "O Windows deixa de controlar o RGB de teclado, mouse e fone, evitando conflito com Corsair iCUE, SteelSeries GG, Logitech G HUB e outros.",
                S(Cu(@"Software\Microsoft\Lighting", "AmbientLightingEnabled", 0), Cu(@"Software\Microsoft\Lighting", "ControlledByForegroundApp", 0))),
            T("printscreen_classic", W, N, "Geral", "Print Screen sem abrir a Ferramenta de Captura", "A tecla Print Screen volta a copiar a tela inteira, como antes, e pode ser usada por jogos e outros programas.",
                S(Cu(@"Control Panel\Keyboard", "PrintScreenKeyForSnippingEnabled", 0))),
            T("fth_off", W, N, "Desempenho", "Desligar o Heap Tolerante a Falhas (FTH)", "O Windows para de aplicar correções automáticas de memória em programas que travaram antes, que podem deixá-los mais lentos.",
                S(Lm(@"SOFTWARE\Microsoft\FTH", "Enabled", 0)), warn: "Um programa que vive travando pode travar mais. Desfaça se notar isso."),
            T("explorer_compact", W, N, "Aparência", "Explorer compacto", "Menos espaço entre os itens nas pastas: cabem mais arquivos na tela.",
                S(Cu(Adv, "UseCompactMode", 1)), explorer: true),
            T("explorer_recent_off", W, N, "Privacidade", "Explorer sem arquivos e pastas recentes", "O Acesso Rápido deixa de listar o que você abriu por último e o Windows para de registrar documentos recentes.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0), Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0),
                  Cu(Adv, "Start_TrackDocs", 0)), explorer: true),
            T("copy_details", W, N, "Geral", "Detalhes sempre visíveis ao copiar arquivos", "A janela de cópia já abre com o gráfico de velocidade e o tempo restante.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\OperationStatusManager", "EnthusiastMode", 1)), rec: true),
            T("shortcut_suffix_off", W, N, "Geral", "Atalhos novos sem \"- Atalho\" no nome", "Atalhos criados a partir de agora ficam só com o nome do programa.",
                S(CuT(@"Software\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "ShortcutNameTemplate", "\"%s.lnk\""))),
            T("context_many_files", W, N, "Geral", "Menu de contexto completo com muitos arquivos", "Ao selecionar mais de 15 arquivos, o clique direito continua mostrando Abrir, Imprimir e as outras opções.",
                S(Cu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "MultipleInvokePromptMinimum", 999))),
            T("context_cast_off", W, N, "Aparência", "Tirar \"Transmitir para dispositivo\" do clique direito", "Remove do menu de contexto a opção de transmitir mídia para TVs e outros dispositivos.",
                S(LmT(Blocked, "{7AD84985-87B4-4a16-BE58-8B72A5B390F7}", "")), rec: true, explorer: true),
            T("context_compat_off", W, N, "Aparência", "Tirar \"Solucionar problemas de compatibilidade\" do clique direito", "Remove do menu de contexto de programas a opção do solucionador de compatibilidade.",
                S(LmT(Blocked, "{1d27f844-3a1f-4410-85ac-14651078412d}", "")), explorer: true),
            T("new_bat_reg", W, N, "Geral", "Criar script .bat e .reg pelo menu Novo", "Clique direito > Novo ganha \"Arquivo em Lotes do Windows\" e \"Entradas de Registro\".",
                S(LmT(@"SOFTWARE\Classes\.bat\ShellNew", "NullFile", ""), LmT(@"SOFTWARE\Classes\.reg\ShellNew", "NullFile", "")), explorer: true),
            T("shake_off", W, N, "Geral", "Sem minimizar janelas ao chacoalhar", "Arrastar uma janela de um lado para o outro não minimiza mais todas as outras.",
                S(Cu(Adv, "DisallowShaking", 1))),
            T("peek_off", W, N, "Aparência", "Sem espiar a área de trabalho", "Passar o mouse no canto da barra de tarefas não deixa mais as janelas transparentes.",
                S(Cu(Adv, "DisablePreviewDesktop", 1)), rec: true, explorer: true),
            T("alt_tab_windows_only", W, N, "Geral", "Alt+Tab só com janelas abertas", "O Alt+Tab mostra só as janelas, sem misturar as abas do Microsoft Edge.",
                S(Cu(Adv, "MultiTaskingAltTabFilter", 3)), rec: true),
            T("start_more_pins", W, N, "Aparência", "Mais fixados no Iniciar", "O menu Iniciar mostra mais apps fixados e uma área menor de recomendados.",
                S(Cu(Adv, "Start_Layout", 1))),
            T("wallpaper_quality", W, N, "Aparência", "Papel de parede sem perda de qualidade", "O Windows para de comprimir imagens JPG usadas como papel de parede.",
                S(Cu(@"Control Panel\Desktop", "JPEGImportQuality", 100)), warn: "Vale para o próximo papel de parede escolhido."),
            T("photo_viewer_classic", W, N, "Aparência", "Visualizador de Fotos clássico do Windows", "Traz de volta o Visualizador de Fotos do Windows 7, leve e rápido, no \"Abrir com\" de JPG, PNG, GIF e BMP.",
                photo, warn: "Para virar o padrão, clique com o botão direito numa foto > Abrir com > Escolher outro aplicativo > Visualizador de Fotos do Windows > Sempre (o Windows exige essa escolha manual)."),
            T("netfx3", W, N, "Jogos", "Ativar o .NET Framework 3.5", "Instala o .NET 3.5 pelo Windows Update. Muitos jogos e programas antigos precisam dele para abrir.",
                S(), new[] { X(ExtraKind.FeatureOn, "NetFx3") }, warn: "Precisa de internet e pode levar alguns minutos.", restart: true),
            T("ps_remotesigned", W, N, "Geral", "Permitir scripts PowerShell (RemoteSigned)", "Scripts .ps1 criados no PC rodam normalmente; scripts baixados da internet continuam exigindo assinatura.",
                S(LmT(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy", "RemoteSigned")), warn: "Só ative se você usa scripts PowerShell. É o padrão do Windows Server."),

            // ---- Energia: menos economia, resposta mais imediata (desktop) ----
            T("power_throttling_off", W, N, "Energia", "Desligar a limitação de energia de programas", "O Windows para de baixar o clock da CPU para programas em segundo plano. Tudo responde na hora, com um pouco mais de consumo.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1)),
                warn: "Evite em notebook na bateria: a autonomia cai.", restart: true),
            T("timer_coalescing_off", W, N, "Energia", "Não agrupar os timers do Windows", "Os timers disparam na hora marcada, em vez de serem juntados para o processador dormir mais.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "CoalescingTimerInterval", 0)),
                warn: "Evite em notebook na bateria: o processador acorda mais vezes.", restart: true),
            T("dynamic_tick_off", W, N, "Energia", "Desligar o dynamic tick", "O relógio do sistema bate num ritmo fixo, sem pular batidas quando o PC está ocioso. Deixa o tempo entre quadros mais regular.",
                S(), new[] { X(ExtraKind.Bcd, "disabledynamictick", "yes") },
                warn: "Evite em notebook na bateria. Se o PC usa BitLocker, tenha a chave de recuperação à mão: mexer na inicialização pode pedir a chave.", restart: true),

            // ---- Rede: placa de rede e protocolos ----
            T("nic_interrupt_moderation_off", W, N, "Rede", "Desligar a moderação de interrupção da placa de rede", "A placa avisa o processador a cada pacote, em vez de juntar vários. Pacotes pequenos de jogo chegam antes.",
                S(), new[] { X(ExtraKind.NicProp, "*InterruptModeration", "0") },
                warn: "Usa um pouco mais de processador em downloads grandes. A conexão cai por alguns segundos ao aplicar."),
            T("netbios_off", W, N, "Rede", "Desligar o NetBIOS sobre TCP/IP", "Desliga o NetBIOS em todas as interfaces de rede: menos tráfego de broadcast e menos portas abertas.",
                S(), new[] { X(ExtraKind.NetBios) },
                warn: "Evite se você acessa pastas compartilhadas ou impressoras pelo nome em PCs muito antigos da rede local."),

            // ---- Memória ----
            T("page_combining_off", W, N, "Desempenho", "Desligar a combinação de páginas de memória", "O Windows para de procurar páginas iguais na RAM para juntá-las. Economiza processador com o PC parado.",
                S(), new[] { X(ExtraKind.PageCombining) },
                warn: "Evite com 8 GB de RAM ou menos: juntar páginas iguais economiza memória.", restart: true),

            // ===================== MOUSE =====================
            T("input_mouse", M, N, "Jogos", "Mouse sem atrasos", "Cursor 1:1 sem aceleração, dica ao parar o mouse em 10 ms, sem rastro, sem som e sem foco ao passar por cima; curvas de movimento padrão.",
                S(CuT(@"Control Panel\Mouse", "MouseSpeed", "0"), CuT(@"Control Panel\Mouse", "MouseThreshold1", "0"), CuT(@"Control Panel\Mouse", "MouseThreshold2", "0"),
                  CuT(@"Control Panel\Mouse", "MouseSensitivity", "10"), CuT(@"Control Panel\Mouse", "MouseHoverTime", "10"), CuT(@"Control Panel\Mouse", "MouseTrails", "0"),
                  CuT(@"Control Panel\Mouse", "Beep", "No"), CuT(@"Control Panel\Mouse", "ExtendedSounds", "No"), Cu(@"Control Panel\Mouse", "ActiveWindowTracking", 0),
                  CuT(@"Control Panel\Mouse", "SnapToDefaultButton", "0"),
                  CuB(@"Control Panel\Mouse", "SmoothMouseXCurve", "0000000000000000156E000000000000004001000000000029DC0300000000000000280000000000"),
                  CuB(@"Control Panel\Mouse", "SmoothMouseYCurve", "0000000000000000FD11010000000000002404000000000000FC12000000000000C0BB010000000000")), rec: true),

            // ===================== TECLADO =====================
            T("sticky_hotkey_off", K, N, "Jogos", "Sem atalho de Teclas de Aderência", "Apertar Shift cinco vezes no meio do jogo não abre mais a janela de acessibilidade.",
                S(CuT(Acc + @"\StickyKeys", "Flags", "506")), rec: true),
            T("keyboard_fast", K, N, "Jogos", "Teclado com resposta mais rápida", "Menor atraso antes de a tecla começar a repetir e repetição na velocidade máxima.",
                S(CuT(@"Control Panel\Keyboard", "KeyboardDelay", "0"), CuT(@"Control Panel\Keyboard", "KeyboardSpeed", "31")),
                warn: "Evite se você prefere o ritmo padrão de digitação: teclas repetem mais cedo."),
            T("numlock_on", K, N, "Geral", "Num Lock ligado ao iniciar", "O teclado numérico já começa ativo na tela de login e na sessão.",
                S(CuT(@"Control Panel\Keyboard", "InitialKeyboardIndicators", "2"), new Setting("HKU", @".DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators", 0, "2"))),
            T("input_keyboard", K, N, "Jogos", "Teclado sem filtros de acessibilidade", "Desliga Teclas de Alternância, de Filtragem e do Mouse, e os atalhos que as ativam no meio do jogo. Use junto com \"Sem atalho de Teclas de Aderência\".",
                S(CuT(Acc + @"\ToggleKeys", "Flags", "58"), CuT(Acc + @"\Keyboard Response", "Flags", "122"), CuT(Acc + @"\Keyboard Response", "AutoRepeatDelay", "0"),
                  CuT(Acc + @"\Keyboard Response", "AutoRepeatRate", "0"), CuT(Acc + @"\Keyboard Response", "BounceTime", "0"), CuT(Acc + @"\Keyboard Response", "DelayBeforeAcceptance", "0"),
                  Cu(Acc + @"\Keyboard Response", "Last BounceKey Setting", 0), Cu(Acc + @"\Keyboard Response", "Last Valid Delay", 0), Cu(Acc + @"\Keyboard Response", "Last Valid Repeat", 0),
                  Cu(Acc + @"\Keyboard Response", "Last Valid Wait", 1000), CuT(Acc + @"\MouseKeys", "Flags", "130"))),
            T("alt_shift_off", K, N, "Jogos", "Sem troca de idioma por Alt+Shift", "Alt+Shift e Ctrl+Shift deixam de trocar o idioma do teclado sem querer no meio do jogo. A troca continua pelo Win+Espaço e pela barra de tarefas.",
                S(CuT(@"Keyboard Layout\Toggle", "Hotkey", "3"), CuT(@"Keyboard Layout\Toggle", "Language Hotkey", "3"), CuT(@"Keyboard Layout\Toggle", "Layout Hotkey", "3")),
                warn: "Evite se você troca de idioma pelo Alt+Shift de propósito. Vale após sair e entrar na conta."),
            T("narrator_hotkey_off", K, N, "Jogos", "Sem atalho do Narrador (Ctrl+Win+Enter)", "O Narrador não abre mais por acidente com Ctrl+Win+Enter.",
                S(Cu(@"Software\Microsoft\Narrator\NoRoam", "WinEnterLaunchEnabled", 0)), rec: true),

            // ===================== GPU =====================
            T("pcie_aspm", G, N, "Desempenho", "Desligar a economia de energia do PCI Express", "O link entre placa de vídeo, SSD e processador deixa de entrar em economia, reduzindo latência.",
                S(), new[] { X(ExtraKind.Power, "501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", "0") },
                warn: "Evite em notebook na bateria: consome um pouco mais.", rec: true),
            T("refresh_rate", G, N, "Jogos", "Monitor na taxa de atualização máxima", "Coloca cada monitor no maior Hz que ele aceita na resolução atual (muita gente joga a 60 Hz sem saber).",
                S(), new[] { X(ExtraKind.RefreshMax) }, rec: true),
            T("hags", G, N, "Jogos", "Agendamento de GPU acelerado por hardware", "A placa de vídeo gerencia a própria fila de quadros, reduzindo a latência. Vale após reiniciar.",
                S(Lm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)), warn: "Evite se notar travadas em gravação/transmissão depois de ativar; desfaça e compare.", rec: true, restart: true),

            // ===================== Formatação (extra) =====================
            T("new_users", W, N, "Geral", "Aplicar também para contas criadas depois", "Copia os ajustes de usuário já aplicados (tema, Explorer, privacidade, mouse, teclado…) para o perfil padrão: contas novas neste PC já nascem com eles.",
                S(), new[] { X(ExtraKind.NewUsers) }),
        };
    }

    /// <summary>Volta o Visualizador de Fotos do Windows para o "Abrir com" (as chaves que o Windows 10/11 deixou de criar).</summary>
    private static Setting[] PhotoViewer()
    {
        const string cls = @"SOFTWARE\Classes\PhotoViewer.FileAssoc.";
        const string cmd = "%SystemRoot%\\System32\\rundll32.exe \"%ProgramFiles%\\Windows Photo Viewer\\PhotoViewer.dll\", ImageView_Fullscreen %1";
        var list = new List<Setting>();
        foreach (var (type, icon, name) in new[] { ("Bitmap", @"%SystemRoot%\System32\imageres.dll,-70", "-3056"), ("JFIF", @"%SystemRoot%\System32\imageres.dll,-72", "-3055"),
                     ("Jpeg", @"%SystemRoot%\System32\imageres.dll,-72", "-3055"), ("Gif", @"%SystemRoot%\System32\imageres.dll,-83", "-3057"),
                     ("Png", @"%SystemRoot%\System32\imageres.dll,-71", "-3057"), ("Wdp", @"%SystemRoot%\System32\wmphoto.dll,-400", "") })
        {
            list.Add(Lm(cls + type, "ImageOptionFlags", 1));
            if (name.Length > 0) list.Add(LmX(cls + type, "FriendlyTypeName", @"@%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll," + name));
            list.Add(LmT(cls + type + @"\DefaultIcon", "", icon));
            list.Add(LmX(cls + type + @"\shell\open\command", "", cmd));
            list.Add(LmT(cls + type + @"\shell\open\DropTarget", "Clsid", "{FFE2A43C-56B9-4bf5-9A79-CC6D4285608A}"));
        }
        const string assoc = @"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations";
        foreach (var (ext, type) in new[] { (".bmp", "Bitmap"), (".dib", "Bitmap"), (".gif", "Gif"), (".jfif", "JFIF"), (".jpe", "Jpeg"), (".jpeg", "Jpeg"),
                     (".jpg", "Jpeg"), (".jxr", "Wdp"), (".png", "Png"), (".tif", "Tiff"), (".tiff", "Tiff"), (".wdp", "Wdp") })
            list.Add(LmT(assoc, ext, "PhotoViewer.FileAssoc." + type));
        return list.ToArray();
    }

    public static OptTweak? ById(string id) => All.FirstOrDefault(t => t.Id == id);
    public static IEnumerable<OptTweak> For(OptPage page) => All.Where(t => t.Page == page && t.Id != "new_users");

    public static string LevelText(OptLevel l) => l switch { OptLevel.Essencial => "Essencial", OptLevel.Recomendado => "Recomendado", OptLevel.Avancado => "Avançado", _ => "" };

    public static readonly string[] Categories = { "Geral", "Aparência", "Desempenho", "Energia", "Privacidade", "Jogos", "Rede", "IA" };

    public static readonly OptProfile[] Profiles =
    {
        new("basico", "PcismoMaster Básico",
            new[] { "Windows leve, sem telemetria nem IA", "Serviços inúteis em modo manual", "Teclado e mouse sem atraso", "Prioridade para jogos e para a janela ativa", "Ponto de restauração e limpeza de temporários" },
            new[] { "telemetry", "diagtrack", "telemetry_tasks", "data_tasks_off", "compat_telemetry", "silent_apps", "widgets", "chat_off", "copilot", "recall", "click_to_do",
                    "edge_preload", "delivery_p2p", "bing", "cdm_full", "start_suggestions", "tips", "lock_screen", "welcome", "ad_id", "activity", "tailored", "feedback",
                    "background_apps", "services_manual", "tracking_services_off", "input_mouse", "sticky_hotkey_off", "narrator_hotkey_off",
                    "win32_priority", "game_mode", "games_priority", "system_responsiveness", "network_throttling" }, Cleanup: true),
        new("sem_ia", "Sem IA",
            new[] { "Copilot e botão do Copilot desligados", "Recall e Clique para Fazer desligados", "Sem IA no Paint, Bloco de Notas e Edge", "Pesquisa sem Bing e sem nuvem" },
            new[] { "copilot", "copilot_button", "recall", "click_to_do", "paint_ai", "notepad_ai", "edge_copilot", "bing", "search_cloud", "settings_agent_off" }, Cleanup: false),
    };
}
