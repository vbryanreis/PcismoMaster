using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ResetExplode;

/// <summary>
/// Ferramentas da NVIDIA que acompanham cada instalação do driver: o Painel de Controle NVIDIA (app da Microsoft Store,
/// instalado pelo winget) e o NVIDIA Profile Inspector (Orbmu2k, MIT), baixado da última release oficial no GitHub com o
/// SHA-256 conferido. Antes ficavam embutidos no app; agora vêm sempre na versão mais nova.
/// </summary>
public static class NvidiaTools
{
    private const string NpiApi = "https://api.github.com/repos/Orbmu2k/nvidiaProfileInspector/releases/latest";
    private const string NpiPrefix = "https://github.com/Orbmu2k/nvidiaProfileInspector/releases/download/";

    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        return c;
    }

    /// <summary>Pasta onde o Profile Inspector fica (Arquivos do PcismoMaster, não some com a limpeza de temporários).</summary>
    public static string NpiFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Ferramentas", "nvidiaProfileInspector");
    public static string NpiExe => Path.Combine(NpiFolder, "nvidiaProfileInspector.exe");
    public static bool HasProfileInspector => File.Exists(NpiExe);

    /// <summary>Versão do Profile Inspector baixado (gravada ao lado do exe), ou "" se não houver.</summary>
    public static string ProfileInspectorVersion
    {
        get { try { return File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : ""; } catch { return ""; } }
    }
    private static string VersionFile => Path.Combine(NpiFolder, "versao.txt");

    /// <summary>
    /// Baixa a última versão do Profile Inspector (se a que está aqui for mais antiga), confere o SHA-256 publicado pelo GitHub,
    /// extrai na pasta do app e cria o atalho na Área de Trabalho. Devolve o resumo para a tela.
    /// </summary>
    public static async Task<string> UpdateProfileInspectorAsync(CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(NpiApi, ct));
        string tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        if (tag.Length > 0 && tag == ProfileInspectorVersion && HasProfileInspector) { TryShortcut(); return $"Profile Inspector {tag} já está atualizado."; }

        string? url = null, digest = null;
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (!(a.GetProperty("name").GetString() ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            url = a.GetProperty("browser_download_url").GetString();
            digest = a.TryGetProperty("digest", out var d) ? d.GetString() : null;
            break;
        }
        if (url == null || !url.StartsWith(NpiPrefix, StringComparison.OrdinalIgnoreCase)) return "Profile Inspector: a última versão no GitHub não tem o pacote .zip.";
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return "Profile Inspector: o GitHub não informou o SHA-256; sem como conferir, nada foi baixado.";

        byte[] zip = await Http.GetByteArrayAsync(url, ct);
        if (!Convert.ToHexString(SHA256.HashData(zip)).Equals(digest[7..], StringComparison.OrdinalIgnoreCase))
            return "Profile Inspector: o arquivo baixado não bate com o SHA-256 do GitHub. Nada foi instalado.";

        await Task.Run(() =>
        {
            string tmp = NpiFolder + ".novo";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            using (var ms = new MemoryStream(zip)) ZipFile.ExtractToDirectory(ms, tmp);
            // O .zip às vezes traz uma pasta dentro: usa a que tiver o exe.
            string src = Directory.EnumerateFiles(tmp, "nvidiaProfileInspector.exe", SearchOption.AllDirectories).Select(Path.GetDirectoryName).FirstOrDefault()
                         ?? throw new InvalidOperationException("o pacote não tem o nvidiaProfileInspector.exe");
            if (Directory.Exists(NpiFolder)) Directory.Delete(NpiFolder, true);
            Directory.Move(src!, NpiFolder);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            File.WriteAllText(VersionFile, tag);
        }, ct);
        TryShortcut();
        return $"Profile Inspector {tag} baixado do GitHub oficial (atalho na Área de Trabalho).";
    }

    private static void TryShortcut()
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null || !HasProfileInspector) return;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic lnk = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NVIDIA Profile Inspector.lnk"));
            lnk.TargetPath = NpiExe;
            lnk.WorkingDirectory = NpiFolder;
            lnk.Save();
        }
        catch { }
    }

    public static void OpenProfileInspector() => Process.Start(new ProcessStartInfo(NpiExe) { UseShellExecute = true, WorkingDirectory = NpiFolder });

    /// <summary>
    /// Instala o Painel de Controle NVIDIA pela Microsoft Store (winget, origem msstore). Se o winget não existir ou falhar,
    /// abre a página dele na Store. Devolve o resumo para a tela.
    /// </summary>
    public static async Task<string> InstallControlPanelAsync(CancellationToken ct)
    {
        if (GraphicsTweaks.HasNvidiaControlPanel()) { TryPanelShortcut(); return "Painel de Controle NVIDIA já instalado."; }
        if (AppInstaller.WingetAvailable)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(AppInstaller.WingetPath,
                    "install --id 9NF8H0H7WMLT --source msstore -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
                    { UseShellExecute = false, CreateNoWindow = true })!;
                await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(10), ct);
                if (GraphicsTweaks.HasNvidiaControlPanel() || p.ExitCode == 0) { TryPanelShortcut(); return "Painel de Controle NVIDIA instalado pela Microsoft Store."; }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { DriverClean.Log("Painel NVIDIA pelo winget: " + ex.Message); }
        }
        try { Process.Start(new ProcessStartInfo(GraphicsTweaks.NvidiaControlPanelStore) { UseShellExecute = true }); } catch { }
        return "Não deu para instalar o Painel NVIDIA sozinho: a página dele na Microsoft Store foi aberta, clique em Obter.";
    }

    private static void TryPanelShortcut() { try { GraphicsTweaks.CreateNvidiaControlPanelShortcut(); } catch { } }

    /// <summary>Os dois juntos, depois de instalar o driver NVIDIA. Nunca lança: devolve o resumo das duas.</summary>
    public static async Task<string> AfterDriverInstallAsync(CancellationToken ct)
    {
        string panel, npi;
        try { panel = await InstallControlPanelAsync(ct); } catch (Exception ex) { panel = "Painel NVIDIA: " + ex.Message; }
        try { npi = await UpdateProfileInspectorAsync(ct); } catch (Exception ex) { npi = "Profile Inspector: " + ex.Message; }
        return panel + " " + npi;
    }
}
