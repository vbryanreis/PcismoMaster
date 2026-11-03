using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ResetExplode.Modular;

namespace ResetExplode;


/// <summary>
/// Instalação enxuta do driver de vídeo ("5 Graphics/4 Driver Debloat Install &amp; Settings" do Ultimate, MIT):
/// abre o pacote do driver com o 7-Zip, tira os componentes escolhidos e instala em silêncio. Diferenças do script:
/// cada componente é opcional (o que quebra algo vem marcado para manter), serviços são DESATIVADOS com backup em vez
/// de apagados, entradas de inicialização saem com backup, e os ajustes do driver ficam no cartão de configurações.
/// </summary>
public static class DriverDebloat
{
    public static string WorkDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "PcismoMaster-Driver");

    public static string? SevenZip()
        => new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
            .Select(p => Path.Combine(p, "7-Zip", "7z.exe")).FirstOrDefault(File.Exists);

    // ---------------- Componentes ----------------

    /// <summary>Pastas do pacote NVIDIA (o script remove todas; as que quebram algo vêm desmarcadas).</summary>
    public static readonly DebloatItem[] Nvidia =
    {
        new("NvTelemetry", "nvidia", "Telemetria", "Coleta de dados de uso da NVIDIA.", true, false),
        new("NvApp", "nvidia", "NVIDIA App", "O aplicativo da NVIDIA (atualizações, filtros, gravação). O Painel de Controle continua.", true, false),
        new("ShadowPlay", "nvidia", "ShadowPlay / gravação", "Gravação e replay instantâneo do NVIDIA App.", true, false),
        new("NvContainer", "nvidia", "Serviços de container", "Processos em segundo plano usados pelo NVIDIA App.", true, false),
        new("NvApp.MessageBus", "nvidia", "Barramento de mensagens do app", "Comunicação interna do NVIDIA App.", true, false),
        new("NvBackend", "nvidia", "Backend do app", "Serviço de suporte do NVIDIA App.", true, false),
        new("FrameViewSDK", "nvidia", "FrameView SDK", "Medição de FPS do NVIDIA App.", true, false),
        new("Display.Nview", "nvidia", "nView", "Gerenciador de áreas de trabalho para várias telas.", true, false),
        new("NvVAD", "nvidia", "Áudio virtual (Broadcast)", "Dispositivo de áudio virtual da NVIDIA Broadcast.", true, false),
        new("MSVCRT", "nvidia", "Bibliotecas Visual C++ do pacote", "Cópias de runtime que o Windows normalmente já tem.", true, false),
        new("PPC", "nvidia", "Controlador USB-C (VirtualLink)", "Só usado pela porta USB-C de algumas RTX 20.", true, false),
        new("NvCpl", "nvidia", "Instalador do Painel de Controle", "Sai do pacote; o Painel de Controle NVIDIA vem da Microsoft Store.", true, false),
        new("HDAudio", "nvidia", "Áudio HDMI/DisplayPort", "Sem ele, caixas de som do monitor ou da TV ligados na placa param de funcionar.", false, true),
        new("PhysX", "nvidia", "PhysX", "Alguns jogos (principalmente antigos) precisam do PhysX para abrir ou para efeitos.", false, true),
        new("NvDLISR", "nvidia", "DLDSR (super resolução por IA)", "Sem ele, o DLDSR some do Painel de Controle.", false, true),
        new("NVPCF", "nvidia", "Platform Controller (notebooks)", "Em notebooks controla o Dynamic Boost (divide energia entre CPU e GPU). Em PC de mesa não faz nada.", false, true),
    };

    /// <summary>Limpezas depois de instalar o driver AMD (serviços são desativados com backup, não apagados).</summary>
    public static readonly DebloatItem[] Amd =
    {
        new("amd-startup", "amd", "Tirar da inicialização", "AMD Noise Suppression, StartRSX e a tarefa StartCN não abrem mais com o Windows.", true, false),
        new("amd-crashdefender", "amd", "Desativar o AMD Crash Defender", "Serviço e drivers que coletam dados de travamentos.", true, false),
        new("amd-installmanager", "amd", "Desinstalar o AMD Install Manager", "Programa que fica instalado só para atualizar o driver.", true, false),
        new("amd-hdaudio", "amd", "Desativar o áudio HDMI/DisplayPort da AMD", "Sem ele, caixas de som do monitor ou da TV ligados na placa param.", false, true),
        new("amd-acp", "amd", "Desativar o áudio do coprocessador (ACP)", "Em notebooks AMD é o microfone e as caixas de som. Só desative em PC de mesa.", false, true),
    };

    /// <summary>Limpezas depois de instalar o driver Intel.</summary>
    public static readonly DebloatItem[] Intel =
    {
        new("intel-startup", "intel", "Tirar o Intel Graphics Software da inicialização", "O painel não abre mais com o Windows.", true, false),
        new("intel-fwupdate", "intel", "Desativar o atualizador de firmware gráfico", "Serviço IntelGFXFWupdateTool.", true, false),
        new("intel-cta", "intel", "Desativar o driver CTA", "Driver auxiliar de telemetria/serviços da Intel.", true, false),
        new("intel-presentmon", "intel", "Desligar o PresentMon do painel", "Medição de FPS que fica rodando em segundo plano.", true, false),
        new("intel-hdcp", "intel", "Desativar o HDCP da Intel", "Streaming protegido (Netflix, Disney+, Blu-ray) para de funcionar.", false, true),
        new("intel-gsc", "intel", "Desativar a interface de firmware (GSC)", "As placas Intel Arc deixam de atualizar o firmware.", false, true),
    };

    private static Setting Svc(string name) => new("HKLM", $@"SYSTEM\CurrentControlSet\Services\{name}", "Start", 4);
    private static bool SvcExists(string name) => Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}") != null;

    /// <summary>Valores de registro de cada limpeza (para o motor de backup).</summary>
    public static Setting[] SettingsFor(string id) => id switch
    {
        "amd-startup" => new[]
        {
            new Setting("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "AMDNoiseSuppression", 0, Remove: true),
            new Setting("HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "StartRSX", 0, Remove: true),
        },
        "amd-crashdefender" => new[] { "AMD Crash Defender Service", "amdfendr", "amdfendrmgr" }.Where(SvcExists).Select(Svc).ToArray(),
        "amd-hdaudio" => new[] { "AtiHDAudioService" }.Where(SvcExists).Select(Svc).ToArray(),
        "amd-acp" => new[] { "amdacpbus", "AMDSAFD" }.Where(SvcExists).Select(Svc).ToArray(),
        "intel-startup" => new[] { new Setting("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Intel® Graphics Software", 0, Remove: true) },
        "intel-fwupdate" => new[] { "IntelGFXFWupdateTool" }.Where(SvcExists).Select(Svc).ToArray(),
        "intel-cta" => new[] { "CtaChildDriver" }.Where(SvcExists).Select(Svc).ToArray(),
        "intel-hdcp" => new[] { "cplspcon" }.Where(SvcExists).Select(Svc).ToArray(),
        "intel-gsc" => new[] { "GSCAuxDriver", "GSCx64" }.Where(SvcExists).Select(Svc).ToArray(),
        "intel-presentmon" => new[] { "PresentMonService" }.Where(SvcExists).Select(Svc).ToArray(),
        _ => Array.Empty<Setting>(),
    };

    /// <summary>Módulo de backup da limpeza; se já foi aplicada, usa as chaves gravadas (para desfazer exatamente o que mudou).</summary>
    public static Module ModuleFor(DebloatItem item)
    {
        string id = "debloat-" + item.Id;
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", id + ".json");
        Setting[] settings = File.Exists(file) && System.Text.Json.JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file)) is { } saved ? saved.Settings : SettingsFor(item.Id);
        return new Module(id, item.Title, "Placa de vídeo", item.Description, "5 Graphics/4 Driver Debloat Install & Settings.ps1", settings);
    }

    public static bool IsApplied(DebloatItem item) => GraphicsTweaks.Engine.HasBackup(ModuleFor(item));

    // ---------------- Pacote ----------------

    /// <summary>Abre o pacote, descobre de qual fabricante ele é pelo conteúdo e instala do jeito enxuto. Devolve "nvidia", "amd" ou "intel" e o código de saída.</summary>
    public static async Task<(string Vendor, int Code)> InstallAsync(string package, ISet<string> nvidiaRemove, Action<string> log, CancellationToken ct)
    {
        string dir = await ExtractAsync(package, log, ct);
        if (File.Exists(Path.Combine(dir, "setup.exe")) && Directory.Exists(Path.Combine(dir, "Display.Driver")))
            return ("nvidia", await InstallNvidiaAsync(dir, nvidiaRemove, log, ct));
        if (File.Exists(Path.Combine(dir, "Bin64", "ATISetup.exe"))) { await InstallAmdAsync(dir, log, ct); return ("amd", 0); }
        if (File.Exists(Path.Combine(dir, "Installer.exe"))) { await InstallIntelAsync(dir, log, ct); return ("intel", 0); }
        TryDelete(dir);
        throw new InvalidOperationException("Esse arquivo não parece um instalador de driver NVIDIA, AMD ou Intel.");
    }

    /// <summary>Abre o pacote do driver com o 7-Zip numa pasta limpa.</summary>
    private static async Task<string> ExtractAsync(string package, Action<string> log, CancellationToken ct)
    {
        string sevenZip = SevenZip() ?? throw new InvalidOperationException("O 7-Zip não está instalado.");
        string dir = Path.Combine(WorkDir, "package");
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        log("Abrindo o pacote do driver…");
        using var p = Process.Start(new ProcessStartInfo(sevenZip, $"x \"{package}\" -o\"{dir}\" -y") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        await p.StandardOutput.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException($"O 7-Zip não conseguiu abrir o pacote (código {p.ExitCode}).");
        return dir;
    }

    /// <summary>NVIDIA: remove os componentes escolhidos do pacote e instala em silêncio e limpo.</summary>
    private static async Task<int> InstallNvidiaAsync(string dir, ISet<string> remove, Action<string> log, CancellationToken ct)
    {
        foreach (var folder in remove)
        {
            if (folder == "NvApp")
            {
                // Do NvApp o script tira tudo menos o necessário para o instalador funcionar.
                string app = Path.Combine(dir, "NvApp");
                foreach (var sub in new[] { "CEF", "osc", "Plugins", "UpgradeConsent", "www" }) TryDelete(Path.Combine(app, sub));
                foreach (var f in new[] { "7z.dll", "7z.exe", "DarkModeCheck.exe", "InstallerExtension.dll", "NvApp.nvi", "NvAppApi.dll", "NvAppExt.dll", "NvConfigGenerator.dll" })
                    TryDelete(Path.Combine(app, f));
            }
            else TryDelete(Path.Combine(dir, folder));
            log("Removido do pacote: " + folder);
        }
        log("Instalando o driver em silêncio (a tela pisca algumas vezes)…");
        using var setup = Process.Start(new ProcessStartInfo(Path.Combine(dir, "setup.exe"), "-s -noreboot -noeula -clean") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir })!;
        await setup.WaitForExitAsync(ct);
        TryDelete(dir);
        TryDelete(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "NVIDIA"));
        return setup.ExitCode;
    }

    /// <summary>AMD: desliga no pacote os componentes extras (atualizador, telemetria…) e instala só o essencial.</summary>
    private static async Task InstallAmdAsync(string dir, Action<string> log, CancellationToken ct)
    {
        string setup = Path.Combine(dir, "Bin64", "ATISetup.exe");
        foreach (var name in new[] { "AMDAUEPInstaller", "AMDCOMPUTE", "AMDLinkDriverUpdate", "AMDRELAUNCHER", "AMDScoSupportTypeUpdate", "AMDUpdater",
                                     "AMDUWPLauncher", "EnableWindowsDriverSearch", "InstallUEP", "ModifyLinkUpdate" })
        {
            string f = Path.Combine(dir, "Config", name + ".xml");
            if (File.Exists(f)) File.WriteAllText(f, File.ReadAllText(f).Replace("<Enabled>true</Enabled>", "<Enabled>false</Enabled>").Replace("<Hidden>true</Hidden>", "<Hidden>false</Hidden>"));
        }
        foreach (var f in new[] { Path.Combine(dir, "Config", "InstallManifest.json"), Path.Combine(dir, "Bin64", "cccmanifest_64.json") })
            if (File.Exists(f)) File.WriteAllText(f, Regex.Replace(File.ReadAllText(f), "\"InstallByDefault\"\\s*:\\s*\"Yes\"", "\"InstallByDefault\" : \"No\""));
        log("Componentes extras desligados no pacote. Instalando o driver…");
        using (var p = Process.Start(new ProcessStartInfo(setup, "-INSTALL -VIEW:2") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(setup)! })!)
            await p.WaitForExitAsync(ct);
        log("Aguardando o instalador da AMD terminar (até 90 s)…");
        await Task.Delay(TimeSpan.FromSeconds(90), ct);
        TryDelete(dir);
        TryDelete(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "AMD"));
    }

    /// <summary>Intel: instala o driver sem extras e o Intel Graphics Software, em silêncio.</summary>
    private static async Task InstallIntelAsync(string dir, Action<string> log, CancellationToken ct)
    {
        string setup = Path.Combine(dir, "Installer.exe");
        log("Instalando o driver Intel sem extras…");
        using (var p = Process.Start(new ProcessStartInfo(setup, "-f --noExtras --terminateProcesses -s") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir })!)
            await p.WaitForExitAsync(ct);
        var panel = Directory.Exists(Path.Combine(dir, "Resources", "Extras")) ? Directory.GetFiles(Path.Combine(dir, "Resources", "Extras"), "IntelGraphicsSoftware_*.exe").FirstOrDefault() : null;
        if (panel != null)
        {
            log("Instalando o Intel Graphics Software…");
            using var p = Process.Start(new ProcessStartInfo(panel, "/s") { UseShellExecute = false, CreateNoWindow = true })!;
            await p.WaitForExitAsync(ct);
        }
        TryDelete(dir);
        TryDelete(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "Intel"));
    }

    /// <summary>Limpezas que não são de registro: tarefa StartCN e desinstalar o AMD Install Manager.</summary>
    public static void ExtraCleanup(string id, Action<string> log)
    {
        if (id == "amd-startup")
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", "/Delete /TN \"StartCN\" /F") { UseShellExecute = false, CreateNoWindow = true })!;
            p.WaitForExit(15000);
        }
        else if (id == "amd-installmanager")
        {
            using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var guid in root?.GetSubKeyNames() ?? Array.Empty<string>())
            {
                using var k = root!.OpenSubKey(guid);
                if ((k?.GetValue("DisplayName") as string)?.Contains("AMD Install Manager", StringComparison.OrdinalIgnoreCase) != true) continue;
                log("Desinstalando o AMD Install Manager…");
                using var p = Process.Start(new ProcessStartInfo("msiexec.exe", $"/x {guid} /qn /norestart") { UseShellExecute = false, CreateNoWindow = true })!;
                p.WaitForExit(180000);
            }
        }
    }

    /// <summary>Fabricante de um instalador escolhido pelo usuário pela assinatura digital (sem apagar o arquivo); null se não for NVIDIA, AMD ou Intel.</summary>
    public static string? SignedVendor(string path)
    {
        if (CorsairSdkInstaller.WinVerifyTrustFile(path) != 0) return null;
        string signer;
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            signer = cert.Subject;
#pragma warning restore SYSLIB0057
        }
        catch { return null; }
        return signer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "nvidia"
            : signer.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ? "amd"
            : signer.Contains("Intel Corporation", StringComparison.OrdinalIgnoreCase) ? "intel" : null;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
