using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using Microsoft.Win32;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>Um ajuste de placa de vídeo: interruptor na tela, valores de registro com backup e desfazer.</summary>
/// <param name="Vendor">nvidia | amd | intel | all</param>
public sealed record GfxTweak(string Id, string Vendor, string Title, string Description, bool Advanced, bool DefaultOn, Func<Setting[]> Build);

/// <summary>
/// "Importar configurações" do "5 Graphics/3 Driver Updated Install &amp; Settings" do Ultimate (MIT), com backup de cada
/// valor (ModuleEngine) e desfazer. Ficam de fora: aceitar o EULA da AMD por você e o "Preferred OpenGL GPU" fixo
/// de uma RTX 4090. Os que podem incomodar (HDCP, P-state fixo, G-SYNC) vêm desligados.
/// </summary>
public static class GraphicsTweaks
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string NvTweak = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\Global\NVTweak";

    public static readonly ModuleEngine Engine = new(new RegistryStore(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups"));

    // ---------------- Onde estão as chaves ----------------

    /// <summary>Chaves das placas de vídeo (Class\{display}\0000, 0001…), opcionalmente só de um fabricante.</summary>
    private static List<string> Adapters(string? vendor = null)
    {
        var list = new List<string>();
        using var root = Registry.LocalMachine.OpenSubKey(DisplayClass);
        if (root == null) return list;
        foreach (var name in root.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
        {
            using var k = root.OpenSubKey(name);
            string id = ((k?.GetValue("MatchingDeviceId") as string) ?? "") + " " + ((k?.GetValue("ProviderName") as string) ?? "");
            bool match = vendor switch
            {
                "nvidia" => id.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) || id.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase),
                "amd" => id.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase) || id.Contains("Advanced Micro", StringComparison.OrdinalIgnoreCase),
                "intel" => id.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase) || id.Contains("Intel", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };
            if (match) list.Add($@"{DisplayClass}\{name}");
        }
        return list;
    }

    /// <summary>Subchaves com um nome (ex.: UMD, power_v1) dentro das chaves das placas de um fabricante.</summary>
    private static List<string> SubKeysNamed(string vendor, string name)
    {
        var list = new List<string>();
        foreach (var adapter in Adapters(vendor))
        {
            void Walk(string path, int depth)
            {
                using var k = Registry.LocalMachine.OpenSubKey(path);
                if (k == null || depth > 3) return;
                foreach (var sub in k.GetSubKeyNames())
                {
                    string p = path + "\\" + sub;
                    if (sub.Equals(name, StringComparison.OrdinalIgnoreCase)) list.Add(p);
                    else Walk(p, depth + 1);
                }
            }
            Walk(adapter, 0);
        }
        return list;
    }

    private static Setting[] Each(IEnumerable<string> keys, string name, int value) => keys.Select(k => new Setting("HKLM", k, name, value)).ToArray();
    private static Setting[] EachHex(IEnumerable<string> keys, string name, string hex) => keys.Select(k => new Setting("HKLM", k, name, 0, Hex: hex)).ToArray();
    private static Setting Lm(string path, string name, int value) => new("HKLM", path, name, value);
    private static Setting Cu(string path, string name, int value) => new("HKCU", path, name, value);
    private static Setting CuText(string path, string name, string text) => new("HKCU", path, name, 0, text);

    private static string[] MonitorKeys()
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore");
        return k?.GetSubKeyNames().Select(n => $@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore\{n}").ToArray() ?? Array.Empty<string>();
    }

    private static string[] DisplayDeviceIds()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='Display'");
            return s.Get().Cast<ManagementObject>().Select(o => { using (o) return o["PNPDeviceID"] as string ?? ""; }).Where(x => x.Length > 0).ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private static string[] TrayIconKeys()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
        return k?.GetSubKeyNames().Select(n => $@"Control Panel\NotifyIconSettings\{n}").ToArray() ?? Array.Empty<string>();
    }

    // ---------------- Catálogo ----------------

    public static readonly GfxTweak[] All =
    {
        // NVIDIA
        new("gfx-nv-physx", "nvidia", "PhysX na placa de vídeo", "Fixa o PhysX na GPU NVIDIA em vez de deixar o driver escolher.", false, true,
            () => new[] { Lm(NvTweak, "NvCplPhysxAuto", 0) }),
        new("gfx-nv-devtools", "nvidia", "Configurações de desenvolvedor no painel", "Mostra o menu Desenvolvedor do Painel de Controle NVIDIA.", false, true,
            () => new[] { Lm(NvTweak, "NvDevToolsVisible", 1) }),
        new("gfx-nv-counters", "nvidia", "Contadores de desempenho para todos os usuários", "Libera os contadores da GPU para ferramentas de monitoramento sem modo administrador.", false, true,
            () => Each(Adapters("nvidia"), "RmProfilingAdminOnly", 0).Append(Lm(NvTweak, "RmProfilingAdminOnly", 0)).ToArray()),
        new("gfx-nv-tray", "nvidia", "Sem ícone da NVIDIA na bandeja", "Não abre o ícone de notificações da NVIDIA ao entrar no Windows.", false, true,
            () => new[] { Cu(@"Software\NVIDIA Corporation\NvTray", "StartOnLogin", 0) }),
        new("gfx-nv-sharpen", "nvidia", "Nitidez de imagem clássica", "Volta a nitidez antiga (\"Image Sharpening\") no Painel de Controle NVIDIA.", false, true,
            () => new[] { Lm(@"SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS", "EnableGR535", 0), Lm(@"SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS", "EnableGR535", 0) }),
        new("gfx-nv-pstate", "nvidia", "Desligar o P-state dinâmico", "A placa fica sempre no clock máximo, até parada: pode tirar microtravadas, mas aumenta consumo e calor em repouso.", true, false,
            () => Each(Adapters("nvidia"), "DisableDynamicPstate", 1)),
        new("gfx-nv-hdcp", "nvidia", "Desligar o HDCP", "Remove a proteção de conteúdo do driver. ATENÇÃO: Netflix, Disney+, Prime Video e Blu-ray param de tocar em alta qualidade (ou nada) no PC.", true, false,
            () => Each(Adapters("nvidia"), "RMHdcpKeyglobZero", 1)),
        // AMD
        new("gfx-amd-update", "amd", "Atualização manual do AMD Software", "O Adrenalin não procura atualizações sozinho.", false, true,
            () => new[] { Cu(@"Software\AMD\CN", "AutoUpdate", 0) }),
        new("gfx-amd-profile", "amd", "Perfil gráfico personalizado", "Deixa o Adrenalin no perfil \"Personalizado\" (respeita os ajustes abaixo).", false, true,
            () => new[] { CuText(@"Software\AMD\CN", "WizardProfile", "PROFILE_CUSTOM") }),
        new("gfx-amd-antilag", "amd", "Radeon Anti-Lag ligado", "Reduz o atraso de entrada nos jogos.", false, true,
            () => Each(Adapters("amd"), "KMD_DeLagEnabled", 1)),
        new("gfx-amd-vsync", "amd", "Aguardar atualização vertical: sempre desligado", "VSync forçado desligado pelo driver.", false, true,
            () => EachHex(SubKeysNamed("amd", "UMD"), "VSyncControl", "3000")),
        new("gfx-amd-tfq", "amd", "Filtragem de textura: desempenho", "Qualidade de filtragem de textura no modo desempenho.", false, true,
            () => EachHex(SubKeysNamed("amd", "UMD"), "TFQ", "3200")),
        new("gfx-amd-tess", "amd", "Tesselação: substituir o jogo, nível máximo desligado", "Ignora a tesselação do jogo (pode mudar o visual em alguns jogos).", false, true,
            () => SubKeysNamed("amd", "UMD").SelectMany(k => new[] { new Setting("HKLM", k, "Tessellation", 0, Hex: "3100"), new Setting("HKLM", k, "Tessellation_OPTION", 0, Hex: "3200") }).ToArray()),
        new("gfx-amd-varibright", "amd", "Vari-Bright: brilho máximo", "Desliga a redução automática de brilho (notebooks AMD).", false, true,
            () => EachHex(SubKeysNamed("amd", "power_v1"), "abmlevel", "00000000")),
        new("gfx-amd-ui", "amd", "AMD Software mais limpo", "Sem menu na bandeja, sem notificações e sem animações no Adrenalin.", false, true,
            () => new[]
            {
                CuText(@"Software\AMD\CN", "SystemTray", "false"), CuText(@"Software\AMD\CN", "CN_Hide_Toast_Notification", "true"),
                CuText(@"Software\AMD\CN", "AnimationEffect", "false"), Cu(@"Software\AMD\CN\FreeSync", "AlreadyNotified", 1),
                Cu(@"Software\AMD\CN\OverlayNotification", "AlreadyNotified", 1), Cu(@"Software\AMD\CN\VirtualSuperResolution", "AlreadyNotified", 1),
            }),
        // Intel
        new("gfx-intel-vsync", "intel", "Sincronização de quadros: VSync desligado", "VSync forçado desligado no driver Intel.", false, true,
            () => Each(Adapters("intel").Select(a => a + @"\3DKeys"), "Global_AsyncFlipMode", 2)),
        new("gfx-intel-lowlat", "intel", "Modo de baixa latência ligado", "Reduz o atraso de entrada no driver Intel.", false, true,
            () => Each(Adapters("intel").Select(a => a + @"\3DKeys"), "Global_LowLatency", 1)),
        new("gfx-intel-vrr", "intel", "Desligar a taxa variável global do Windows", "Evita cintilação e conflito com o driver (otimizações para jogos em janela continuam ligadas).", false, true,
            () => new[] { CuText(@"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;") }),
        // Todas
        new("gfx-all-acm", "all", "Desligar o gerenciamento automático de cor", "O Windows não ajusta as cores dos apps sozinho (evita cores lavadas em alguns monitores).", false, true,
            () => Each(MonitorKeys(), "AutoColorManagementEnabled", 0)),
        new("gfx-all-msi", "all", "Modo MSI nas placas de vídeo", "Interrupções por mensagem (MSI), com menos latência. Vale depois de reiniciar.", false, true,
            () => DisplayDeviceIds().Select(id => Lm($@"SYSTEM\CurrentControlSet\Enum\{id}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties", "MSISupported", 1)).ToArray()),
        new("gfx-all-tray", "all", "Mostrar todos os ícones da bandeja", "Mostra na barra de tarefas os ícones que o Windows esconde (não é de vídeo, mas faz parte do script).", false, true,
            () => TrayIconKeys().Select(k => Cu(k, "IsPromoted", 1)).ToArray()),
    };

    /// <summary>O módulo do ajuste: se já foi aplicado, usa as chaves gravadas no backup (para desfazer exatamente o que mudou).</summary>
    public static Module ModuleFor(GfxTweak t)
    {
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", t.Id + ".json");
        Setting[] settings = File.Exists(file) && JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file)) is { } saved ? saved.Settings : t.Build();
        return new Module(t.Id, t.Title, "Placa de vídeo", t.Description, "5 Graphics/3 Driver Updated Install & Settings.ps1", settings);
    }

    public static bool IsApplied(GfxTweak t) => Engine.HasBackup(ModuleFor(t));

    /// <summary>
    /// Reinstalar o driver (principalmente "limpo" ou depois do DDU) apaga os valores, mas o backup continua e o ajuste
    /// apareceria como APLICADO sem estar. Se os valores não batem mais, o backup antigo é descartado (ele é de outro driver)
    /// e o ajuste volta a "não aplicado". Devolve true quando descartou.
    /// </summary>
    public static bool DiscardIfStale(GfxTweak t)
    {
        var m = ModuleFor(t);
        if (!Engine.HasBackup(m) || Engine.Matches(m)) return false;
        File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", t.Id + ".json"));
        return true;
    }

    /// <summary>O mesmo para o perfil global: se o driver novo não tem mais os valores aplicados, descarta o backup.</summary>
    public static bool DiscardDrsIfStale(string id, (uint Id, uint Value, string Name)[] expected)
    {
        if (!DrsApplied(id) || !NvDrs.Available) return false;
        var want = expected.ToDictionary(x => x.Id, x => x.Value);
        var backup = JsonSerializer.Deserialize<List<DrsBackup>>(File.ReadAllText(DrsBackupFile(id))) ?? new();
        bool stale = NvDrs.Session(s =>
        {
            IntPtr p = NvDrs.BaseProfile(s);
            return backup.Any(b => want.TryGetValue(b.Id, out uint v)
                                   && (NvDrs.Get(s, p, b.Id) is not { Location: DrsLocation.Current } cur || (uint)cur.Number != v));
        });
        if (stale) File.Delete(DrsBackupFile(id));
        return stale;
    }

    // ---------------- Perfil global da NVIDIA (o .nip do script) ----------------

    /// <summary>As configurações do .nip do script para o perfil global (sem o "Preferred OpenGL GPU" de uma RTX 4090).</summary>
    public static readonly (uint Id, uint Value, string Name)[] NipSettings =
    {
        (277041154, 0, "Frame Rate Limiter V3"), (8102046, 1, "Maximum Pre-Rendered Frames"), (6600001, 1, "Preferred Refresh Rate"),
        (390467, 1, "Ultra Low Latency - CPL State"), (277041152, 0, "Ultra Low Latency - Enabled"), (11041231, 138504007, "Vertical Sync"),
        (270198627, 0, "Vertical Sync - Smooth AFR Behavior"), (5912412, 2525368439, "Vertical Sync - Tear Control"),
        (550932728, 0, "Vulkan/OpenGL Present Method"), (276652957, 0, "Antialiasing - Gamma Correction"), (276757595, 1, "Antialiasing - Mode"),
        (282555346, 0, "Antialiasing - Setting"), (8703344, 1, "Anisotropic Filter - Optimization"), (15151633, 1, "Anisotropic Filter - Sample Optimization"),
        (282245910, 1, "Anisotropic Filtering - Mode"), (270426537, 1, "Anisotropic Filtering - Setting"), (1686376, 0, "Texture Filtering - Negative LOD Bias"),
        (13510289, 20, "Texture Filtering - Quality"), (3066610, 0, "Texture Filtering - Trilinear Optimization"), (1343646814, 0, "CUDA - Force P2 State"),
        (283962569, 1, "CUDA - Sysmem Fallback Policy"), (274197361, 1, "Power Management - Mode"), (11306135, 4294967295, "Shader Cache - Cache Size"),
        (549528094, 1, "Threaded Optimization"), (544392611, 0, "OpenGL GDI Compatibility"),
    };

    /// <summary>O .nip do script ajustado à placa deste PC (regras em <see cref="GpuRules.Adapt"/>).</summary>
    public static (uint Id, uint Value, string Name)[] NipFor(GpuCard? card) => GpuRules.Adapt(NipSettings, card);

    /// <summary>G-SYNC desligado no perfil global (separado: quem tem monitor G-SYNC normalmente quer manter).</summary>
    public static readonly (uint Id, uint Value, string Name)[] GsyncOff =
    {
        (294973784, 0, "GSYNC - Application Mode"), (279476687, 4, "GSYNC - Application State"), (278196567, 0, "GSYNC - Global Feature"),
        (278196727, 0, "GSYNC - Global Mode"), (268604728, 0, "GSYNC - Indicator Overlay"),
    };

    private sealed record DrsBackup(uint Id, bool WasUser, ulong Value, string? Text = null);

    /// <summary>"GPU com renderização OpenGL": o perfil recomendado tira a placa fixa (o script fixava a 4090 dele) e deixa em Seleção automática.</summary>
    public const uint OpenGlGpu = 0x20D0F3E6;
    private static string DrsBackupFile(string id) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", id + ".drs.json");

    public static bool DrsApplied(string id) => File.Exists(DrsBackupFile(id));

    /// <summary>Grava no perfil global, guardando antes o que cada configuração tinha (valor seu ou herdado).</summary>
    /// <summary>Aplica pelo nome do interruptor: "gfx-nv-drs" (perfil recomendado, ajustado à placa) ou "gfx-nv-gsync".</summary>
    public static void ApplyDrsById(string id)
    {
        if (DrsApplied(id)) return;
        ApplyDrs(id, id == "gfx-nv-gsync" ? GsyncOff : NipFor(GpuDetect.DetectNvidia()));
    }

    /// <summary>
    /// Depois de instalar o driver com o app aberto, a NVAPI deste processo pode ainda não enxergar a placa: tenta algumas
    /// vezes e, se não der, aplica num processo novo do próprio PcismoMaster (--apply-drs). Devolve null se deu certo, ou o erro.
    /// </summary>
    public static async Task<string?> ApplyDrsRobustAsync(string id)
    {
        for (int i = 0; i < 3; i++)
        {
            try { ApplyDrsById(id); return null; }
            catch (Exception ex) when (i < 2) { DriverClean.Log($"Perfil NVIDIA ({id}), tentativa {i + 1}: {ex.Message}"); await Task.Delay(4000); }
            catch (Exception ex) { DriverClean.Log($"Perfil NVIDIA ({id}), tentativa {i + 1}: {ex.Message}; tentando num processo novo."); }
        }
        string? exe = Environment.ProcessPath;
        if (exe == null) return "não foi possível abrir o ajudante";
        using var p = Process.Start(new ProcessStartInfo(exe, $"--apply-drs {id}") { UseShellExecute = false, CreateNoWindow = true })!;
        await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        return DrsApplied(id) ? null : "a NVIDIA não aceitou o perfil agora; reinicie o PC e clique em Aplicar";
    }

    public static void ApplyDrs(string id, (uint Id, uint Value, string Name)[] settings)
    {
        if (DrsApplied(id)) throw new InvalidOperationException("Já aplicado. Desfaça antes de aplicar de novo.");
        NvDrs.Session(s =>
        {
            IntPtr p = NvDrs.BaseProfile(s);
            var backup = settings.Select(x => NvDrs.Get(s, p, x.Id) is { Location: DrsLocation.Current, IsPredefined: false } v
                ? new DrsBackup(x.Id, true, v.Number) : new DrsBackup(x.Id, false, 0)).ToList();
            // Perfil recomendado: placa OpenGL em Seleção automática (guarda a fixada, se houver, para o Desfazer).
            bool resetGl = false;
            if (id == "gfx-nv-drs" && NvDrs.Get(s, p, OpenGlGpu) is { Location: DrsLocation.Current, IsPredefined: false, Text: not null } gl)
            {
                backup.Add(new DrsBackup(OpenGlGpu, true, 0, gl.Text));
                resetGl = true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(DrsBackupFile(id))!);
            File.WriteAllText(DrsBackupFile(id), JsonSerializer.Serialize(backup));
            foreach (var x in settings) NvDrs.Set(s, p, x.Id, DrsType.Dword, x.Value, null);
            if (resetGl) NvDrs.Delete(s, p, OpenGlGpu);
            return 0;
        }, save: true);
    }

    /// <summary>Volta cada configuração ao que era: o seu valor anterior ou "herdado" (tira do perfil).</summary>
    public static void RestoreDrs(string id)
    {
        var backup = JsonSerializer.Deserialize<List<DrsBackup>>(File.ReadAllText(DrsBackupFile(id))) ?? new();
        NvDrs.Session(s =>
        {
            IntPtr p = NvDrs.BaseProfile(s);
            foreach (var b in backup)
                if (b.WasUser && b.Text != null) NvDrs.Set(s, p, b.Id, DrsType.WString, 0, b.Text);
                else if (b.WasUser) NvDrs.Set(s, p, b.Id, DrsType.Dword, b.Value, null);
                else NvDrs.Delete(s, p, b.Id);
            return 0;
        }, save: true);
        File.Delete(DrsBackupFile(id));
    }

    // ---------------- Driver NVIDIA silencioso ----------------

    /// <summary>Instala o driver NVIDIA baixado (assinatura já conferida) em modo silencioso e limpo, como o script.</summary>
    public static async Task<int> InstallNvidiaSilentAsync(string installer, CancellationToken ct)
    {
        using var p = Process.Start(new ProcessStartInfo(installer, "-s -noreboot -noeula -clean") { UseShellExecute = false, CreateNoWindow = true })!;
        await p.WaitForExitAsync(ct);
        try { var old = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "NVIDIA"); if (Directory.Exists(old)) Directory.Delete(old, true); } catch { }
        return p.ExitCode;
    }

    /// <summary>O Painel de Controle NVIDIA (app da Microsoft Store) está instalado?</summary>
    public static bool HasNvidiaControlPanel()
    {
        string apps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
        try { return Directory.EnumerateDirectories(apps, "NVIDIACorp.NVIDIAControlPanel_*").Any(); }
        catch { return true; } // sem permissão para listar: não insiste
    }

    public const string NvidiaControlPanelStore = "ms-windows-store://pdp/?ProductId=9NF8H0H7WMLT";
    private const string NvidiaControlPanelApp = @"shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel";

    /// <summary>Abre o Painel de Controle NVIDIA; se não estiver instalado, a página dele na Microsoft Store. Devolve se abriu o painel.</summary>
    public static bool OpenNvidiaControlPanel()
    {
        bool has = HasNvidiaControlPanel();
        Process.Start(new ProcessStartInfo(has ? "explorer.exe" : NvidiaControlPanelStore) { Arguments = has ? NvidiaControlPanelApp : "", UseShellExecute = true });
        return has;
    }

    /// <summary>Atalho "NVIDIA Control Panel" na Área de Trabalho (como o script "5 Nvidia Settings").</summary>
    public static void CreateNvidiaControlPanelShortcut()
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell indisponível.");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic lnk = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NVIDIA Control Panel.lnk"));
        lnk.TargetPath = NvidiaControlPanelApp;
        lnk.Save();
    }

    /// <summary>
    /// "NVIDIA Settings: Default" do script: o perfil global volta ao padrão do driver (tudo o que foi mudado nele sai, não só o nosso).
    /// Os backups do perfil recomendado e do G-SYNC deixam de valer e são apagados.
    /// </summary>
    public static void ResetGlobalDrs(params string[] backupIds)
    {
        NvDrs.Session(s =>
        {
            IntPtr p = NvDrs.BaseProfile(s);
            try { NvDrs.RestoreProfile(s, p); }
            catch (DrsException)
            {
                foreach (var v in NvDrs.ProfileSettings(s, p).Where(v => v.Location == DrsLocation.Current && !v.IsPredefined))
                    NvDrs.Delete(s, p, v.Id);
            }
            return 0;
        }, save: true);
        foreach (var id in backupIds) if (DrsApplied(id)) File.Delete(DrsBackupFile(id));
    }
}
