using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;
using System.Text.Json;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>App do catálogo "Instalar apps" (pós-formatação), instalado pelo winget.</summary>
public sealed class InstallableApp : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Categoria da sub-aba (ex.: "Navegadores", "Segurança", "Imagem"), no estilo do Ninite.</summary>
    public string Category { get; init; } = "";
    public string WingetId { get; init; } = "";
    /// <summary>Começo do nome em "Aplicativos instalados" (Painel de Controle), para saber se já está instalado.</summary>
    public string DisplayPrefix { get; init; } = "";
    /// <summary>Para pacotes portáteis do winget (yt-dlp, ffmpeg), que não aparecem em "Aplicativos instalados":
    /// nome do executável a procurar na pasta Links do winget ou no PATH. Quando preenchido, substitui o DisplayPrefix.</summary>
    public string ProbeExe { get; init; } = "";
    /// <summary>Apps fora do winget (ex.: ArtCraft): repositório GitHub "dono/repo" cuja última release traz o instalador.
    /// Quando preenchido, o app é baixado e instalado direto do GitHub oficial, não pelo winget.</summary>
    public string DownloadRepo { get; init; } = "";
    /// <summary>Final do nome do arquivo da release a baixar (ex.: "windows-x64.msi").</summary>
    public string AssetSuffix { get; init; } = "";
    /// <summary>Com o catálogo grande (estilo Ninite) nada vem pré-marcado: o usuário escolhe.</summary>
    public bool PreSelect { get; init; } = false;

    /// <summary>Site do fabricante (só o domínio), de onde vem o ícone do cartão.</summary>
    public string Site => AppInstaller.Sites.GetValueOrDefault(WingetId, "");
    /// <summary>Inicial mostrada no quadrado enquanto o ícone não carrega (ou se não houver ícone).</summary>
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";
    /// <summary>DisplayIcon do app instalado (plano B do ícone).</summary>
    public string LocalIcon { get; set; } = "";

    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set { Set(ref _icon, value); OnChanged(nameof(HasIcon)); } }
    public bool HasIcon => _icon != null;

    private bool _selected, _installed;
    private string _status = "";

    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public bool Installed
    {
        get => _installed;
        set { Set(ref _installed, value); OnChanged(nameof(CanSelect)); }
    }
    public bool CanSelect => !Installed;
    public string Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(p);
    }
}

/// <summary>
/// Catálogo de apps essenciais e instalação silenciosa pelo winget (Gerenciador de Pacotes do Windows), que baixa
/// sempre a versão oficial mais recente do site do fabricante e confere o hash do instalador.
/// </summary>
public static class AppInstaller
{
    /// <summary>Ordem das sub-abas de categoria (inspirada no Ninite). "Todos" é adicionado na interface.</summary>
    public static readonly string[] Categories =
    {
        "Navegadores", "Mensagens", "Mídia", "Imagem", "Documentos",
        "Segurança", "Compactação", "Utilitários", "Desenvolvimento", "Jogos",
        "ArtCraft",
    };

    public static List<InstallableApp> Catalog() => new()
    {
        // ---- Navegadores ----
        new() { Category = "Navegadores", Name = "Google Chrome", Description = "Navegador do Google.", WingetId = "Google.Chrome", DisplayPrefix = "Google Chrome" },
        new() { Category = "Navegadores", Name = "Mozilla Firefox", Description = "Navegador livre da Mozilla.", WingetId = "Mozilla.Firefox", DisplayPrefix = "Mozilla Firefox" },
        new() { Category = "Navegadores", Name = "Brave", Description = "Navegador focado em privacidade, bloqueia anúncios.", WingetId = "Brave.Brave", DisplayPrefix = "Brave" },
        new() { Category = "Navegadores", Name = "Opera", Description = "Navegador com VPN e recursos extras.", WingetId = "Opera.Opera", DisplayPrefix = "Opera" },

        // ---- Mensagens ----
        new() { Category = "Mensagens", Name = "Discord", Description = "Chat de voz e texto para jogar com os amigos.", WingetId = "Discord.Discord", DisplayPrefix = "Discord" },
        new() { Category = "Mensagens", Name = "Zoom", Description = "Reuniões e chamadas de vídeo.", WingetId = "Zoom.Zoom", DisplayPrefix = "Zoom" },
        new() { Category = "Mensagens", Name = "Telegram", Description = "Mensageiro rápido e seguro.", WingetId = "Telegram.TelegramDesktop", DisplayPrefix = "Telegram" },

        // ---- Mídia ----
        new() { Category = "Mídia", Name = "VLC", Description = "Player de vídeo e música que abre qualquer formato.", WingetId = "VideoLAN.VLC", DisplayPrefix = "VLC media player" },
        new() { Category = "Mídia", Name = "Spotify", Description = "Streaming de músicas e podcasts.", WingetId = "Spotify.Spotify", DisplayPrefix = "Spotify" },
        new() { Category = "Mídia", Name = "Audacity", Description = "Editor de áudio gratuito.", WingetId = "Audacity.Audacity", DisplayPrefix = "Audacity" },
        new() { Category = "Mídia", Name = "HandBrake", Description = "Conversor de vídeo (recodifica para mp4, etc.).", WingetId = "HandBrake.HandBrake", DisplayPrefix = "HandBrake" },
        new() { Category = "Mídia", Name = "iTunes", Description = "Player e gerenciador de mídia da Apple.", WingetId = "Apple.iTunes", DisplayPrefix = "iTunes" },
        new() { Category = "Mídia", Name = "yt-dlp", Description = "Motor de download de vídeos/áudio (usado pela aba Utilidades · Baixar vídeos). Grátis e open-source.", WingetId = "yt-dlp.yt-dlp", ProbeExe = "yt-dlp" },
        new() { Category = "Mídia", Name = "ffmpeg", Description = "Conversor de áudio/vídeo que o yt-dlp usa para gerar mp3/mp4. Grátis e open-source.", WingetId = "Gyan.FFmpeg", ProbeExe = "ffmpeg" },

        // ---- Imagem ----
        new() { Category = "Imagem", Name = "GIMP", Description = "Editor de imagens completo, alternativa ao Photoshop.", WingetId = "GIMP.GIMP", DisplayPrefix = "GIMP" },
        new() { Category = "Imagem", Name = "Paint.NET", Description = "Editor de imagens leve e fácil.", WingetId = "dotPDN.PaintDotNet", DisplayPrefix = "paint.net" },
        new() { Category = "Imagem", Name = "Krita", Description = "Desenho e pintura digital.", WingetId = "KDE.Krita", DisplayPrefix = "Krita" },
        new() { Category = "Imagem", Name = "Inkscape", Description = "Editor de vetores (SVG).", WingetId = "Inkscape.Inkscape", DisplayPrefix = "Inkscape" },
        new() { Category = "Imagem", Name = "IrfanView", Description = "Visualizador de imagens rápido.", WingetId = "IrfanSkiljan.IrfanView", DisplayPrefix = "IrfanView" },
        new() { Category = "Imagem", Name = "ShareX", Description = "Captura de tela e gravação com upload.", WingetId = "ShareX.ShareX", DisplayPrefix = "ShareX" },
        new() { Category = "Imagem", Name = "Greenshot", Description = "Captura de tela com anotações.", WingetId = "Greenshot.Greenshot", DisplayPrefix = "Greenshot" },
        new() { Category = "Imagem", Name = "Blender", Description = "Modelagem e animação 3D.", WingetId = "BlenderFoundation.Blender", DisplayPrefix = "Blender" },

        // ---- Documentos ----
        new() { Category = "Documentos", Name = "LibreOffice", Description = "Pacote de escritório completo (texto, planilha, apresentação).", WingetId = "TheDocumentFoundation.LibreOffice", DisplayPrefix = "LibreOffice" },
        new() { Category = "Documentos", Name = "SumatraPDF", Description = "Leitor de PDF leve e rápido.", WingetId = "SumatraPDF.SumatraPDF", DisplayPrefix = "Sumatra" },
        new() { Category = "Documentos", Name = "Foxit Reader", Description = "Leitor de PDF com anotações.", WingetId = "Foxit.FoxitReader", DisplayPrefix = "Foxit" },
        new() { Category = "Documentos", Name = "Adobe Acrobat Reader", Description = "Leitor de PDF oficial da Adobe.", WingetId = "Adobe.Acrobat.Reader.64-bit", DisplayPrefix = "Adobe Acrobat" },

        // ---- Segurança ----
        new() { Category = "Segurança", Name = "Malwarebytes", Description = "Antimalware que remove pragas e adware.", WingetId = "Malwarebytes.Malwarebytes", DisplayPrefix = "Malwarebytes" },

        // ---- Compactação ----
        new() { Category = "Compactação", Name = "7-Zip", Description = "Compactador gratuito (zip, rar, 7z). Abre e cria arquivos compactados.", WingetId = "7zip.7zip", DisplayPrefix = "7-Zip" },
        new() { Category = "Compactação", Name = "PeaZip", Description = "Compactador livre com muitos formatos.", WingetId = "Giorgiotani.Peazip", DisplayPrefix = "PeaZip" },
        new() { Category = "Compactação", Name = "WinRAR", Description = "Compactador clássico (pago após o teste). Escolha ele ou o 7-Zip.", WingetId = "RARLab.WinRAR", DisplayPrefix = "WinRAR" },

        // ---- Utilitários ----
        new() { Category = "Utilitários", Name = "Lightshot", Description = "Captura de tela rápida com a tecla Print Screen.", WingetId = "Skillbrains.Lightshot", DisplayPrefix = "Lightshot" },
        new() { Category = "Utilitários", Name = "Everything", Description = "Busca instantânea de arquivos (voidtools).", WingetId = "voidtools.Everything", DisplayPrefix = "Everything" },
        new() { Category = "Utilitários", Name = "TeamViewer", Description = "Acesso remoto e suporte.", WingetId = "TeamViewer.TeamViewer", DisplayPrefix = "TeamViewer" },
        new() { Category = "Utilitários", Name = "KeePassXC", Description = "Cofre de senhas offline.", WingetId = "KeePassXCTeam.KeePassXC", DisplayPrefix = "KeePassXC" },
        new() { Category = "Utilitários", Name = "WinDirStat", Description = "Mostra o que está ocupando espaço no disco.", WingetId = "WinDirStat.WinDirStat", DisplayPrefix = "WinDirStat" },

        // ---- Desenvolvimento ----
        new() { Category = "Desenvolvimento", Name = "Visual Studio Code", Description = "Editor de código da Microsoft.", WingetId = "Microsoft.VisualStudioCode", DisplayPrefix = "Microsoft Visual Studio Code" },
        new() { Category = "Desenvolvimento", Name = "Git", Description = "Controle de versão.", WingetId = "Git.Git", DisplayPrefix = "Git" },
        new() { Category = "Desenvolvimento", Name = "Python 3", Description = "Linguagem Python (interpretador e pip).", WingetId = "Python.Python.3.12", DisplayPrefix = "Python 3" },
        new() { Category = "Desenvolvimento", Name = "Notepad++", Description = "Editor de texto e código leve.", WingetId = "Notepad++.Notepad++", DisplayPrefix = "Notepad++" },
        new() { Category = "Desenvolvimento", Name = "PuTTY", Description = "Cliente SSH/Telnet.", WingetId = "PuTTY.PuTTY", DisplayPrefix = "PuTTY" },
        new() { Category = "Desenvolvimento", Name = "WinSCP", Description = "Transferência de arquivos SFTP/FTP.", WingetId = "WinSCP.WinSCP", DisplayPrefix = "WinSCP" },

        // ---- Jogos ----
        new() { Category = "Jogos", Name = "Steam", Description = "Loja e biblioteca de jogos da Valve.", WingetId = "Valve.Steam", DisplayPrefix = "Steam" },
        new() { Category = "Jogos", Name = "Epic Games", Description = "Loja e launcher da Epic (Fortnite, jogos grátis).", WingetId = "EpicGames.EpicGamesLauncher", DisplayPrefix = "Epic Games Launcher" },

        // ---- ArtCraft (apps de criação da ArtCraft; não estão no winget: baixamos o .msi oficial da última release no GitHub) ----
        new() { Category = "ArtCraft", Name = "ArtCraft Launcher", Description = "Central oficial da ArtCraft: instala e mantém atualizados todos os apps de criação de uma vez. É o caminho recomendado pelo próprio site.", DownloadRepo = "storytold/craft-launcher", AssetSuffix = "windows-x64.msi", DisplayPrefix = "ArtCraft Launcher" },
        new() { Category = "ArtCraft", Name = "PhotoCraft", Description = "Editor de imagens open-source, no estilo do Photoshop (early alpha).", DownloadRepo = "storytold/photocraft", AssetSuffix = "windows-x64.msi", DisplayPrefix = "PhotoCraft" },
        new() { Category = "ArtCraft", Name = "PdfCraft", Description = "Central de PDF: ler, organizar, juntar, dividir e proteger PDFs (early alpha).", DownloadRepo = "storytold/pdfcraft", AssetSuffix = "windows-x64.msi", DisplayPrefix = "PdfCraft" },
        new() { Category = "ArtCraft", Name = "FigCraft", Description = "Design de interfaces e prototipagem, no estilo do Figma (early alpha).", DownloadRepo = "storytold/figcraft", AssetSuffix = "windows-x64.msi", DisplayPrefix = "FigCraft" },
        new() { Category = "ArtCraft", Name = "Craft Anything", Description = "Explora como um software funciona e ajuda a criar a sua própria versão (early alpha).", DownloadRepo = "storytold/craft-anything", AssetSuffix = "windows-x64.msi", DisplayPrefix = "Craft Anything" },
    };

    public static string WingetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");

    public static bool WingetAvailable => File.Exists(WingetPath);

    /// <summary>Domínio do site de cada app (o ícone do cartão vem dele).</summary>
    public static readonly Dictionary<string, string> Sites = new()
    {
        ["Google.Chrome"] = "chrome.google.com", ["Mozilla.Firefox"] = "firefox.com", ["Brave.Brave"] = "brave.com", ["Opera.Opera"] = "opera.com",
        ["Discord.Discord"] = "discord.com", ["Zoom.Zoom"] = "zoom.us", ["Telegram.TelegramDesktop"] = "telegram.org",
        ["VideoLAN.VLC"] = "videolan.org", ["Spotify.Spotify"] = "spotify.com", ["Audacity.Audacity"] = "audacityteam.org",
        ["HandBrake.HandBrake"] = "handbrake.fr", ["Apple.iTunes"] = "music.apple.com", ["yt-dlp.yt-dlp"] = "yt-dlp.org", ["Gyan.FFmpeg"] = "ffmpeg.org",
        ["GIMP.GIMP"] = "gimp.org", ["dotPDN.PaintDotNet"] = "getpaint.net", ["KDE.Krita"] = "krita.org", ["Inkscape.Inkscape"] = "inkscape.org",
        ["IrfanSkiljan.IrfanView"] = "irfanview.com", ["ShareX.ShareX"] = "getsharex.com", ["Greenshot.Greenshot"] = "getgreenshot.org",
        ["BlenderFoundation.Blender"] = "blender.org", ["TheDocumentFoundation.LibreOffice"] = "libreoffice.org",
        ["SumatraPDF.SumatraPDF"] = "sumatrapdfreader.org", ["Foxit.FoxitReader"] = "foxit.com", ["Adobe.Acrobat.Reader.64-bit"] = "acrobat.adobe.com",
        ["Malwarebytes.Malwarebytes"] = "malwarebytes.com", ["7zip.7zip"] = "7-zip.org", ["Giorgiotani.Peazip"] = "peazip.github.io",
        ["RARLab.WinRAR"] = "win-rar.com", ["Skillbrains.Lightshot"] = "prnt.sc", ["voidtools.Everything"] = "voidtools.com",
        ["TeamViewer.TeamViewer"] = "teamviewer.com", ["KeePassXCTeam.KeePassXC"] = "keepassxc.org", ["WinDirStat.WinDirStat"] = "windirstat.net",
        ["Microsoft.VisualStudioCode"] = "code.visualstudio.com", ["Git.Git"] = "git-scm.com", ["Python.Python.3.12"] = "python.org",
        ["Notepad++.Notepad++"] = "notepad-plus-plus.org", ["PuTTY.PuTTY"] = "putty.org", ["WinSCP.WinSCP"] = "winscp.net",
        ["Valve.Steam"] = "store.steampowered.com", ["EpicGames.EpicGamesLauncher"] = "store.epicgames.com",
    };

    /// <summary>Ícone direto quando o do Google/DuckDuckGo não serve.</summary>
    public static readonly Dictionary<string, string> IconUrls = new()
    {
        ["VideoLAN.VLC"] = "https://www.videolan.org/favicon.ico",
    };

    /// <summary>Nomes em "Aplicativos instalados" (máquina 64/32 bits e usuário atual).</summary>
    public static HashSet<string> InstalledNames() => InstalledEntries().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nome → DisplayIcon de cada entrada em "Aplicativos instalados".</summary>
    public static Dictionary<string, string> InstalledEntries()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
                                             (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n && !string.IsNullOrWhiteSpace(n))
                        names.TryAdd(n.Trim(), k.GetValue("DisplayIcon") as string ?? "");
                }
            }
            catch { }
        }
        return names;
    }

    public static void RefreshInstalled(IEnumerable<InstallableApp> apps)
    {
        var names = InstalledEntries();
        foreach (var a in apps)
        {
            var entry = a.DisplayPrefix.Length > 0
                ? names.FirstOrDefault(n => n.Key.StartsWith(a.DisplayPrefix, StringComparison.OrdinalIgnoreCase))
                : default;
            a.Installed = a.ProbeExe.Length > 0 ? ExternalTools.Has(a.ProbeExe) : entry.Key != null;
            a.LocalIcon = entry.Value ?? "";
            if (a.Installed) a.Selected = false;
        }
    }

    /// <summary>Instala um app em silêncio. Retorna (ok, mensagem curta).</summary>
    public static async Task<(bool Ok, string Message)> InstallAsync(InstallableApp app, CancellationToken ct, bool upgrade = false)
    {
        // Apps fora do winget (ArtCraft): baixa o .msi oficial da última release no GitHub e instala em silêncio.
        if (app.DownloadRepo.Length > 0) return await InstallDirectAsync(app, ct);

        var psi = new ProcessStartInfo(WingetPath,
            $"{(upgrade ? "upgrade" : "install")} --id {app.WingetId} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o winget.");
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }
        string output = await outTask + await errTask;

        return p.ExitCode switch
        {
            0 => (true, "Instalado."),
            unchecked((int)0x8A15002B) or unchecked((int)0x8A150061) => (true, "Já estava instalado."),
            unchecked((int)0x8A150014) => (false, "Pacote não encontrado no winget."),
            unchecked((int)0x8A150008) => (false, "Falha no download: confira a internet e tente de novo."),
            _ => (false, $"Falhou (código 0x{p.ExitCode:X8}). {LastLine(output)}".Trim()),
        };
    }

    // ---- Apps fora do winget (ArtCraft): download direto do .msi oficial no GitHub + instalação silenciosa ----

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(30) }; // apps de criação podem ser grandes
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>
    /// Instala um app ArtCraft: acha o instalador (<see cref="InstallableApp.AssetSuffix"/>) na última release do
    /// repositório oficial, baixa, confere a assinatura digital pelo Windows e roda o msiexec em silêncio.
    /// O progresso aparece no <see cref="InstallableApp.Status"/> do próprio item.
    /// </summary>
    private static async Task<(bool Ok, string Message)> InstallDirectAsync(InstallableApp app, CancellationToken ct)
    {
        // 1) Descobrir o instalador da última versão publicada.
        app.Status = "Procurando a última versão…";
        string tag = "?", url = "", assetName = "";
        try
        {
            string json = await Http.GetStringAsync($"https://api.github.com/repos/{app.DownloadRepo}/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("tag_name", out var t)) tag = t.GetString() ?? "?";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(app.AssetSuffix, StringComparison.OrdinalIgnoreCase) &&
                        a.TryGetProperty("browser_download_url", out var u) && u.GetString() is string link && link.StartsWith("https://"))
                    {
                        assetName = name; url = link; break;
                    }
                }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return (false, $"Não consegui consultar o GitHub: {ex.Message}"); }
        if (url.Length == 0) return (false, $"A versão {tag} ainda não tem instalador do Windows ({app.AssetSuffix}).");

        string tmpDir = Path.Combine(Path.GetTempPath(), "PcismoMaster-artcraft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        string msiPath = Path.Combine(tmpDir, assetName);
        try
        {
            // 2) Baixar o .msi (mostrando o progresso no item).
            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(msiPath);
                var buf = new byte[81920];
                long done = 0; int read; long lastReport = 0;
                while ((read = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read), ct);
                    done += read;
                    if (done - lastReport > 1_000_000)
                    {
                        lastReport = done;
                        app.Status = total > 0
                            ? $"Baixando {tag}… {done / 1_048_576.0:0.0} de {total / 1_048_576.0:0.0} MB"
                            : $"Baixando {tag}… {done / 1_048_576.0:0.0} MB";
                    }
                }
            }

            // 3) Conferir a assinatura digital antes de rodar (o Windows valida o Authenticode do arquivo).
            app.Status = "Verificando o instalador…";
            int trust = CorsairSdkInstaller.WinVerifyTrustFile(msiPath);
            // Sem assinatura: só avisa (o arquivo veio da release oficial no GitHub, por HTTPS). Assinatura inválida: cancela.
            bool unsigned = trust is unchecked((int)0x800B0100) or unchecked((int)0x800B0003);
            string note = unsigned ? " (instalador sem assinatura digital; origem: release oficial no GitHub)" : "";
            if (!unsigned && trust != 0)
                return (false, $"A assinatura digital do instalador é inválida (0x{trust:X8}). Nada foi instalado.");

            // 4) Instalar em silêncio pelo msiexec.
            app.Status = "Instalando…";
            var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{msiPath}\" /qn /norestart")
            {
                UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o msiexec.");
            try { await p.WaitForExitAsync(ct); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }

            return p.ExitCode switch
            {
                0 => (true, $"Instalado ({tag}).{note}"),
                3010 => (true, $"Instalado ({tag}); reinicie o Windows para concluir.{note}"), // ERROR_SUCCESS_REBOOT_REQUIRED
                1638 => (true, "Já estava instalado (ou já há uma versão instalada)."),         // ERROR_PRODUCT_VERSION
                1602 => (false, "Instalação cancelada."),                                       // ERROR_INSTALL_USEREXIT
                1618 => (false, "Outra instalação está em andamento. Aguarde e tente de novo."),// ERROR_INSTALL_ALREADY_RUNNING
                1603 => (false, "Falha na instalação (1603). Feche o app se estiver aberto e tente de novo."),
                _ => (false, $"Falhou (código {p.ExitCode})."),
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return (false, $"Falhou: {ex.Message}"); }
        finally { try { Directory.Delete(tmpDir, true); } catch { } }
    }

    /// <summary>Última linha útil da saída do winget (sem as barras de progresso).</summary>
    private static string LastLine(string output) =>
        output.Split('\n', '\r').Select(l => l.Trim())
              .LastOrDefault(l => l.Length > 3 && !l.All(c => "-\\|/█▒ ".Contains(c)) && !l.Contains("KB /") && !l.Contains("MB /")) ?? "";
}
