using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Modular;

/// <summary>Roda PowerShell e devolve a saída (o app usa powershell.exe; os testes usam um falso que não mexe no PC).</summary>
public interface IPsRunner { Task<string> RunAsync(string script, IProgress<string>? log, CancellationToken ct); }

/// <summary>Taxa de atualização dos monitores (os testes usam um falso).</summary>
public interface IDisplayRefresh
{
    /// <summary>Monitor → (Hz atual, maior Hz na resolução atual).</summary>
    Dictionary<string, (int Current, int Max)> Read();
    bool Set(string device, int hz);
}

/// <summary>Estado de um ajuste neste PC. On: null = não se aplica aqui (recurso ou serviço que não existe).</summary>
public sealed record OptState(bool? On, bool Ours);

/// <summary>
/// Aplica e desfaz os ajustes do OptCatalog. Registro: ModuleEngine (diário com o valor exato de antes). O resto: um
/// diário próprio (&lt;id&gt;.extra.json) com o estado lido antes de mudar, gravado ANTES da primeira mudança. Para ficar
/// rápido, a leitura e a aplicação de vários ajustes viram um só PowerShell.
/// </summary>
public sealed class OptEngine
{
    private readonly ISettingsStore store;
    private readonly ModuleEngine engine;
    private readonly IPsRunner ps;
    private readonly IDisplayRefresh display;
    private readonly Func<string, bool> serviceExists;
    public string Dir { get; }

    public OptEngine(ISettingsStore store, string dir, IPsRunner ps, IDisplayRefresh display, Func<string, bool> serviceExists)
    {
        this.store = store; Dir = dir; this.ps = ps; this.display = display; this.serviceExists = serviceExists;
        engine = new ModuleEngine(store, Path.Combine(dir, "reg"));
    }

    // ---------------- Registro ----------------

    /// <summary>Valores que valem neste PC: serviços que não existem ficam de fora (não cria serviço falso).</summary>
    public Setting[] SettingsFor(OptTweak t)
    {
        var m = new Module(t.Id, t.Title, t.Category, t.Description, "PcismoMaster", t.Settings);
        if (engine.HasBackup(m) && Saved(t.Id) is { } saved) return saved; // desfaz exatamente o que foi gravado
        return t.Settings.Where(s => ServiceName(s) is not { } svc || serviceExists(svc)).ToArray();
    }

    private Setting[]? Saved(string id)
    {
        try { return JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(Dir, "reg", id + ".json")))?.Settings; } catch { return null; }
    }

    private static string? ServiceName(Setting s)
    {
        const string p = @"SYSTEM\CurrentControlSet\Services\";
        if (s.Hive != "HKLM" || !s.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return null;
        string rest = s.Path[p.Length..];
        int i = rest.IndexOf('\\');
        return i < 0 ? rest : null; // Tcpip6\Parameters etc. não são o Start de um serviço
    }

    private Module ModuleFor(OptTweak t) => new(t.Id, t.Title, t.Category, t.Description, "PcismoMaster", SettingsFor(t));

    private string ExtraFile(string id) => Path.Combine(Dir, id + ".extra.json");
    public bool Ours(OptTweak t) => engine.HasBackup(ModuleFor(t)) || File.Exists(ExtraFile(t.Id));

    // ---------------- Estado ----------------

    /// <summary>Lê o estado de vários ajustes: registro aqui mesmo, o resto num PowerShell só.</summary>
    public async Task<Dictionary<string, OptState>> StatesAsync(IEnumerable<OptTweak> tweaks, CancellationToken ct = default)
    {
        var list = tweaks.ToList();
        var extras = await ReadExtrasAsync(list, ct);
        var result = new Dictionary<string, OptState>();
        foreach (var t in list)
        {
            bool? on = null;
            var m = ModuleFor(t);
            if (m.Settings.Length > 0) on = SafeMatches(m);
            for (int i = 0; i < t.Extras.Length; i++)
            {
                bool? x = t.Extras[i].Kind == ExtraKind.NewUsers ? File.Exists(Path.Combine(Dir, "reg", "new_users.json"))
                    : IsOn(t.Extras[i], extras.GetValueOrDefault(Key(t, i)));
                if (x == null) continue;
                on = (on ?? true) && x.Value;
            }
            result[t.Id] = new OptState(on, Ours(t));
        }
        return result;
    }

    private bool SafeMatches(Module m) { try { return engine.Matches(m); } catch { return false; } }

    private static string Key(OptTweak t, int i) => t.Id + "#" + i;

    private async Task<Dictionary<string, string>> ReadExtrasAsync(IList<OptTweak> tweaks, CancellationToken ct)
    {
        var res = new Dictionary<string, string>();
        var sb = new StringBuilder("$ErrorActionPreference = 'SilentlyContinue'\r\n$r = @{}\r\n");
        bool any = false, caps = false;
        foreach (var t in tweaks)
            for (int i = 0; i < t.Extras.Length; i++)
            {
                var x = t.Extras[i];
                if (x.Kind == ExtraKind.RefreshMax) { res[Key(t, i)] = RefreshState(); continue; }
                if (ReadExpr(x) is not { } expr) continue;
                caps |= x.Kind == ExtraKind.Capabilities;
                any = true;
                sb.Append("try { $r['").Append(Key(t, i)).Append("'] = [string](").Append(expr).Append(") } catch { $r['").Append(Key(t, i)).Append("'] = '?' }\r\n");
            }
        if (!any) return res;
        string script = (caps ? "$caps = $null; try { $caps = @(Get-WindowsCapability -Online -ErrorAction Stop) } catch { }\r\n" : "") + sb + "'@@' + ($r | ConvertTo-Json -Compress)\r\n";
        string output = await ps.RunAsync(script, null, ct);
        int at = output.LastIndexOf("@@", StringComparison.Ordinal);
        if (at < 0) return res;
        try
        {
            foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, string?>>(output[(at + 2)..].Trim()) ?? new())
                res[kv.Key] = kv.Value ?? "";
        }
        catch { }
        return res;
    }

    /// <summary>true = já está como o ajuste deixa; false = não; null = não se aplica neste PC (ou não deu para ler).</summary>
    public static bool? IsOn(OptExtra x, string? state)
    {
        if (state == null || state == "?") return null;
        return x.Kind switch
        {
            ExtraKind.Tasks => state.Split('|').All(s => s.Length == 0) ? null : state.Split('|').All(s => s is "" or "Disabled"),
            ExtraKind.Capabilities or ExtraKind.Appx => state.Length == 0,
            ExtraKind.FeatureOff => state.Length == 0 ? null : state is not ("Enabled" or "EnablePending"),
            ExtraKind.FeatureOn => state.Length == 0 ? null : state is "Enabled" or "EnablePending",
            ExtraKind.Power => state.Split(',') is [var ac, var dc] && ac.Length > 0 ? ac == x.Args[2] && dc == x.Args[2] : null,
            ExtraKind.Hibernate => state != "1",
            ExtraKind.ReservedStorage => state.Length == 0 ? null : state != "Enabled",
            ExtraKind.MemCompression => state.Length == 0 ? null : !state.Equals("True", StringComparison.OrdinalIgnoreCase),
            ExtraKind.PageCombining => state.Length == 0 ? null : !state.Equals("True", StringComparison.OrdinalIgnoreCase),
            ExtraKind.NicProp or ExtraKind.NetBios => state.Length == 0 ? null : Pairs(state).All(p => p.Value == (x.Kind == ExtraKind.NetBios ? "2" : x.Args[1])),
            ExtraKind.Bcd => state is "Yes" or "Sim" || state.Equals(x.Args[1], StringComparison.OrdinalIgnoreCase),
            ExtraKind.OneDrive or ExtraKind.Mstsc => state == "0",
            ExtraKind.BootName => state == x.Args[0],
            ExtraKind.CreateKey => state == "1",
            ExtraKind.DeleteKeys => state.Split('|').All(s => s == "0"),
            ExtraKind.Discord => state == "-" ? null : state == x.Args[1],
            ExtraKind.RefreshMax => state.Length == 0 ? null : state.Split(';').All(p => p.Split('=') is [_, var v] && v.Split('/') is [var c, var m] && c == m),
            _ => null,
        };
    }

    private static string Q(string s) => "'" + s.Replace("'", "''") + "'";
    private static string List(IEnumerable<string> items) => "@(" + string.Join(",", items.Select(Q)) + ")";
    private const string NetBtKey = @"HKLM:\SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
    /// <summary>"nome=valor|nome=valor" (placas de rede, interfaces do NetBIOS) em pares; o nome pode ter "=", o valor não.</summary>
    private static IEnumerable<KeyValuePair<string, string>> Pairs(string state) => state.Split('|', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => { int i = p.LastIndexOf('='); return new KeyValuePair<string, string>(i < 0 ? p : p[..i], i < 0 ? "" : p[(i + 1)..]); });
    private static (string Path, string Name) TaskParts(string full) { int i = full.LastIndexOf('\\'); return (full[..(i + 1)], full[(i + 1)..]); }
    private const string DiscordFile = "\"$env:APPDATA\\discord\\settings.json\"";

    private static string? ReadExpr(OptExtra x) => x.Kind switch
    {
        ExtraKind.Tasks => "(" + string.Join(",", x.Args.Select(a => { var (p, n) = TaskParts(a); return $"(\"$((Get-ScheduledTask -TaskPath {Q(p)} -TaskName {Q(n)} -ErrorAction SilentlyContinue).State)\")"; })) + ") -join '|'",
        ExtraKind.Capabilities => $"$(if ($null -eq $caps) {{ '?' }} else {{ (@($caps | Where-Object {{ $n = $_.Name; $_.State -eq 'Installed' -and ({List(x.Args)} | Where-Object {{ $n -like $_ }}) }} | ForEach-Object Name)) -join ',' }})",
        ExtraKind.FeatureOn or ExtraKind.FeatureOff => $"(Get-WindowsOptionalFeature -Online -FeatureName {Q(x.Args[0])} -ErrorAction SilentlyContinue).State",
        ExtraKind.Power => $"(@(powercfg /qh SCHEME_CURRENT {x.Args[0]} {x.Args[1]} | Select-String '0x[0-9a-fA-F]+\\s*$' | ForEach-Object {{ [Convert]::ToInt32(($_ -replace '.*0x', '').Trim(), 16) }}) | Select-Object -Last 2) -join ','",
        ExtraKind.Hibernate => "(Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power').HibernateEnabled",
        ExtraKind.ReservedStorage => "(Get-WindowsReservedStorageState).ReservedStorageState",
        ExtraKind.MemCompression => "(Get-MMAgent).MemoryCompression",
        ExtraKind.PageCombining => "(Get-MMAgent).PageCombining",
        ExtraKind.NicProp => $"(@(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object {{ $p = Get-NetAdapterAdvancedProperty -Name $_.Name -RegistryKeyword {Q(x.Args[0])} -ErrorAction SilentlyContinue; if ($p) {{ $_.Name + '=' + $p.RegistryValue[0] }} }})) -join '|'",
        ExtraKind.NetBios => $"(@(Get-ChildItem {Q(NetBtKey)} -ErrorAction SilentlyContinue | ForEach-Object {{ $_.PSChildName + '=' + [string](Get-ItemProperty $_.PSPath).NetbiosOptions }})) -join '|'",
        ExtraKind.Bcd => $"$(if ($m = @(bcdedit /enum '{{current}}') -match '^{x.Args[0]}\\s') {{ ($m[0] -split '\\s+')[1] }} else {{ '-' }})",
        ExtraKind.Appx => $"(@(Get-AppxPackage -AllUsers | Where-Object {{ $n = $_.Name; {List(x.Args)} | Where-Object {{ $n -like $_ }} }} | ForEach-Object Name | Select-Object -Unique)) -join ','",
        ExtraKind.OneDrive => "$(if ((Test-Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\") -or (Test-Path \"$env:ProgramFiles\\Microsoft OneDrive\\OneDrive.exe\")) { '1' } else { '0' })",
        ExtraKind.Mstsc => "$(if (Test-Path \"$env:SystemRoot\\System32\\mstsc.exe\") { '1' } else { '0' })",
        ExtraKind.BootName => "((bcdedit /enum '{current}') | Where-Object { $_ -match '^(description|descri\\S*)\\s+' } | Select-Object -First 1) -replace '^\\S+\\s+', ''",
        ExtraKind.CreateKey => $"$(if (Test-Path {Q("Registry::" + Hive(x.Args[0]) + "\\" + x.Args[1])}) {{ '1' }} else {{ '0' }})",
        ExtraKind.DeleteKeys => "(" + string.Join(",", x.Args.Select(k => $"$(if (Test-Path {Q("Registry::" + k)}) {{ '1' }} else {{ '0' }})")) + ") -join '|'",
        ExtraKind.Discord => $"$(if (Test-Path {DiscordFile}) {{ $j = Get-Content -Raw {DiscordFile} | ConvertFrom-Json; if ($j.PSObject.Properties.Name -contains {Q(x.Args[0])}) {{ ($j.{x.Args[0]} | ConvertTo-Json -Compress) }} else {{ '' }} }} else {{ '-' }})",
        _ => null,
    };

    private static string Hive(string h) => h switch { "HKCU" => "HKEY_CURRENT_USER", "HKU" => "HKEY_USERS", _ => "HKEY_LOCAL_MACHINE" };

    private string RefreshState()
    {
        try { return string.Join(";", display.Read().Select(kv => $"{kv.Key}={kv.Value.Current}/{kv.Value.Max}")); } catch { return ""; }
    }

    // ---------------- Aplicar e desfazer ----------------

    /// <summary>Aplica vários ajustes. Devolve os que falharam (id → motivo).</summary>
    public async Task<Dictionary<string, string>> ApplyAsync(IList<OptTweak> tweaks, IProgress<string>? log, CancellationToken ct = default)
    {
        var failed = new Dictionary<string, string>();
        // Grupo (ex.: IPv6): ligar um desfaz o outro do mesmo grupo que o PcismoMaster aplicou.
        var groups = tweaks.Where(t => t.Group != null).Select(t => t.Group).ToHashSet();
        var rivals = OptCatalog.All.Where(o => o.Group != null && groups.Contains(o.Group) && !tweaks.Contains(o) && Ours(o)).ToList();
        if (rivals.Count > 0) foreach (var kv in await UndoAsync(rivals, log, ct)) failed[kv.Key] = kv.Value;

        foreach (var t in tweaks.Where(t => !t.Extras.Any(x => x.Kind == ExtraKind.NewUsers)))
        {
            var m = ModuleFor(t);
            if (m.Settings.Length == 0) continue;
            try
            {
                if (engine.HasBackup(m)) { if (SafeMatches(m)) continue; engine.Restore(m); m = ModuleFor(t); }
                engine.Enable(m);
                log?.Report("✔ " + t.Title);
            }
            catch (Exception e) { failed[t.Id] = Reason(e); log?.Report($"✖ {t.Title}: {Reason(e)}"); }
        }

        // O resto: lê o estado de antes (só de quem ainda não tem diário), grava o diário e roda tudo num script.
        var withExtras = tweaks.Where(t => t.Extras.Any(x => x.Kind != ExtraKind.NewUsers) && !failed.ContainsKey(t.Id)).ToList();
        var fresh = withExtras.Where(t => !File.Exists(ExtraFile(t.Id))).ToList();
        var before = await ReadExtrasAsync(fresh, ct);
        Directory.CreateDirectory(Dir);
        foreach (var t in fresh)
        {
            var saved = t.Extras.Select((_, i) => before.GetValueOrDefault(Key(t, i)) ?? "").ToArray();
            WriteAtomic(ExtraFile(t.Id), JsonSerializer.Serialize(saved));
        }
        var sb = new StringBuilder();
        foreach (var t in withExtras)
        {
            var saved = ReadExtraJournal(t.Id);
            for (int i = 0; i < t.Extras.Length; i++)
            {
                var x = t.Extras[i];
                if (x.Kind == ExtraKind.RefreshMax) { RefreshApply(t, log); continue; }
                if (ApplyScript(t, x, i, saved.ElementAtOrDefault(i) ?? "") is { } s) Block(sb, t, s);
            }
        }
        foreach (var t in tweaks.Where(t => t.StopServices != null && !failed.ContainsKey(t.Id)))
            foreach (var svc in t.StopServices!) sb.Append($"Stop-Service -Name {Q(svc)} -Force -ErrorAction SilentlyContinue\r\n");
        if (sb.Length > 0) await RunLogged(sb.ToString(), log, failed, ct);

        foreach (var t in withExtras.Where(t => !failed.ContainsKey(t.Id))) log?.Report("✔ " + t.Title);
        foreach (var t in tweaks.Where(t => t.Extras.Any(x => x.Kind == ExtraKind.NewUsers)))
            try { await NewUsersApplyAsync(log, ct); log?.Report("✔ " + t.Title); }
            catch (Exception e) { failed[t.Id] = Reason(e); log?.Report($"✖ {t.Title}: {Reason(e)}"); }
        return failed;
    }

    /// <summary>Desfaz o que o PcismoMaster aplicou (os que não têm diário ficam como estão).</summary>
    public async Task<Dictionary<string, string>> UndoAsync(IList<OptTweak> tweaks, IProgress<string>? log, CancellationToken ct = default)
    {
        var failed = new Dictionary<string, string>();
        var sb = new StringBuilder();
        var withExtras = new List<OptTweak>();
        var wasOurs = tweaks.Where(Ours).Select(t => t.Id).ToHashSet();
        foreach (var t in tweaks)
        {
            if (t.Extras.Any(x => x.Kind == ExtraKind.NewUsers))
            {
                try { await NewUsersUndoAsync(log, ct); log?.Report("↩ " + t.Title); } catch (Exception e) { failed[t.Id] = Reason(e); }
                continue;
            }
            if (!File.Exists(ExtraFile(t.Id))) continue;
            var saved = ReadExtraJournal(t.Id);
            withExtras.Add(t);
            for (int i = 0; i < t.Extras.Length; i++)
            {
                var x = t.Extras[i];
                if (x.Kind == ExtraKind.RefreshMax) { RefreshUndo(saved.ElementAtOrDefault(i) ?? ""); continue; }
                if (UndoScript(t, x, saved.ElementAtOrDefault(i) ?? "") is { } s) Block(sb, t, s);
            }
        }
        if (sb.Length > 0) await RunLogged(sb.ToString(), log, failed, ct);
        foreach (var t in withExtras.Where(t => !failed.ContainsKey(t.Id))) File.Delete(ExtraFile(t.Id));

        foreach (var t in tweaks.Where(t => !t.Extras.Any(x => x.Kind == ExtraKind.NewUsers)))
        {
            var m = ModuleFor(t);
            try { if (engine.HasBackup(m)) engine.Restore(m); }
            catch (Exception e) { failed[t.Id] = Reason(e); }
            if (failed.TryGetValue(t.Id, out var why)) log?.Report($"✖ {t.Title}: {why}");
            else if (wasOurs.Contains(t.Id) && !Ours(t)) log?.Report("↩ " + t.Title);
        }
        // Serviços que voltaram a Automático: liga de novo.
        var svcs = tweaks.Where(t => t.StopServices != null).SelectMany(t => t.StopServices!).ToList();
        if (svcs.Count > 0)
            await ps.RunAsync($"foreach ($s in {List(svcs)}) {{ $x = Get-Service -Name $s -ErrorAction SilentlyContinue; if ($x -and $x.StartType -eq 'Automatic') {{ Start-Service -Name $s -ErrorAction SilentlyContinue }} }}", null, ct);
        return failed;
    }

    private static void Block(StringBuilder sb, OptTweak t, string body) =>
        sb.Append("try { & {\r\n$ErrorActionPreference = 'Stop'\r\n").Append(body).Append("\r\n} } catch { Write-Output ('@@FAIL ").Append(t.Id).Append(" ' + $_.Exception.Message) }\r\n");

    private async Task RunLogged(string body, IProgress<string>? log, Dictionary<string, string> failed, CancellationToken ct)
    {
        // O runner não mostra as linhas @@ no log; as falhas vêm da saída inteira.
        string output = await ps.RunAsync(body, log, ct);
        foreach (var line in output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("@@FAIL ")))
        {
            var rest = line[7..];
            int sp = rest.IndexOf(' ');
            string id = sp < 0 ? rest : rest[..sp], why = sp < 0 ? "erro" : rest[(sp + 1)..];
            failed[id] = why;
            log?.Report($"✖ {OptCatalog.ById(id)?.Title ?? id}: {why}");
        }
    }

    private string[] ReadExtraJournal(string id)
    {
        try { return JsonSerializer.Deserialize<string[]>(File.ReadAllText(ExtraFile(id))) ?? Array.Empty<string>(); } catch { return Array.Empty<string>(); }
    }

    private static void WriteAtomic(string file, string text)
    {
        using (var s = new FileStream(file + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            s.Write(bytes);
            s.Flush(true);
        }
        File.Move(file + ".tmp", file, true);
    }

    private static string Reason(Exception e) => e is UnauthorizedAccessException or System.Security.SecurityException
        ? "o Windows protege esta chave (precisa de permissão de sistema)" : e.Message;

    private string KeyBackup(string id, int n) => Path.Combine(Dir, $"{id}-chave{n}.reg");

    public string? ApplyScript(OptTweak t, OptExtra x, int index, string before) => x.Kind switch
    {
        ExtraKind.Tasks => string.Join("\r\n", x.Args.Select(a => { var (p, n) = TaskParts(a); return $"Get-ScheduledTask -TaskPath {Q(p)} -TaskName {Q(n)} -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null"; })),
        ExtraKind.Capabilities => before.Length == 0 ? null : $"foreach ($c in {List(before.Split(','))}) {{ Write-Output \"Removendo $c...\"; Remove-WindowsCapability -Online -Name $c | Out-Null }}",
        ExtraKind.FeatureOff => before is "Enabled" or "EnablePending" ? $"Disable-WindowsOptionalFeature -Online -FeatureName {Q(x.Args[0])} -NoRestart -WarningAction SilentlyContinue | Out-Null" : null,
        ExtraKind.FeatureOn => before is "Enabled" or "EnablePending" ? null : $"Write-Output 'Instalando {x.Args[0]} (pode demorar)...'\r\nEnable-WindowsOptionalFeature -Online -FeatureName {Q(x.Args[0])} -All -NoRestart -WarningAction SilentlyContinue | Out-Null",
        ExtraKind.Power => $"powercfg /setacvalueindex SCHEME_CURRENT {x.Args[0]} {x.Args[1]} {x.Args[2]}\r\npowercfg /setdcvalueindex SCHEME_CURRENT {x.Args[0]} {x.Args[1]} {x.Args[2]}\r\npowercfg /setactive SCHEME_CURRENT",
        ExtraKind.Hibernate => "powercfg /hibernate off",
        ExtraKind.ReservedStorage => "Set-WindowsReservedStorageState -State Disabled | Out-Null",
        ExtraKind.MemCompression => "Disable-MMAgent -MemoryCompression",
        ExtraKind.PageCombining => "Disable-MMAgent -PageCombining",
        ExtraKind.NicProp => $"Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object {{ Set-NetAdapterAdvancedProperty -Name $_.Name -RegistryKeyword {Q(x.Args[0])} -RegistryValue {Q(x.Args[1])} -ErrorAction SilentlyContinue }}",
        ExtraKind.NetBios => $"Get-ChildItem {Q(NetBtKey)} | ForEach-Object {{ Set-ItemProperty $_.PSPath -Name NetbiosOptions -Value 2 -Type DWord }}",
        ExtraKind.Bcd => $"bcdedit /set '{{current}}' {x.Args[0]} {x.Args[1]} | Out-Null",
        ExtraKind.Appx => $"$p = {List(x.Args)}\r\nGet-AppxPackage -AllUsers | Where-Object {{ $n = $_.Name; $p | Where-Object {{ $n -like $_ }} }} | ForEach-Object {{ Write-Output \"Removendo $($_.Name)...\"; Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue }}\r\n" +
                           "Get-AppxProvisionedPackage -Online | Where-Object { $n = $_.DisplayName; $p | Where-Object { $n -like $_ } } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null",
        ExtraKind.OneDrive => "$ErrorActionPreference = 'Continue'\r\n" + Bloatware.ProgramRemove("onedrive"),
        ExtraKind.Mstsc => "$ErrorActionPreference = 'Continue'\r\n" + Bloatware.ProgramRemove("rdp"),
        ExtraKind.BootName => $"bcdedit /set '{{current}}' description {Q(x.Args[0])} | Out-Null",
        ExtraKind.CreateKey => $"$k = {Q("Registry::" + Hive(x.Args[0]) + "\\" + x.Args[1])}\r\nif (!(Test-Path $k)) {{ New-Item -Path $k -Force | Out-Null }}\r\nSet-ItemProperty -Path $k -Name '(default)' -Value ''",
        ExtraKind.DeleteKeys => string.Join("\r\n", x.Args.Select((k, n) =>
            $"if (Test-Path {Q("Registry::" + k)}) {{ reg export {Q(k)} {Q(KeyBackup(t.Id, n))} /y | Out-Null; reg delete {Q(k)} /f | Out-Null }}")),
        ExtraKind.Discord => $"$f = {DiscordFile}\r\nif (Test-Path $f) {{ $j = Get-Content -Raw $f | ConvertFrom-Json; $j | Add-Member -NotePropertyName {Q(x.Args[0])} -NotePropertyValue ({Q(x.Args[1])} | ConvertFrom-Json) -Force; [IO.File]::WriteAllText($f, ($j | ConvertTo-Json -Depth 10)) }}",
        _ => null,
    };

    public string? UndoScript(OptTweak t, OptExtra x, string before) => x.Kind switch
    {
        ExtraKind.Tasks => string.Join("\r\n", x.Args.Zip(before.Split('|').Concat(Enumerable.Repeat("", x.Args.Length)))
            .Where(p => p.Second is not ("" or "Disabled"))
            .Select(p => { var (path, n) = TaskParts(p.First); return $"Get-ScheduledTask -TaskPath {Q(path)} -TaskName {Q(n)} -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue | Out-Null"; })),
        ExtraKind.Capabilities => before.Length == 0 ? null : $"foreach ($c in {List(before.Split(','))}) {{ Write-Output \"Reinstalando $c (baixa do Windows Update)...\"; Add-WindowsCapability -Online -Name $c | Out-Null }}",
        ExtraKind.FeatureOff => before is "Enabled" or "EnablePending" ? $"Enable-WindowsOptionalFeature -Online -FeatureName {Q(x.Args[0])} -All -NoRestart -WarningAction SilentlyContinue | Out-Null" : null,
        ExtraKind.FeatureOn => before is "Enabled" or "EnablePending" or "" ? null : $"Disable-WindowsOptionalFeature -Online -FeatureName {Q(x.Args[0])} -NoRestart -WarningAction SilentlyContinue | Out-Null",
        ExtraKind.Power => before.Split(',') is [var ac, var dc] && ac.Length > 0
            ? $"powercfg /setacvalueindex SCHEME_CURRENT {x.Args[0]} {x.Args[1]} {ac}\r\npowercfg /setdcvalueindex SCHEME_CURRENT {x.Args[0]} {x.Args[1]} {dc}\r\npowercfg /setactive SCHEME_CURRENT" : null,
        ExtraKind.Hibernate => before == "1" ? "powercfg /hibernate on" : null,
        ExtraKind.ReservedStorage => before == "Enabled" ? "Set-WindowsReservedStorageState -State Enabled | Out-Null" : null,
        ExtraKind.MemCompression => before.Equals("True", StringComparison.OrdinalIgnoreCase) ? "Enable-MMAgent -MemoryCompression" : null,
        ExtraKind.PageCombining => before.Equals("True", StringComparison.OrdinalIgnoreCase) ? "Enable-MMAgent -PageCombining" : null,
        ExtraKind.NicProp => before.Length == 0 ? null : string.Join("\r\n", Pairs(before).Select(p => $"Set-NetAdapterAdvancedProperty -Name {Q(p.Key)} -RegistryKeyword {Q(x.Args[0])} -RegistryValue {Q(p.Value)} -ErrorAction SilentlyContinue")),
        ExtraKind.NetBios => before.Length == 0 ? null : string.Join("\r\n", Pairs(before).Select(p => p.Value.Length == 0
            ? $"Remove-ItemProperty {Q(NetBtKey + "\\" + p.Key)} -Name NetbiosOptions -ErrorAction SilentlyContinue"
            : $"Set-ItemProperty {Q(NetBtKey + "\\" + p.Key)} -Name NetbiosOptions -Value {p.Value} -Type DWord -ErrorAction SilentlyContinue")),
        ExtraKind.Bcd => before == "-" ? $"bcdedit /deletevalue '{{current}}' {x.Args[0]} | Out-Null" : before is "Yes" or "Sim" ? null : $"bcdedit /set '{{current}}' {x.Args[0]} no | Out-Null",
        ExtraKind.Appx => null, // volta pela Microsoft Store
        ExtraKind.OneDrive => before == "1" ? Bloatware.ProgramReinstall("onedrive") : null,
        ExtraKind.Mstsc => before == "1" ? Bloatware.ProgramReinstall("rdp") : null,
        ExtraKind.BootName => $"bcdedit /set '{{current}}' description {Q(before.Trim().Length > 0 ? before.Trim() : "Windows")} | Out-Null",
        ExtraKind.CreateKey => before == "0" ? $"Remove-Item -Path {Q("Registry::" + Hive(x.Args[0]) + "\\" + x.Args[2])} -Recurse -Force -ErrorAction SilentlyContinue" : null,
        ExtraKind.DeleteKeys => string.Join("\r\n", x.Args.Select((_, n) => $"if (Test-Path {Q(KeyBackup(t.Id, n))}) {{ reg import {Q(KeyBackup(t.Id, n))} 2>&1 | Out-Null; Remove-Item {Q(KeyBackup(t.Id, n))} -Force }}")),
        ExtraKind.Discord => before == "-" ? null : $"$f = {DiscordFile}\r\nif (Test-Path $f) {{ $j = Get-Content -Raw $f | ConvertFrom-Json; " +
            (before.Length == 0 ? $"$j.PSObject.Properties.Remove({Q(x.Args[0])})" : $"$j | Add-Member -NotePropertyName {Q(x.Args[0])} -NotePropertyValue ({Q(before)} | ConvertFrom-Json) -Force") +
            "; [IO.File]::WriteAllText($f, ($j | ConvertTo-Json -Depth 10)) }",
        _ => null,
    };

    private void RefreshApply(OptTweak t, IProgress<string>? log)
    {
        foreach (var (dev, (cur, max)) in display.Read())
            if (cur != max)
            {
                bool ok = display.Set(dev, max); // fora do log?.: com log nulo a chamada seria pulada
                log?.Report(ok ? $"{dev}: {cur} Hz → {max} Hz" : $"{dev}: não aceitou {max} Hz");
            }
    }

    private void RefreshUndo(string before)
    {
        foreach (var part in before.Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (part.Split('=') is [var dev, var v] && v.Split('/') is [var c, _] && int.TryParse(c, out int hz)) display.Set(dev, hz);
    }

    // ---------------- Contas novas (perfil padrão) ----------------

    private const string DefaultHive = "PM_Default";
    private static readonly Module NewUsersProbe = new("new_users", "", "", "", "", Array.Empty<Setting>());

    /// <summary>Os valores de usuário (HKCU) dos ajustes aplicados, copiados para o perfil padrão (C:\Users\Default).</summary>
    public Setting[] NewUserSettings() => OptCatalog.All.Where(t => engine.HasBackup(ModuleFor(t)))
        .SelectMany(t => SettingsFor(t)).Where(s => s.Hive == "HKCU")
        .Select(s => s with { Hive = "HKU", Path = DefaultHive + "\\" + s.Path })
        .DistinctBy(s => (s.Path.ToLowerInvariant(), s.Name.ToLowerInvariant())).ToArray();

    private async Task WithDefaultHive(Func<Task> work, CancellationToken ct)
    {
        string dat = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Users", "Default", "NTUSER.DAT");
        await ps.RunAsync($"reg load 'HKU\\{DefaultHive}' {Q(dat)} | Out-Null", null, ct);
        try { await work(); }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await ps.RunAsync($"[gc]::Collect(); reg unload 'HKU\\{DefaultHive}' | Out-Null", null, ct);
        }
    }

    private async Task NewUsersApplyAsync(IProgress<string>? log, CancellationToken ct)
    {
        var settings = NewUserSettings();
        if (settings.Length == 0) { log?.Report("Nenhum ajuste de usuário aplicado ainda para copiar às contas novas."); return; }
        await WithDefaultHive(() =>
        {
            var m = NewUsersProbe with { Settings = settings };
            if (engine.HasBackup(m)) engine.Restore(Saved("new_users") is { } s ? m with { Settings = s } : m);
            engine.Enable(m);
            log?.Report($"{settings.Length} valores copiados para o perfil de contas novas.");
            return Task.CompletedTask;
        }, ct);
    }

    private async Task NewUsersUndoAsync(IProgress<string>? log, CancellationToken ct)
    {
        if (!engine.HasBackup(NewUsersProbe) || Saved("new_users") is not { } s) return;
        await WithDefaultHive(() => { engine.Restore(NewUsersProbe with { Settings = s }); return Task.CompletedTask; }, ct);
    }
}

/// <summary>Taxa de atualização pela API do Windows (EnumDisplaySettings / ChangeDisplaySettingsEx).</summary>
public sealed class DisplayRefresh : IDisplayRefresh
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID, DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettings(string name, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string name, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr lParam);

    private const int CurrentSettings = -1, DmDisplayFrequency = 0x400000;
    private const uint CdsUpdateRegistry = 1;

    private static IEnumerable<string> Attached()
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref dd, 0); i++, dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>())
            if ((dd.StateFlags & 1) != 0) yield return dd.DeviceName; // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
    }

    private static DEVMODE New() => new() { dmSize = (short)Marshal.SizeOf<DEVMODE>() };

    public Dictionary<string, (int Current, int Max)> Read()
    {
        var res = new Dictionary<string, (int, int)>();
        foreach (var dev in Attached())
        {
            var cur = New();
            if (!EnumDisplaySettings(dev, CurrentSettings, ref cur)) continue;
            int max = cur.dmDisplayFrequency;
            var m = New();
            for (int i = 0; EnumDisplaySettings(dev, i, ref m); i++, m = New())
                if (m.dmPelsWidth == cur.dmPelsWidth && m.dmPelsHeight == cur.dmPelsHeight && m.dmBitsPerPel == cur.dmBitsPerPel && m.dmDisplayFrequency > max)
                    max = m.dmDisplayFrequency;
            res[dev] = (cur.dmDisplayFrequency, max);
        }
        return res;
    }

    public bool Set(string device, int hz)
    {
        var cur = New();
        if (!EnumDisplaySettings(device, CurrentSettings, ref cur)) return false;
        cur.dmDisplayFrequency = hz;
        cur.dmFields = DmDisplayFrequency;
        return ChangeDisplaySettingsEx(device, ref cur, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero) == 0;
    }
}

/// <summary>Executor real: powershell.exe escondido, saída em UTF-8.</summary>
public sealed class PowerShellRunner : IPsRunner
{
    public async Task<string> RunAsync(string script, IProgress<string>? log, CancellationToken ct)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Scripts");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "opt-" + Guid.NewGuid().ToString("N")[..8] + ".ps1");
        await File.WriteAllTextAsync(file, "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n$ProgressPreference = 'SilentlyContinue'\r\n" + script, new UTF8Encoding(true), ct);
        var psi = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{file}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
          StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        var all = new StringBuilder();
        try
        {
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.StandardInput.Close();
            async Task Pump(StreamReader r)
            {
                string? line;
                while ((line = await r.ReadLineAsync(ct)) != null)
                {
                    lock (all) all.AppendLine(line);
                    if (line.Trim().Length > 0 && !line.StartsWith("@@")) log?.Report(line.TrimEnd());
                }
            }
            using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
            await Task.WhenAll(Pump(p.StandardOutput), Pump(p.StandardError), p.WaitForExitAsync(ct));
        }
        finally { try { File.Delete(file); } catch { } }
        return all.ToString();
    }
}
