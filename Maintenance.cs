using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ResetExplode;

/// <summary>Uma ferramenta da aba Manutenção: o que faz, se cria ponto de restauração antes e os passos a executar.</summary>
public sealed record MaintTool(string Id, string Title, string Description, bool RestorePoint, bool NeedsReboot,
    Func<Maintenance.Ctx, Task> Run);

/// <summary>
/// Reparos e reinícios do Windows (SFC, DISM, CHKDSK, rede, Windows Update, spooler, áudio, Bluetooth, Explorer…).
/// Tudo roda fora da thread da janela, com a saída dos comandos transmitida linha a linha. Só executa quando o
/// usuário clica — nunca em build ou teste.
/// </summary>
public static class Maintenance
{
    static Maintenance() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Contexto de uma execução: para onde vai a saída e o cancelamento.</summary>
    public sealed class Ctx(IProgress<string> log, CancellationToken ct)
    {
        public IProgress<string> Log { get; } = log;
        public CancellationToken Ct { get; } = ct;
        public int Failures { get; set; }
        public void Line(string s) => Log.Report(s);
    }

    private static string Sys(string exe) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
    private static readonly string Ps = Sys(@"WindowsPowerShell\v1.0\powershell.exe");

    // ---------------- Ferramentas ----------------

    public static readonly MaintTool Weekly = new("weekly", "Revisão semanal",
        "Confere a saúde do Windows e corrige o que estiver danificado: disco, arquivos do sistema e imagem do Windows. Seus ajustes continuam aplicados. Leva de 10 a 30 minutos.",
        RestorePoint: true, NeedsReboot: false, async c =>
        {
            await Step(c, "Verificar o disco", Sys("chkdsk.exe"), "C: /scan", Oem);
            await Step(c, "Verificar os arquivos protegidos do sistema", Sys("sfc.exe"), "/scannow", Encoding.Unicode);
            await Step(c, "Reparar a imagem do Windows", Sys("dism.exe"), "/Online /Cleanup-Image /RestoreHealth", Oem);
        });

    public static readonly MaintTool[] Tools =
    {
        new("full", "Reparo completo (DISM + SFC)",
            "Repara a imagem do Windows e depois verifica todos os arquivos do sistema. Leva de 10 a 30 minutos.", true, false, async c =>
            {
                await Step(c, "Reparar a imagem do Windows (DISM)", Sys("dism.exe"), "/Online /Cleanup-Image /RestoreHealth", Oem);
                await Step(c, "Verificar arquivos do sistema (SFC)", Sys("sfc.exe"), "/scannow", Encoding.Unicode);
            }),
        new("sfc", "Verificar arquivos do sistema (SFC)",
            "Procura arquivos do Windows corrompidos e os substitui por cópias corretas.", true, false,
            c => Step(c, "SFC", Sys("sfc.exe"), "/scannow", Encoding.Unicode)),
        new("dism", "Reparar a imagem do Windows (DISM)",
            "Corrige o repositório de componentes que o SFC usa. Precisa de internet.", true, false,
            c => Step(c, "DISM", Sys("dism.exe"), "/Online /Cleanup-Image /RestoreHealth", Oem)),
        new("chkdsk", "Verificar o disco C: (CHKDSK)",
            "Verifica o sistema de arquivos sem reiniciar. Se achar problemas, indica o reparo na próxima inicialização.", false, false,
            c => Step(c, "CHKDSK", Sys("chkdsk.exe"), "C: /scan", Oem)),
        new("dns", "Limpar o cache de DNS",
            "Resolve sites que não abrem ou apontam para o endereço errado.", false, false,
            c => Step(c, "Limpar DNS", Sys("ipconfig.exe"), "/flushdns", Oem)),
        new("ip", "Renovar o endereço IP",
            "Pede um novo endereço ao roteador. A conexão cai por alguns segundos.", false, false, async c =>
            {
                await Step(c, "Liberar o endereço", Sys("ipconfig.exe"), "/release", Oem, failOk: true);
                await Step(c, "Pedir um novo endereço", Sys("ipconfig.exe"), "/renew", Oem);
            }),
        new("netreset", "Redefinir a rede",
            "Redefine Winsock e TCP/IP para os padrões do Windows. Corrige internet que não funciona após vírus ou VPN.", true, true, async c =>
            {
                await Step(c, "Redefinir o Winsock", Sys("netsh.exe"), "winsock reset", Oem);
                await Step(c, "Redefinir o TCP/IP", Sys("netsh.exe"), "int ip reset", Oem, failOk: true);
                await Step(c, "Limpar o cache de DNS", Sys("ipconfig.exe"), "/flushdns", Oem);
            }),
        new("wureset", "Redefinir o Windows Update",
            "Para os serviços, renomeia as pastas de cache e reinicia. Corrige atualizações que falham ou travam.", true, false, async c =>
            {
                string[] services = { "wuauserv", "cryptSvc", "bits", "msiserver" };
                foreach (var s in services) await Step(c, $"Parar {s}", Sys("net.exe"), $"stop {s} /y", Oem, failOk: true);
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                RenameAside(c, Path.Combine(win, "SoftwareDistribution"));
                RenameAside(c, Path.Combine(win, "System32", "catroot2"));
                foreach (var s in services) await Step(c, $"Iniciar {s}", Sys("net.exe"), $"start {s}", Oem, failOk: true);
            }),
        new("spooler", "Destravar a impressora",
            "Limpa a fila de impressão e reinicia o spooler. Resolve documentos presos na fila.", false, false, async c =>
            {
                await Step(c, "Parar o spooler", Sys("net.exe"), "stop spooler /y", Oem, failOk: true);
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "PRINTERS");
                int n = 0;
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir)) { try { File.Delete(f); n++; } catch (Exception ex) { c.Line($"  não apagou {Path.GetFileName(f)}: {ex.Message}"); } }
                c.Line($"{n} arquivo(s) da fila apagado(s).");
                await Step(c, "Iniciar o spooler", Sys("net.exe"), "start spooler", Oem);
            }),
        new("audio", "Reiniciar o áudio do Windows",
            "Reinicia os serviços de áudio. Resolve som que sumiu, chiado ou travamentos.", false, false,
            c => PowerShell(c, "Reiniciar o áudio", "Restart-Service -Name AudioEndpointBuilder -Force; Start-Service Audiosrv; Get-Service Audiosrv, AudioEndpointBuilder | Format-Table -AutoSize Name, Status")),
        new("bt", "Reiniciar o Bluetooth",
            "Reinicia o serviço de suporte Bluetooth. Resolve dispositivos que não conectam.", false, false,
            c => PowerShell(c, "Reiniciar o Bluetooth", "Restart-Service -Name bthserv -Force; Get-Service bthserv | Format-Table -AutoSize Name, Status")),
        new("explorer", "Reiniciar o Windows Explorer",
            "Reinicia a barra de tarefas e as pastas. Resolve barra de tarefas ou menu Iniciar travados.", false, false,
            c => RestartExplorer(c, null)),
        new("iconcache", "Reconstruir o cache de ícones",
            "Corrige ícones em branco ou errados e miniaturas que não aparecem.", false, false,
            c => RestartExplorer(c, () =>
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var files = new List<string>();
                if (File.Exists(Path.Combine(local, "IconCache.db"))) files.Add(Path.Combine(local, "IconCache.db"));
                string exp = Path.Combine(local, "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(exp)) files.AddRange(Directory.GetFiles(exp, "iconcache_*.db").Concat(Directory.GetFiles(exp, "thumbcache_*.db")));
                int n = 0;
                foreach (var f in files) { try { File.Delete(f); n++; } catch { } }
                c.Line($"{n} arquivo(s) de cache de ícones e miniaturas apagado(s).");
            })),
        new("gpureset", "Reiniciar o driver de vídeo",
            "Atalho do Windows (Win+Ctrl+Shift+B) para tela preta ou congelada. Seus programas continuam abertos.", false, false, c =>
            {
                c.Line("Enviando Win+Ctrl+Shift+B… a tela pisca e pode apitar.");
                SendWinCtrlShiftB();
                c.Line("Pronto.");
                return Task.CompletedTask;
            }),
        new("hwscan", "Procurar alterações de hardware",
            "Faz o Windows detectar de novo pendrives, monitores e webcams que não aparecem.", false, false,
            c => Step(c, "Procurar alterações de hardware", Sys("pnputil.exe"), "/scan-devices", Oem)),
        new("store", "Reparar Microsoft Store e menu Iniciar",
            "Registra de novo os apps essenciais do Windows quando a Store ou o Iniciar não abrem.", true, false,
            c => PowerShell(c, "Registrar Store e Iniciar",
                "$names = 'Microsoft.WindowsStore','Microsoft.Windows.StartMenuExperienceHost','Microsoft.Windows.ShellExperienceHost','MicrosoftWindows.Client.CBS';" +
                "Get-AppxPackage -AllUsers -Name $names | ForEach-Object { Write-Output ('Registrando ' + $_.Name); " +
                "try { Add-AppxPackage -DisableDevelopmentMode -Register (Join-Path $_.InstallLocation 'AppXManifest.xml') -ErrorAction Stop } catch { Write-Output ('  ' + $_.Exception.Message) } }")),
    };

    // ---------------- Ponto de restauração ----------------

    /// <summary>Cria um ponto de restauração. Devolve null se deu certo, ou o motivo (Proteção do Sistema desligada,
    /// já existe um das últimas 24 h etc.).</summary>
    public static async Task<string?> CreateRestorePoint(Ctx c, string title)
    {
        c.Line("Criando ponto de restauração…");
        var lines = new List<string>();
        string desc = ("PcismoMaster - " + title).Replace("'", "");
        // Sem barra de progresso (o PowerShell a manda como texto CLIXML misturado à saída) e conferindo na lista
        // de pontos do Windows se o novo existe — o Checkpoint-Computer às vezes só avisa, sem erro, quando não cria.
        string script =
            "$ProgressPreference='SilentlyContinue'; $before=(Get-Date).AddMinutes(-2); $msg='';" +
            $"try {{ Checkpoint-Computer -Description '{desc}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop -WarningVariable w -WarningAction SilentlyContinue; $msg=($w -join ' ') }} catch {{ $msg=$_.Exception.Message }};" +
            "$new = Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Where-Object { $_.ConvertToDateTime($_.CreationTime) -ge $before };" +
            "if ($new) { 'RP-OK' } else { 'RP-FALHA: ' + $msg }";
        await Run(Ps, Args(script), Encoding.UTF8, new Progress<string>(s => { lock (lines) lines.Add(s.Trim()); }), c.Ct);
        string? result;
        lock (lines) result = lines.LastOrDefault(l => l.StartsWith("RP-"));
        if (result == "RP-OK") { c.Line("Ponto de restauração criado."); return null; }
        string why = result?.Replace("RP-FALHA:", "").Trim() is { Length: > 0 } m ? m : "o Windows não informou o motivo";
        if (why.Contains("1440")) why = "o Windows já criou um ponto de restauração nas últimas 24 horas (limite do próprio Windows)";
        else if (why.Contains("disabled", StringComparison.OrdinalIgnoreCase) || why.Contains("desativad", StringComparison.OrdinalIgnoreCase)
                 || why.Contains("desabilitad", StringComparison.OrdinalIgnoreCase))
            why = "a Proteção do Sistema está desligada no disco C: (ative em Configurações → Sistema → Sobre → Proteção do sistema)";
        c.Line("Ponto de restauração não criado: " + why);
        lock (lines) foreach (var l in lines.Where(l => !l.StartsWith("RP-") && l.Length > 0).Take(8)) c.Line("  " + l);
        return why;
    }

    // ---------------- Execução ----------------

    private static Encoding Oem => Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

    private static async Task Step(Ctx c, string title, string exe, string args, Encoding enc, bool failOk = false)
    {
        c.Ct.ThrowIfCancellationRequested();
        c.Line("");
        c.Line($"▶ {title}");
        int code = await Run(exe, args, enc, c.Log, c.Ct);
        if (code != 0 && !failOk) { c.Failures++; c.Line($"  terminou com código {code}."); }
    }

    private static Task PowerShell(Ctx c, string title, string script) => Step(c, title, Ps, Args(script), Encoding.UTF8);

    private static string Args(string script)
        => "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
           Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + script));

    /// <summary>Roda um programa sem janela e repassa a saída linha a linha (o "\r" do progresso de SFC/DISM/CHKDSK
    /// também conta como fim de linha). Cancelar encerra o processo.</summary>
    public static async Task<int> Run(string exe, string args, Encoding enc, IProgress<string> log, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = enc, StandardErrorEncoding = enc,
        };
        using var p = Process.Start(psi) ?? throw new IOException($"Não foi possível iniciar {Path.GetFileName(exe)}.");
        using var reg = ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch { } });
        async Task Pump(StreamReader r)
        {
            var buf = new char[1024];
            var line = new StringBuilder();
            int n;
            while ((n = await r.ReadAsync(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < n; i++)
                {
                    char ch = buf[i];
                    if (ch == '\0') continue;
                    if (ch is '\r' or '\n') { if (line.Length > 0) { string s = line.ToString().TrimEnd(); if (s.Length > 0) log.Report("  " + s); line.Clear(); } }
                    else line.Append(ch);
                }
            if (line.Length > 0) log.Report("  " + line.ToString().TrimEnd());
        }
        await Task.WhenAll(Pump(p.StandardOutput), Pump(p.StandardError));
        await p.WaitForExitAsync();
        ct.ThrowIfCancellationRequested();
        return p.ExitCode;
    }

    private static void RenameAside(Ctx c, string dir)
    {
        if (!Directory.Exists(dir)) return;
        string old = dir + ".old";
        try
        {
            if (Directory.Exists(old)) Directory.Delete(old, recursive: true);
            Directory.Move(dir, old);
            c.Line($"{Path.GetFileName(dir)} → {Path.GetFileName(old)}");
        }
        catch (Exception ex) { c.Failures++; c.Line($"  não deu para renomear {Path.GetFileName(dir)}: {ex.Message}"); }
    }

    /// <summary>Encerra o Explorer (o Windows o abre de novo sozinho) e, se ele não voltar em alguns segundos, abre.</summary>
    internal static async Task RestartExplorer(Ctx c, Action? whileStopped)
    {
        c.Line("Encerrando o Windows Explorer…");
        foreach (var p in Process.GetProcessesByName("explorer")) { using (p) try { p.Kill(); p.WaitForExit(5000); } catch { } }
        whileStopped?.Invoke();
        for (int i = 0; i < 10 && Process.GetProcessesByName("explorer").Length == 0; i++) await Task.Delay(500, c.Ct);
        if (Process.GetProcessesByName("explorer").Length == 0)
        {
            c.Line("Abrindo o Explorer de novo…");
            // Pelo próprio Windows (sem herdar o modo administrador do PcismoMaster).
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true });
        }
        c.Line("Explorer reiniciado.");
    }

    // ---------------- Win+Ctrl+Shift+B ----------------

    [StructLayout(LayoutKind.Sequential)] private struct KeybdInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential, Size = 40)] private struct Input { public uint Type; public KeybdInput Ki; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint n, Input[] inputs, int size);

    private static void SendWinCtrlShiftB()
    {
        ushort[] keys = { 0x5B, 0x11, 0x10, 0x42 }; // Win, Ctrl, Shift, B
        var list = new List<Input>();
        foreach (var k in keys) list.Add(new Input { Type = 1, Ki = new KeybdInput { Vk = k } });
        foreach (var k in keys.Reverse()) list.Add(new Input { Type = 1, Ki = new KeybdInput { Vk = k, Flags = 2 } });
        SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<Input>());
    }
}
