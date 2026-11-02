using System.Diagnostics;
using System.IO;
using System.Text;

namespace ResetExplode;

/// <summary>
/// Baixa áudio (mp3) ou vídeo (mp4) de um link (YouTube, Instagram etc.) usando o yt-dlp + ffmpeg, instalados
/// pela aba "Instalar apps". Nenhum binário é embutido no PcismoMaster.
/// </summary>
public static class MediaDownloader
{
    /// <summary>true quando yt-dlp e ffmpeg estão disponíveis (instalados pela aba Instalar apps ou no PATH).</summary>
    public static bool Ready => ExternalTools.Has("yt-dlp") && ExternalTools.Has("ffmpeg");

    /// <summary>Pasta padrão de saída: Downloads\PcismoMaster do usuário.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "PcismoMaster");

    /// <summary>
    /// Baixa o link. <paramref name="format"/> = "mp3" (áudio) ou "mp4" (vídeo). <paramref name="onLine"/> recebe cada
    /// linha de saída do yt-dlp (progresso), chamada fora da thread da UI. Retorna (ok, mensagem curta).
    /// </summary>
    public static async Task<(bool Ok, string Message)> DownloadAsync(
        string url, string format, string outDir, Action<string> onLine, CancellationToken ct)
    {
        string? ytdlp = ExternalTools.Find("yt-dlp");
        string? ffmpeg = ExternalTools.Find("ffmpeg");
        if (ytdlp == null) return (false, "yt-dlp não encontrado. Instale na aba Instalar apps.");
        if (ffmpeg == null) return (false, "ffmpeg não encontrado. Instale na aba Instalar apps.");
        if (string.IsNullOrWhiteSpace(url)) return (false, "Cole um link primeiro.");

        string ffdir = Path.GetDirectoryName(ffmpeg) ?? "";
        Directory.CreateDirectory(outDir);
        // Limita o título a 200 bytes para não estourar o limite de caminho do Windows; inclui o id para não sobrescrever.
        string outTemplate = Path.Combine(outDir, "%(title).200B [%(id)s].%(ext)s");

        var args = new List<string>
        {
            "--no-playlist", "--newline", "--no-part", "--ignore-config",
            "--ffmpeg-location", ffdir,
            "-o", outTemplate,
        };
        if (format == "mp3")
            args.AddRange(new[] { "-x", "--audio-format", "mp3", "--audio-quality", "0", "--embed-metadata" });
        else // mp4
            args.AddRange(new[] { "-f", "bv*+ba/b", "--merge-output-format", "mp4", "--embed-metadata" });
        args.Add(url);

        var psi = new ProcessStartInfo(ytdlp)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o yt-dlp.");
        p.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }

        return p.ExitCode == 0
            ? (true, "Concluído.")
            : (false, $"yt-dlp terminou com erro (código {p.ExitCode}). Confira o link e a internet.");
    }
}
