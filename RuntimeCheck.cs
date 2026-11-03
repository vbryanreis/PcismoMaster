using System.IO;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Uma biblioteca para jogos e se ela está instalada em 32 e 64 bits.</summary>
public sealed record RuntimeStatus(string Name, bool X86, bool X64, string Note = "");

/// <summary>
/// Só confere se as bibliotecas que os jogos pedem estão instaladas (Visual C++ 2005 a 2015-2022 e o DirectX de junho de 2010).
/// A instalação fica na aba Instalar apps; aqui nada é baixado nem alterado.
/// </summary>
public static class RuntimeCheck
{
    public static List<RuntimeStatus> Read()
    {
        var names = InstalledNames();
        bool Has(string year, bool x64) => names.Any(n => n.Contains("Visual C++ " + year, StringComparison.OrdinalIgnoreCase)
                                                          && n.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                                                          && (x64 ? n.Contains("x64") : !n.Contains("x64")));
        var list = new List<RuntimeStatus>();
        foreach (var year in new[] { "2005", "2008", "2010", "2012", "2013" })
            list.Add(new RuntimeStatus("Visual C++ " + year, Has(year, false), Has(year, true)));

        // 2015 a 2022 são uma só biblioteca (14.x): o registro dela diz se está instalada e a versão.
        static string? Vc14(RegistryView view)
        {
            using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey($@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{(view == RegistryView.Registry64 ? "x64" : "x86")}");
            return k?.GetValue("Installed") is int i && i == 1 ? k.GetValue("Version") as string : null;
        }
        string? v86 = Vc14(RegistryView.Registry32), v64 = Vc14(RegistryView.Registry64);
        list.Add(new RuntimeStatus("Visual C++ 2015-2022", v86 != null, v64 != null, (v64 ?? v86)?.TrimStart('v') ?? ""));

        // DirectX 12 já vem no Windows; jogos antigos pedem as DLLs do "DirectX End-User Runtime" (junho de 2010).
        string sys = Environment.SystemDirectory, wow = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        bool Dx(string dir) => new[] { "d3dx9_43.dll", "d3dx10_43.dll", "d3dx11_43.dll", "xinput1_3.dll", "XAudio2_7.dll" }.All(f => File.Exists(Path.Combine(dir, f)));
        list.Add(new RuntimeStatus("DirectX (junho de 2010)", Dx(wow), Dx(sys), "DirectX 12 já vem no Windows"));
        return list;
    }

    private static List<string> InstalledNames()
    {
        var names = new List<string>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (var sub in root?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var k = root!.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n) names.Add(n);
                }
            }
            catch { }
        }
        return names;
    }
}
