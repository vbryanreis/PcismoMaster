using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ResetExplode;

/// <summary>
/// Ícones de apps para as abas Instalar apps e Desinstalar Apps. Tudo sai congelado (Freeze), então pode ser montado
/// fora da thread da UI. Ícones da internet ficam guardados em %LocalAppData%\PcismoMaster\appicons e só são baixados
/// uma vez; ícone pequeno demais (16 px) é descartado para não aparecer borrado.
/// </summary>
public static class AppIcons
{
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(typeof(AppIcons).Assembly.GetName().Name ?? "app");
        return c;
    }

    private static string CacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "appicons");

    /// <summary>Ícone do site do app: cache em disco → favicon do Google (64 px) → DuckDuckGo → endereço direto. Null se nenhum tiver 32 px ou mais.</summary>
    public static async Task<ImageSource?> FromWebAsync(string key, string domain, string directUrl = "")
    {
        string file = Path.Combine(CacheDir, Safe(key) + ".png");
        try
        {
            if (File.Exists(file)) return Best(await File.ReadAllBytesAsync(file));
            if (File.Exists(file + ".none") && File.GetLastWriteTimeUtc(file + ".none") > DateTime.UtcNow.AddDays(-7)) return null;
        }
        catch { }

        var urls = new List<string>();
        if (directUrl.Length > 0) urls.Add(directUrl);
        if (domain.Length > 0)
        {
            urls.Add($"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=64");
            urls.Add($"https://icons.duckduckgo.com/ip3/{domain}.ico");
        }
        foreach (var url in urls)
        {
            try
            {
                byte[] data = await Http.GetByteArrayAsync(url);
                if (Best(data) is not BitmapSource bmp || bmp.PixelWidth < 32) continue;
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                Directory.CreateDirectory(CacheDir);
                using (var fs = File.Create(file)) enc.Save(fs);
                return bmp;
            }
            catch { }
        }
        try { Directory.CreateDirectory(CacheDir); File.WriteAllText(file + ".none", ""); } catch { } // tenta de novo em uma semana
        return null;
    }

    /// <summary>Maior quadro do arquivo (ico tem vários tamanhos), congelado.</summary>
    private static BitmapSource? Best(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource? best = null;
            foreach (var f in dec.Frames)
                if (best == null || f.PixelWidth > best.PixelWidth) best = f;
            if (best == null) return null;
            if (best.PixelWidth > 64) // reduz: a UI mostra 32 px; menos memória
            {
                double s = 64.0 / best.PixelWidth;
                best = new TransformedBitmap(best, new ScaleTransform(s, s));
            }
            var frozen = BitmapFrame.Create(best);
            frozen.Freeze();
            return frozen;
        }
        catch { return null; }
    }

    /// <summary>
    /// Ícone de um arquivo do PC no formato do registro ("C:\app\app.exe,0", com ou sem aspas, .exe/.dll/.ico).
    /// Usado com o DisplayIcon das chaves de desinstalação.
    /// </summary>
    public static ImageSource? FromFile(string spec, int size = 48)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        string path = spec.Trim();
        int index = 0;
        int comma = path.LastIndexOf(',');
        if (comma > 0 && int.TryParse(path[(comma + 1)..].Trim(), out int idx)) { index = idx; path = path[..comma]; }
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try { if (!File.Exists(path)) return null; } catch { return null; }

        IntPtr[] icons = new IntPtr[1];
        try
        {
            if (PrivateExtractIcons(path, index, size, size, icons, out _, 1, 0) == 0 || icons[0] == IntPtr.Zero) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
        finally { if (icons[0] != IntPtr.Zero) DestroyIcon(icons[0]); }
    }

    private static string Safe(string key) => string.Concat(key.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, out uint ids, uint count, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr h);
}
