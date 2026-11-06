using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ResetExplode;

/// <summary>Versão nova pronta para instalar (já baixada e com o SHA-256 conferido).</summary>
public sealed record PendingUpdate(Version Version, string Tag, string File, string Notes);

/// <summary>
/// Atualização automática pelas releases do GitHub (vbryanreis/PcismoMaster). Procura a última release, baixa o
/// PcismoMaster-X.Y.Z.exe, confere o SHA-256 que o GitHub publica e, quando a pessoa aceita, troca o .exe e reabre.
/// O Windows deixa renomear um .exe em uso: o atual vira .old, o novo entra no lugar e o .old some na próxima abertura.
/// </summary>
public static class AppUpdater
{
    private const string Api = "https://api.github.com/repos/vbryanreis/PcismoMaster/releases/latest";
    private const string Prefix = "https://github.com/vbryanreis/PcismoMaster/releases/download/";

    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        return c;
    }

    public static Version Current { get; } = Assembly.GetExecutingAssembly().GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : new Version(0, 0, 0);

    /// <summary>Só o .exe publicado (arquivo único) se atualiza; o build de desenvolvimento (dotnet build) fica de fora.</summary>
#pragma warning disable IL3000 // de propósito: Location vazio é justamente o sinal de que este é o .exe de arquivo único
    public static bool Supported =>
        string.IsNullOrEmpty(typeof(AppUpdater).Assembly.Location) && Environment.ProcessPath is { } p && p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
#pragma warning restore IL3000

    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Atualizacao");

    /// <summary>Procura e baixa. null = já está na última versão.</summary>
    public static async Task<PendingUpdate?> CheckAndDownloadAsync(IProgress<double>? progress, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(Api, ct));
        var r = doc.RootElement;
        string tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= Current) return null;

        string? url = null, digest = null, name = null;
        foreach (var a in r.GetProperty("assets").EnumerateArray())
        {
            string n = a.GetProperty("name").GetString() ?? "";
            if (!n.Equals($"PcismoMaster-{latest.ToString(3)}.exe", StringComparison.OrdinalIgnoreCase)) continue;
            url = a.GetProperty("browser_download_url").GetString();
            digest = a.TryGetProperty("digest", out var d) ? d.GetString() : null;
            name = n;
            break;
        }
        if (url == null || !url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"a versão {tag} ainda não tem o .exe publicado");
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("o GitHub não informou o SHA-256 do arquivo");
        string notes = r.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

        Directory.CreateDirectory(Folder);
        string file = Path.Combine(Folder, name!);
        // Já baixado numa abertura anterior? Confere e reaproveita.
        if (File.Exists(file) && await HashMatchesAsync(file, digest, ct)) return new PendingUpdate(latest, tag, file, notes);

        string part = file + ".part";
        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(part);
            var buf = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report((double)done / total);
            }
        }
        if (!await HashMatchesAsync(part, digest, ct))
        {
            File.Delete(part);
            throw new InvalidOperationException("o arquivo baixado não bate com o SHA-256 do GitHub; nada foi instalado");
        }
        File.Move(part, file, true);
        // Apaga downloads de versões antigas.
        foreach (var old in Directory.EnumerateFiles(Folder, "PcismoMaster-*.exe*"))
            if (!old.Equals(file, StringComparison.OrdinalIgnoreCase)) try { File.Delete(old); } catch { }
        return new PendingUpdate(latest, tag, file, notes);
    }

    private static async Task<bool> HashMatchesAsync(string path, string digest, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).Equals(digest[7..], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Troca o .exe pelo novo e abre a nova versão. Depois disso o app atual precisa fechar.</summary>
    public static void Install(PendingUpdate update)
    {
        string exe = Environment.ProcessPath!;
        string old = exe + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(exe, old);
        try { File.Copy(update.File, exe); }
        catch { File.Move(old, exe); throw; } // devolve o atual se a cópia falhar
        Process.Start(new ProcessStartInfo(exe, $"--after-update {Environment.ProcessId}") { UseShellExecute = true });
    }

    /// <summary>Na abertura: apaga o .exe antigo que ficou da última atualização.</summary>
    public static void CleanupOld()
    {
        if (Environment.ProcessPath is not { } exe || !File.Exists(exe + ".old")) return;
        // O Windows pode segurar o .exe antigo alguns segundos depois de o processo fechar: tenta de novo em segundo plano.
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                try { File.Delete(exe + ".old"); return; } catch { }
                await Task.Delay(1000);
            }
        });
    }
}
