using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Opções da limpeza com o DDU.</summary>
/// <param name="Auto">true: o DDU limpa sozinho e reinicia; false: abre a tela do DDU no modo de segurança.</param>
/// <param name="Gpus">all | nvidia | amd | intel</param>
/// <param name="Audio">Também remove drivers de áudio Realtek e Sound Blaster (o PC fica sem som até reinstalar).</param>
/// <param name="BlockWu">Impede o Windows Update de instalar um driver de vídeo sozinho depois da limpeza.</param>
public sealed record DduOptions(bool Auto, string Gpus, bool Audio, bool BlockWu);

/// <summary>
/// Limpeza completa de driver de vídeo com o Display Driver Uninstaller (DDU, Wagnardsoft) no modo de segurança —
/// a versão do PcismoMaster do "5 Graphics/1 Driver Clean.ps1" do Ultimate (MIT). Diferenças do script:
/// confere a assinatura da Wagnardsoft, extrai o DDU sem precisar do 7-Zip, só mexe no boot depois de tudo pronto
/// e conferido, guarda o valor do Windows Update para poder desfazer, deixa um atalho de emergência para sair do
/// modo de segurança e reinicia com contagem regressiva que pode ser cancelada.
/// </summary>
public static class DriverClean
{
    // O mesmo arquivo que o Ultimate usa: o instalador oficial do DDU (assinatura da Wagnardsoft conferida antes de usar).
    private const string DduUrl = "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/ddu.exe";
    private const string RunOnceName = "*PcismoMasterDDU"; // o "*" faz o RunOnce rodar também no modo de segurança
    private const string WuKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching";

    public static string WorkDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "PcismoMaster-DDU");
    private static string Sys(string exe) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
    public static string EmergencyShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Sair do modo de segurança (PcismoMaster).cmd");

    /// <summary>Baixa (ou usa o arquivo escolhido), confere a assinatura, extrai e configura o DDU. Devolve o caminho do executável.</summary>
    public static async Task<string> PrepareAsync(DduOptions o, string? localSetup, Action<string> log, CancellationToken ct)
    {
        Directory.CreateDirectory(WorkDir);
        string setup = Path.Combine(WorkDir, "ddu-setup.exe");
        if (localSetup != null) { File.Copy(localSetup, setup, overwrite: true); log("Usando o DDU escolhido: " + Path.GetFileName(localSetup)); }
        else
        {
            log("Baixando o DDU…");
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            await File.WriteAllBytesAsync(setup, await http.GetByteArrayAsync(DduUrl, ct), ct);
        }
        log("Conferindo a assinatura digital (Wagnardsoft)…");
        DriverManager.VerifyPublisher(setup, "Wagnardsoft");
        log($"DDU {FileVersionInfo.GetVersionInfo(setup).FileVersion} com assinatura válida.");

        string dir = Path.Combine(WorkDir, "ddu");
        // Da tentativa anterior sobra o Settings.xml somente leitura: sem tirar o atributo, apagar a pasta dá "acesso negado".
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        log("Extraindo o DDU…");
        using (var p = Process.Start(new ProcessStartInfo(setup, $"/S /D={dir}") { UseShellExecute = false, CreateNoWindow = true })!)
            await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(2), ct);
        string exe = FindDdu(dir);
        if (exe.Length == 0 && DriverDebloat.SevenZip() is string sevenZip)
        {
            // Plano B, como o script do Ultimate: abrir o instalador com o 7-Zip.
            log("O instalador não extraiu sozinho; abrindo com o 7-Zip…");
            using var z = Process.Start(new ProcessStartInfo(sevenZip, $"x \"{setup}\" -o\"{dir}\" -y") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
            await z.StandardOutput.ReadToEndAsync(ct);
            await z.WaitForExitAsync(ct);
            exe = FindDdu(dir);
        }
        if (exe.Length == 0) throw new InvalidOperationException("O DDU não foi extraído. Nada foi alterado no PC.");

        string settings = Path.Combine(Path.GetDirectoryName(exe)!, "Settings");
        Directory.CreateDirectory(settings);
        string xml = Path.Combine(settings, "Settings.xml");
        if (File.Exists(xml)) File.SetAttributes(xml, FileAttributes.Normal);
        await File.WriteAllTextAsync(xml, SettingsXml(o), ct);
        File.SetAttributes(xml, FileAttributes.ReadOnly); // o DDU não reescreve as opções
        log("DDU pronto: " + exe);
        return exe;
    }

    private static string FindDdu(string dir)
        => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "Display Driver Uninstaller.exe", SearchOption.AllDirectories).FirstOrDefault() ?? "" : "";

    /// <summary>Registro de cada etapa da limpeza (para entender o que aconteceu se algo falhar, inclusive no modo de segurança).</summary>
    public static string LogFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ddu-log.txt");

    public static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}\r\n");
        }
        catch { }
    }

    /// <summary>Configuração do DDU (a mesma do Ultimate: remove painéis, PhysX, GeForce Experience, Vulkan etc.).</summary>
    private static string SettingsXml(DduOptions o)
    {
        static string B(bool v) => v ? "True" : "False";
        var on = new[] { "RemoveMonitors", "RemoveCrimsonCache", "RemoveAMDDirs", "RemoveAudioBus", "RemoveAMDKMPFD", "RemoveNvidiaDirs", "RemovePhysX",
            "Remove3DTVPlay", "RemoveGFE", "RemoveNVBROADCAST", "RemoveNVCP", "RemoveINTELCP", "RemoveINTELIGS", "RemoveOneAPI", "RemoveEnduranceGaming",
            "RemoveIntelNpu", "RemoveAMDCP", "RemoveVulkan" };
        var off = new[] { "UseRoamingConfig", "CheckUpdates", "CreateRestorePoint", "SaveLogs", "ShowOffer", "EnableSafeModeDialog", "UsedBCD",
            "KeepNVCPopt", "RememberLastChoice" };
        var lines = new List<string> { "<SelectedLanguage>en-US</SelectedLanguage>" };
        lines.AddRange(on.Select(n => $"<{n}>True</{n}>"));
        lines.AddRange(off.Select(n => $"<{n}>False</{n}>"));
        lines.Add($"<PreventWinUpdate>{B(o.BlockWu)}</PreventWinUpdate>");
        lines.Add("<LastSelectedGPUIndex>0</LastSelectedGPUIndex>");
        lines.Add("<LastSelectedTypeIndex>0</LastSelectedTypeIndex>");
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<DisplayDriverUninstaller Version=\"18.1.4.2\">\r\n\t<Settings>\r\n\t\t"
               + string.Join("\r\n\t\t", lines) + "\r\n\t</Settings>\r\n</DisplayDriverUninstaller>\r\n";
    }

    /// <summary>Argumentos do DDU no modo automático.</summary>
    public static string AutoArgs(DduOptions o)
    {
        var a = new List<string>
        {
            o.Gpus switch { "nvidia" => "-CleanNvidia", "amd" => "-CleanAmd", "intel" => "-CleanIntel", _ => "-CleanAllGpus" },
        };
        if (o.Audio) { a.Add("-CleanSoundBlaster"); a.Add("-CleanRealtek"); }
        a.Add("-Restart");
        return string.Join(" ", a);
    }

    // ---------------- Windows Update (driver de vídeo automático) ----------------

    /// <summary>Valor atual de SearchOrderConfig (null = não existe; o Windows usa o padrão, que baixa drivers).</summary>
    public static int? ReadWuSearchOrder()
    {
        using var k = Registry.LocalMachine.OpenSubKey(WuKey);
        return k?.GetValue("SearchOrderConfig") is int v ? v : null;
    }

    public static void BlockWu()
    {
        using var k = Registry.LocalMachine.CreateSubKey(WuKey);
        k.SetValue("SearchOrderConfig", 0, RegistryValueKind.DWord);
    }

    /// <summary>Volta ao valor guardado (ou ao padrão do Windows, que deixa o Windows Update instalar drivers).</summary>
    public static void RestoreWu(int? previous)
    {
        using var k = Registry.LocalMachine.CreateSubKey(WuKey);
        if (previous is int v) k.SetValue("SearchOrderConfig", v, RegistryValueKind.DWord);
        else k.DeleteValue("SearchOrderConfig", throwOnMissingValue: false);
    }

    // ---------------- Modo de segurança ----------------

    /// <summary>
    /// Programa o modo de segurança: script que roda no login do modo seguro (primeiro tira o modo seguro, depois o DDU),
    /// atalho de emergência na Área de Trabalho e o boot em modo seguro. Confere o BCD no fim; se algo falhar, desfaz.
    /// </summary>
    public static void ScheduleSafeMode(DduOptions o, string dduExe)
    {
        string script = Path.Combine(WorkDir, "run-ddu.ps1");
        string ddu = dduExe.Replace("'", "''");
        string body = o.Auto
            ? $"$p = Start-Process -FilePath '{ddu}' -ArgumentList '{AutoArgs(o)}' -Wait -PassThru"
            : $"$p = Start-Process -FilePath '{ddu}' -Wait -PassThru";
        string logFile = LogFile.Replace("'", "''");
        File.WriteAllText(script,
            "# PcismoMaster: limpeza de driver com o DDU (modo de segurança)\r\n" +
            $"function L($t) {{ Add-Content -LiteralPath '{logFile}' -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  [modo de segurança] ' + $t) }}\r\n" +
            "L 'Script iniciado.'\r\n" +
            "# 1) sai do modo de segurança no próximo boot, aconteça o que acontecer\r\n" +
            $"& '{Sys("bcdedit.exe")}' /deletevalue '{{current}}' safeboot | Out-Null\r\n" +
            "L ('Modo de segurança desfeito para o próximo boot (código ' + $LASTEXITCODE + ').')\r\n" +
            "Write-Host 'PcismoMaster: limpando o driver de vídeo com o DDU...'\r\n" +
            "# 2) DDU\r\n" +
            "try {\r\n  " + body + "\r\n  L ('DDU terminou (código ' + $p.ExitCode + ').')\r\n} catch { L ('Falha ao abrir o DDU: ' + $_.Exception.Message) }\r\n");
        File.WriteAllText(EmergencyShortcut,
            "@echo off\r\nrem PcismoMaster: tira o modo de segurança e reinicia (use se a limpeza não começar sozinha)\r\n" +
            "bcdedit /deletevalue {current} safeboot\r\nshutdown /r /t 5\r\n");

        using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
            k.SetValue(RunOnceName, $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Maximized -File \"{script}\"", RegistryValueKind.String);

        var (code, output) = RunBcd("/set {current} safeboot minimal");
        Log($"bcdedit /set safeboot minimal → código {code}: {output.Trim()}");
        // Os nomes dos campos do bcdedit podem vir traduzidos; o valor "Minimal" não é.
        string check = RunBcd("/enum {current}").Output;
        bool ok = code == 0 && (check.Contains("safeboot", StringComparison.OrdinalIgnoreCase) || check.Contains("Minimal", StringComparison.OrdinalIgnoreCase));
        Log("bcdedit /enum {current}: " + (ok ? "modo de segurança confirmado" : "NÃO confirmado\r\n" + check.Trim()));
        if (!ok)
        {
            Unschedule();
            throw new InvalidOperationException("O Windows não aceitou o modo de segurança (" + output.Trim() + "). Nada foi alterado.");
        }
    }

    /// <summary>Desfaz o agendamento (boot normal, sem RunOnce). Usado no Cancelar e se algo falhar.</summary>
    public static void Unschedule()
    {
        RunBcd("/deletevalue {current} safeboot");
        using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", writable: true))
            k?.DeleteValue(RunOnceName, throwOnMissingValue: false);
        try { File.Delete(EmergencyShortcut); } catch { }
    }

    /// <summary>Agenda o reinício e confere se o Windows aceitou (ex.: código 1190 = já havia um desligamento agendado).</summary>
    public static void Restart(int seconds)
    {
        var psi = new ProcessStartInfo(Sys("shutdown.exe"), $"/r /t {seconds} /c \"PcismoMaster: reiniciando em modo de segurança para limpar o driver de vídeo.\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        using var p = Process.Start(psi)!;
        string err = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
        p.WaitForExit(10000);
        Log($"shutdown /r /t {seconds} → código {p.ExitCode} {err.Trim()}");
        if (p.ExitCode != 0) throw new InvalidOperationException($"O Windows não aceitou o reinício (código {p.ExitCode}). {err.Trim()}");
    }
    public static void AbortRestart() => Process.Start(new ProcessStartInfo(Sys("shutdown.exe"), "/a") { UseShellExecute = false, CreateNoWindow = true });

    private static (int Code, string Output) RunBcd(string args)
    {
        var psi = new ProcessStartInfo(Sys("bcdedit.exe"), args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        return (p.ExitCode, o);
    }
}
