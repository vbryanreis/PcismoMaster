using System.IO;

namespace ResetExplode;

/// <summary>
/// Localiza ferramentas externas de linha de comando (yt-dlp, ffmpeg) que foram instaladas pela aba
/// "Instalar apps" (winget). Não embute nem baixa nada: só encontra o executável na pasta de atalhos do
/// winget (pacotes portáteis) ou no PATH do sistema.
/// </summary>
public static class ExternalTools
{
    /// <summary>Pasta de atalhos (shims) que o winget cria para pacotes portáteis como yt-dlp e ffmpeg.</summary>
    public static string WinGetLinks =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links");

    /// <summary>Caminho do executável (ex.: "yt-dlp") na pasta Links do winget ou no PATH; null se não achar.</summary>
    public static string? Find(string exe)
    {
        string file = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe : exe + ".exe";

        string link = Path.Combine(WinGetLinks, file);
        if (File.Exists(link)) return link;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try { string p = Path.Combine(dir.Trim(), file); if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    public static bool Has(string exe) => Find(exe) != null;
}
