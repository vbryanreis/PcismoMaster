using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ResetExplode;

/// <summary>
/// Captura de tela (PNG) e gravação de vídeo do monitor do jogo, sem injetar nada no jogo:
/// a foto é tirada da tela já composta pelo Windows (como a tecla Print Screen) e o vídeo usa o ffmpeg
/// (instalado em Instalar apps) com a Duplicação de Área de Trabalho do Windows. Jogos em tela cheia
/// exclusiva antiga podem sair pretos; em janela sem borda e DX11/12 modernos funciona.
/// </summary>
public static class ScreenCapture
{
    public static string ShotsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PcismoMaster");
    public static string VideosDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "PcismoMaster");

    /// <summary>Nome de arquivo seguro: "jogo_2026-11-05_21-30-12".</summary>
    public static string FileStem(string? game)
    {
        string g = string.IsNullOrWhiteSpace(game) ? "Tela" : game;
        foreach (char c in Path.GetInvalidFileNameChars()) g = g.Replace(c, '_');
        return $"{g}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
    }

    /// <summary>Retângulo (pixels) e posição na lista de monitores do monitor da janela ativa.</summary>
    public static (int X, int Y, int W, int H, int Index) ForegroundMonitor()
    {
        IntPtr mon = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY);
        var all = new List<(IntPtr H, string Name)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(h, ref info)) all.Add((h, info.szDevice));
            return true;
        }, IntPtr.Zero);
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        GetMonitorInfo(mon, ref mi);
        var r = mi.rcMonitor;
        // A Duplicação de Área de Trabalho numera as saídas na ordem DISPLAY1, DISPLAY2…
        int index = all.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList().FindIndex(m => m.H == mon);
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, Math.Max(0, index));
    }

    /// <summary>Tira a foto do monitor do jogo e grava em PNG (a gravação do arquivo roda em segundo plano). Devolve o caminho.</summary>
    public static async Task<string> ScreenshotAsync(string? game)
    {
        var (x, y, w, h, _) = ForegroundMonitor();
        BitmapSource shot = Grab(x, y, w, h);
        Directory.CreateDirectory(ShotsDir);
        string file = Path.Combine(ShotsDir, FileStem(game) + ".png");
        await Task.Run(() =>
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(shot));
            using var fs = File.Create(file);
            enc.Save(fs);
        });
        return file;
    }

    private static BitmapSource Grab(int x, int y, int w, int h)
    {
        IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), bmp = CreateCompatibleBitmap(screen, w, h);
        IntPtr old = SelectObject(mem, bmp);
        try
        {
            // CAPTUREBLT inclui janelas em camadas; o overlay só aparece se a opção "mostrar nas capturas" estiver ligada.
            BitBlt(mem, 0, 0, w, h, screen, x, y, SRCCOPY | CAPTUREBLT);
            SelectObject(mem, old);
            var src = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            DeleteObject(bmp); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>Argumentos do ffmpeg: Duplicação de Área de Trabalho (ddagrab) e NVENC na NVIDIA, senão x264 rápido.</summary>
    public static string FfmpegArgs(int outputIndex, int fps, bool nvenc, string file)
    {
        string input = $"-f lavfi -i ddagrab=output_idx={outputIndex}:framerate={fps}:draw_mouse=0";
        string enc = nvenc
            ? "-c:v h264_nvenc -preset p4 -rc vbr -cq 23 -b:v 0"
            : "-vf hwdownload,format=bgra -c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p";
        return $"-hide_banner -loglevel error {input} {enc} -y \"{file}\"";
    }

    /// <summary>Gravação em andamento (um ffmpeg por vez). Para com "q" no ffmpeg, que fecha o MP4 direitinho.</summary>
    public sealed class Recorder
    {
        private Process? _p;
        private readonly StringBuilder _err = new();
        public string? File { get; private set; }
        public DateTime Started { get; private set; }
        public bool Running => _p is { HasExited: false };

        /// <summary>Começa a gravar o monitor do jogo. Devolve null se começou, ou o motivo de não ter começado.</summary>
        public async Task<string?> StartAsync(string? game, int fps, bool nvenc)
        {
            if (Running) return "Já está gravando.";
            string? ffmpeg = ExternalTools.Find("ffmpeg");
            if (ffmpeg == null) return "Instale o ffmpeg na aba Instalar apps para gravar vídeo.";
            Directory.CreateDirectory(VideosDir);
            File = Path.Combine(VideosDir, FileStem(game) + ".mp4");
            var (_, _, _, _, index) = ForegroundMonitor();
            string? why = await Launch(ffmpeg, FfmpegArgs(index, fps, nvenc, File));
            if (why != null && nvenc) why = await Launch(ffmpeg, FfmpegArgs(index, fps, false, File)); // NVENC indisponível: tenta x264
            return why;
        }

        private async Task<string?> Launch(string ffmpeg, string args)
        {
            _err.Clear();
            var psi = new ProcessStartInfo(ffmpeg, args)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8 };
            var p = new Process { StartInfo = psi };
            p.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } d) lock (_err) if (_err.Length < 4000) _err.AppendLine(d); };
            try { p.Start(); } catch (Exception ex) { p.Dispose(); return "Não deu para abrir o ffmpeg: " + ex.Message; }
            p.BeginErrorReadLine();
            // Se o ffmpeg não aceitar a captura (versão antiga, codificador ausente), ele fecha logo.
            await Task.WhenAny(p.WaitForExitAsync(), Task.Delay(1500));
            if (p.HasExited)
            {
                string msg; lock (_err) msg = _err.ToString().Trim();
                p.Dispose();
                return "O ffmpeg não conseguiu gravar" + (msg.Length > 0 ? ": " + msg.Split('\n')[^1].Trim() : ".");
            }
            _p = p;
            Started = DateTime.Now;
            return null;
        }

        /// <summary>Para a gravação e devolve o arquivo.</summary>
        public async Task<string?> StopAsync()
        {
            var p = _p;
            _p = null;
            if (p == null) return null;
            try
            {
                if (!p.HasExited) { await p.StandardInput.WriteAsync("q"); await p.StandardInput.FlushAsync(); }
                var done = p.WaitForExitAsync();
                if (await Task.WhenAny(done, Task.Delay(8000)) != done) p.Kill(entireProcessTree: true);
            }
            catch { try { p.Kill(entireProcessTree: true); } catch { } }
            finally { p.Dispose(); }
            return File;
        }

        /// <summary>Ao fechar o app: encerra o ffmpeg sem esperar.</summary>
        public void Abort()
        {
            try { if (_p is { HasExited: false } p) { p.StandardInput.Write("q"); p.StandardInput.Flush(); if (!p.WaitForExit(3000)) p.Kill(true); } } catch { }
        }
    }

    private const int SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }
    private delegate bool MonitorEnumProc(IntPtr h, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFOEX mi);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
}
