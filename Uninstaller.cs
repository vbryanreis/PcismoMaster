using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Uma "sobra" encontrada depois de desinstalar: uma pasta ou uma chave de registro ligada ao app.</summary>
public sealed record Leftover(string Kind, string Display, long SizeBytes, string? FilePath,
                              RegistryHive Hive, RegistryView View, string? RegPath)
{
    public string SizeText => SizeBytes <= 0 ? ""
        : SizeBytes >= 1L << 30 ? $"{SizeBytes / (double)(1L << 30):0.0} GB"
        : SizeBytes >= 1L << 20 ? $"{SizeBytes / (double)(1L << 20):0} MB"
        : $"{SizeBytes / 1024.0:0} KB";
}

/// <summary>
/// Desinstala um programa pelo desinstalador OFICIAL dele e, depois, procura sobras (pastas e chaves de registro)
/// ligadas ao app para o usuário revisar e remover. Toda remoção tem guardas de segurança: só apaga dentro de
/// Program Files / AppData / ProgramData e de ramos Software\ do registro — NUNCA pastas do Windows nem chaves do
/// sistema (Microsoft, Classes, Policies, Windows). Nada é apagado sem o usuário marcar e confirmar.
/// </summary>
public static class Uninstaller
{
    // ---------------- desinstalação oficial ----------------

    public static async Task<bool> RunOfficialAsync(InstalledProgram p, CancellationToken ct)
    {
        var (exe, args) = ParseCommand(p.UninstallCmd);
        if (exe.Length == 0) return false;

        // msiexec: /I (modificar) vira /X (remover) para garantir a desinstalação.
        if (Path.GetFileNameWithoutExtension(exe).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
            args = args.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase).Replace("/i", "/X");

        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true }; // deixa a UI do desinstalador aparecer, herdando a elevação
        using var proc = Process.Start(psi);
        if (proc == null) return false;
        await proc.WaitForExitAsync(ct);
        return true;
    }

    private static (string Exe, string Args) ParseCommand(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.Length == 0) return ("", "");
        if (cmd[0] == '"')
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0) return (cmd.Substring(1, end - 1), cmd[(end + 1)..].Trim());
        }
        int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) { int cut = i + 4; return (cmd[..cut].Trim(), cmd[cut..].Trim()); }
        int sp = cmd.IndexOf(' ');
        return sp > 0 ? (cmd[..sp], cmd[(sp + 1)..].Trim()) : (cmd, "");
    }

    // ---------------- varredura de sobras ----------------

    public static List<Leftover> ScanLeftovers(InstalledProgram p)
    {
        var found = new List<Leftover>();
        var tokens = Tokens(p.Name, p.Publisher);
        if (tokens.Count == 0) return found;

        // Pastas em locais de usuário/programas (só filhos diretos, nome batendo com o app).
        foreach (var root in FileRoots())
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    string nm = Path.GetFileName(dir);
                    if (IsDeniedName(nm) || !Matches(nm, tokens)) continue;
                    if (!IsSafeFolder(dir)) continue;
                    found.Add(new Leftover("Pasta", dir, DirSize(dir), dir, default, default, null));
                }
            }
            catch { }
        }

        // A própria pasta de instalação, se o desinstalador deixou para trás.
        if (p.InstallLocation.Length > 0 && IsSafeFolder(p.InstallLocation) && Directory.Exists(p.InstallLocation)
            && !found.Any(f => string.Equals(f.FilePath, p.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            found.Add(new Leftover("Pasta", p.InstallLocation, DirSize(p.InstallLocation), p.InstallLocation, default, default, null));

        // Chaves de registro de fornecedor (HKCU/HKLM Software e WOW6432Node), só filhos diretos batendo com o app.
        foreach (var (hive, view) in new[] { (RegistryHive.CurrentUser, RegistryView.Default),
                                             (RegistryHive.LocalMachine, RegistryView.Registry64),
                                             (RegistryHive.LocalMachine, RegistryView.Registry32) })
        {
            try
            {
                using var bk = RegistryKey.OpenBaseKey(hive, view);
                using var sw = bk.OpenSubKey("Software");
                if (sw == null) continue;
                foreach (var name in sw.GetSubKeyNames())
                {
                    if (IsDeniedName(name) || !Matches(name, tokens)) continue;
                    string path = "Software\\" + name;
                    if (!IsSafeRegPath(path)) continue;
                    found.Add(new Leftover("Registro", $"{HiveLabel(hive, view)}\\{path}", 0, null, hive, view, path));
                }
            }
            catch { }
        }
        return found;
    }

    /// <summary>Remove as sobras recebidas, revalidando as guardas de segurança em cada uma. Retorna (removidas, falhas).</summary>
    public static (int Ok, int Fail) DeleteLeftovers(IEnumerable<Leftover> items)
    {
        int ok = 0, fail = 0;
        foreach (var it in items)
        {
            try
            {
                if (it.Kind == "Pasta" && it.FilePath is { } path && IsSafeFolder(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (it.Kind == "Registro" && it.RegPath is { } reg && IsSafeRegPath(reg))
                {
                    using var bk = RegistryKey.OpenBaseKey(it.Hive, it.View);
                    bk.DeleteSubKeyTree(reg, throwOnMissingSubKey: false);
                }
                else { fail++; continue; }
                ok++;
            }
            catch { fail++; }
        }
        return (ok, fail);
    }

    // ---------------- guardas de segurança ----------------

    private static IEnumerable<string> FileRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);        // AppData\Roaming
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);    // AppData\Local
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);   // ProgramData
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    }

    private static IEnumerable<string> AllowedRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    }

    /// <summary>Só é seguro apagar uma pasta que esteja DENTRO (ao menos um nível) de um dos locais permitidos e fora do Windows.</summary>
    private static bool IsSafeFolder(string path)
    {
        try
        {
            string full = Path.GetFullPath(path).TrimEnd('\\');
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (full.Equals(win, StringComparison.OrdinalIgnoreCase) || full.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (var root in AllowedRoots())
            {
                string r = root.TrimEnd('\\');
                if (r.Length == 0) continue;
                if (full.Length > r.Length + 1 && full.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase))
                    return true; // dentro do local permitido, mas não é o próprio root
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>Só é seguro apagar chaves sob Software\, com profundidade mínima e fora dos ramos críticos do sistema.</summary>
    private static bool IsSafeRegPath(string sub)
    {
        string s = sub.Replace('/', '\\').Trim('\\');
        string lower = s.ToLowerInvariant();
        if (!lower.StartsWith("software\\")) return false;
        if (s.Split('\\').Length < 2) return false; // nunca a raiz "Software"
        string[] critical = { "software\\microsoft", "software\\classes", "software\\policies", "software\\windows",
                              "software\\wow6432node\\microsoft", "software\\wow6432node\\classes", "software\\wow6432node\\policies" };
        return !critical.Any(c => lower == c || lower.StartsWith(c + "\\"));
    }

    private static readonly string[] Denied =
    {
        "windows", "microsoft", "common files", "windowsapps", "packages", "windowspowershell",
        "temp", "intel", "nvidia corporation", "realtek", "amd", "defender", "internetexplorer",
        "classes", "policies", "clients",
    };
    private static bool IsDeniedName(string name)
    {
        string n = name.ToLowerInvariant();
        return Denied.Any(d => n == d || n.Contains(d));
    }

    private static readonly string[] Stop =
    {
        "software", "inc", "corp", "corporation", "ltd", "llc", "the", "com", "technologies",
        "systems", "group", "limited", "gmbh", "team", "project", "free", "windows", "microsoft",
        "update", "version", "edition", "application", "program", "install", "setup",
    };

    private static List<string> Tokens(string name, string publisher)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (name + " " + publisher).Split(new[] { ' ', '-', '_', '.', ',', '(', ')', '/', '\\', '&', '+' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string w = new string(raw.Where(char.IsLetter).ToArray());
            if (w.Length >= 4 && !Stop.Contains(w.ToLowerInvariant())) set.Add(w.ToLowerInvariant());
        }
        return set.ToList();
    }

    private static bool Matches(string candidate, List<string> tokens)
    {
        string c = candidate.ToLowerInvariant();
        return tokens.Any(t => c.Contains(t));
    }

    private static long DirSize(string dir)
    {
        try
        {
            long total = 0;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                try { total += new FileInfo(f).Length; } catch { }
            return total;
        }
        catch { return 0; }
    }

    private static string HiveLabel(RegistryHive hive, RegistryView view) =>
        (hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM") + (view == RegistryView.Registry32 ? " (32 bits)" : "");
}
