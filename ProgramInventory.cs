using System.IO;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Um programa instalado, lido do registro de "Programas e Recursos" (chaves Uninstall).</summary>
public sealed record InstalledProgram(
    string Name, string Version, string Publisher, string InstallLocation,
    string UninstallCmd, bool Quiet, RegistryHive Hive, RegistryView View, string KeyPath, long SizeKb)
{
    public string SizeText => SizeKb <= 0 ? "" : SizeKb >= 1048576 ? $"{SizeKb / 1048576.0:0.0} GB" : $"{SizeKb / 1024.0:0} MB";
    public string Meta => string.Join("   ·   ", new[] { Publisher, Version, SizeText }.Where(s => s.Length > 0));
}

/// <summary>
/// Lê a lista de programas instalados como o "Programas e Recursos" do Windows: só aplicativos de verdade, escondendo
/// componentes do sistema, atualizações/patches e entradas sem desinstalador. Não remove nada — só lista.
/// </summary>
public static class ProgramInventory
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public static List<InstalledProgram> Read()
    {
        var list = new List<InstalledProgram>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser,  RegistryView.Default),
        };

        foreach (var (hive, view) in sources)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var unins = baseKey.OpenSubKey(UninstallRoot);
                if (unins == null) continue;

                foreach (var subName in unins.GetSubKeyNames())
                {
                    try
                    {
                        using var k = unins.OpenSubKey(subName);
                        if (k == null) continue;

                        string name = (k.GetValue("DisplayName") as string ?? "").Trim();
                        if (name.Length == 0) continue;

                        string quietCmd = (k.GetValue("QuietUninstallString") as string ?? "").Trim();
                        string unCmd = (k.GetValue("UninstallString") as string ?? "").Trim();
                        if (quietCmd.Length == 0 && unCmd.Length == 0) continue; // sem desinstalador: não listamos

                        // Filtros de segurança (mesma visão do "Programas e Recursos"):
                        if (ToInt(k.GetValue("SystemComponent")) == 1) continue;            // componente do sistema
                        if (((k.GetValue("ParentKeyName") as string) ?? "").Length > 0) continue; // patch/sub-item
                        if ((k.GetValue("ReleaseType") as string) is "Security Update" or "Update" or "Hotfix" or "ServicePack") continue;
                        if (IsUpdateName(name)) continue;

                        string ver = (k.GetValue("DisplayVersion") as string ?? "").Trim();
                        if (!seen.Add(name + "|" + ver)) continue; // dedupe (mesma entrada em 64/32 bits)

                        string pub = (k.GetValue("Publisher") as string ?? "").Trim();
                        string loc = (k.GetValue("InstallLocation") as string ?? "").Trim();
                        long sizeKb = ToLong(k.GetValue("EstimatedSize"));
                        bool quiet = quietCmd.Length > 0;

                        list.Add(new InstalledProgram(name, ver, pub, loc, quiet ? quietCmd : unCmd, quiet,
                                                      hive, view, UninstallRoot + "\\" + subName, sizeKb));
                    }
                    catch { }
                }
            }
            catch { }
        }
        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsUpdateName(string name)
    {
        string n = name.TrimStart();
        return n.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Update for", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Hotfix", StringComparison.OrdinalIgnoreCase)
            || n.Contains("(KB", StringComparison.OrdinalIgnoreCase);
    }

    private static int ToInt(object? v) => v is int i ? i : 0;
    private static long ToLong(object? v) => v is int i ? i : v is long l ? l : 0;
}
