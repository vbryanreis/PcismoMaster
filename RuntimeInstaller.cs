using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace ResetExplode;

/// <summary>
/// Baixa e instala as bibliotecas para jogos das fontes combinadas com o Pedro:
/// Visual C++ = pacote "AIO" do abbodi1406/vcredist no GitHub (todas as versões 2005–2022 num instalador só), conferido pelo
/// SHA-256 que o GitHub publica para o arquivo; DirectX = "DirectX End-User Runtime" (junho de 2010) do site da Microsoft,
/// com a assinatura digital da Microsoft conferida antes de abrir.
/// </summary>
public static class RuntimeInstaller
{
    public const string VcRepoPage = "https://github.com/abbodi1406/vcredist/releases/latest";
    private const string VcApi = "https://api.github.com/repos/abbodi1406/vcredist/releases/latest";
    public const string DxPage = "https://www.microsoft.com/en-us/download/details.aspx?id=8109";
    private const string DxUrl = "https://download.microsoft.com/download/8/4/A/84A35BF1-DAFE-4AE8-82AF-AD2AE20B6B14/directx_Jun2010_redist.exe";

    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        return c;
    }

    private static string Folder => Path.Combine(Path.GetTempPath(), "PcismoMaster", "bibliotecas");

    /// <summary>Visual C++ AIO: baixa a última versão, confere o SHA-256 e roda em modo passivo (/y: mostra o progresso, instala todas).</summary>
    public static async Task<string> InstallVisualCppAsync(Action<string, double> progress, CancellationToken ct)
    {
        progress("Consultando a última versão do Visual C++ AIO (abbodi1406/vcredist)…", 0.05);
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(VcApi, ct));
        string tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        string want = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "VisualCppRedist_AIO-arm64.exe" : "VisualCppRedist_AIO_x86_x64.exe";
        string? url = null, digest = null;
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (!string.Equals(a.GetProperty("name").GetString(), want, StringComparison.OrdinalIgnoreCase)) continue;
            url = a.GetProperty("browser_download_url").GetString();
            digest = a.TryGetProperty("digest", out var d) ? d.GetString() : null;
        }
        if (url == null || !url.StartsWith("https://github.com/abbodi1406/vcredist/releases/download/", StringComparison.OrdinalIgnoreCase))
            return $"Não achei o arquivo {want} na última versão do repositório. Abra a página e baixe à mão.";
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return "O GitHub não informou o SHA-256 do arquivo; sem como conferir, nada foi baixado. Abra a página e baixe à mão.";

        string file = Path.Combine(Folder, want);
        await DownloadAsync(url, file, $"Visual C++ AIO {tag}", progress, ct);
        progress("Conferindo o SHA-256…", 0.92);
        string hash = await Task.Run(() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), ct);
        if (!hash.Equals(digest[7..], StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(file);
            return "O arquivo baixado não bate com o SHA-256 publicado no GitHub. Apagado; nada foi instalado.";
        }
        progress("Instalando o Visual C++ (todas as versões)…", 0.95);
        int code = await RunAsync(file, "/y", ct);
        TryDelete(file);
        return code is 0 or 3010
            ? $"Visual C++ AIO {tag} instalado." + (code == 3010 ? " Reinicie o PC para concluir." : "")
            : $"O instalador do Visual C++ terminou com o código {code}.";
    }

    /// <summary>DirectX junho de 2010: baixa o pacote completo da Microsoft, confere a assinatura, extrai e roda o DXSETUP em silêncio.</summary>
    public static async Task<string> InstallDirectXAsync(Action<string, double> progress, CancellationToken ct)
    {
        string file = Path.Combine(Folder, "directx_Jun2010_redist.exe");
        await DownloadAsync(DxUrl, file, "DirectX (junho de 2010)", progress, ct);
        progress("Conferindo a assinatura digital da Microsoft…", 0.9);
        if (!await Task.Run(() => SignedByMicrosoft(file), ct)) { TryDelete(file); return "O pacote do DirectX não tem assinatura válida da Microsoft. Apagado; nada foi instalado."; }

        string dir = Path.Combine(Folder, "directx");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        progress("Extraindo…", 0.93);
        await RunAsync(file, $"/Q /T:\"{dir}\"", ct);
        string setup = Path.Combine(dir, "DXSETUP.exe");
        if (!File.Exists(setup) || !SignedByMicrosoft(setup)) return "Não foi possível extrair o DirectX (ou o DXSETUP não tem assinatura da Microsoft).";
        progress("Instalando o DirectX…", 0.96);
        int code = await RunAsync(setup, "/silent", ct);
        TryDelete(file);
        try { Directory.Delete(dir, true); } catch { }
        return code == 0 ? "DirectX (junho de 2010) instalado." : $"O instalador do DirectX terminou com o código {code}.";
    }

    private static bool SignedByMicrosoft(string path)
    {
        try
        {
            if (CorsairSdkInstaller.WinVerifyTrustFile(path) != 0) return false;
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            return cert.Subject.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static async Task DownloadAsync(string url, string file, string what, Action<string, double> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? 0;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(file);
        var buf = new byte[1 << 16];
        long done = 0; int r; var sw = Stopwatch.StartNew(); long lastReport = 0;
        while ((r = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, r), ct);
            done += r;
            if (sw.ElapsedMilliseconds - lastReport < 250) continue; // não inunda a tela de atualizações
            lastReport = sw.ElapsedMilliseconds;
            progress($"Baixando {what}… {done / 1048576.0:F0} de {(total > 0 ? (total / 1048576.0).ToString("F0") : "?")} MB",
                     total > 0 ? 0.1 + 0.8 * done / total : 0.5);
        }
    }

    private static async Task<int> RunAsync(string exe, string args, CancellationToken ct)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! })
                      ?? throw new InvalidOperationException("O instalador não abriu.");
        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    }

    private static void TryDelete(string f) { try { File.Delete(f); } catch { } }
}
