using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ResetExplode;

/// <summary>
/// OSD sobre o jogo (estilo RTSS/Afterburner): janela transparente, sempre no topo, que ignora cliques
/// e nunca rouba o foco. Aparece sobre jogos em tela cheia "sem bordas" e na maioria dos DX11/12 em
/// tela cheia (que o Windows apresenta via flip); não aparece em tela cheia exclusiva legada.
/// </summary>
public partial class OverlayWindow : Window
{
    public string Corner { get; set; } = "TL"; // TL, TC, TR, BL, BC, BR
    /// <summary>true = barra horizontal numa linha só; false = lista vertical com gráfico.</summary>
    public bool Horizontal { get; set; }

    private IntPtr _hwnd, _hook;
    private IntPtr _monitor;                  // monitor do jogo (o da última janela ativa de outro programa)
    private readonly WinEventProc _onForeground; // guardado num campo para o GC não coletar o callback

    public OverlayWindow()
    {
        InitializeComponent();
        _onForeground = (_, _, hwnd, _, _, _, _) => { TrackGame(hwnd); KeepOnTop(); };
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            SetWindowDisplayAffinity(_hwnd, _captureVisible ? WDA_NONE : WDA_EXCLUDEFROMCAPTURE);
        };
        SizeChanged += (_, _) => Reposition();
        // Só escuta a troca de janela ativa enquanto o overlay está na tela.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && _hook == IntPtr.Zero)
            {
                _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _onForeground, 0, 0, WINEVENT_OUTOFCONTEXT);
                TrackGame(GetForegroundWindow());
                KeepOnTop();
            }
            else if (!IsVisible) Unhook();
        };
        Closed += (_, _) => Unhook();
    }

    public void SetScale(double s) { Scale.ScaleX = Scale.ScaleY = s; }

    /// <summary>Opacidade do fundo (0 = transparente, 1 = preto sólido); o texto continua opaco.</summary>
    public void SetBackgroundOpacity(double a) =>
        Box.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), 0, 0, 0));

    public void Reposition() => KeepOnTop();

    /// <summary>
    /// Cola no canto escolhido do monitor do jogo (área total, para ficar sobre jogos em tela cheia) e volta para o topo.
    /// Jogos em janela sem borda (GTA V, Cyberpunk 2077...) também se marcam como "sempre no topo" e, ao serem ativados,
    /// passam na frente; por isso isto roda a cada troca de janela ativa e a cada atualização do overlay.
    /// </summary>
    public void KeepOnTop()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible) return;
        if (_monitor == IntPtr.Zero) _monitor = MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(_monitor, ref mi) || !GetWindowRect(_hwnd, out var wr)) return;

        // Tudo em pixels do mesmo sistema de coordenadas (GetMonitorInfo e GetWindowRect), sem conversão de DPI.
        int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
        int m = (int)Math.Round(12 * VisualTreeHelper.GetDpi(this).DpiScaleX);
        var r = mi.rcMonitor;
        int x = Corner.EndsWith("R") ? r.Right - w - m : Corner.EndsWith("C") ? r.Left + (r.Right - r.Left - w) / 2 : r.Left + m;
        int y = Corner.StartsWith("B") ? r.Bottom - h - m : r.Top + m;
        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    /// <summary>Segue o monitor da janela ativa, a não ser que seja do próprio PcismoMaster.</summary>
    private void TrackGame(IntPtr fg)
    {
        if (fg == IntPtr.Zero) return;
        GetWindowThreadProcessId(fg, out uint pid);
        if (pid == (uint)Environment.ProcessId) return;
        _monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
    }

    private void Unhook()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    /// <summary>Atualiza o overlay. <paramref name="show"/> = itens escolhidos (chaves de OverlayMetrics).</summary>
    public void Update(FrameStats? f, OverlayLive? live, IReadOnlyList<OverlayPart> parts, IReadOnlySet<string> show, string? hour, OverlayStyle st)
    {
        ApplyStyle(st);
        VPanel.Visibility = Horizontal ? Visibility.Collapsed : Visibility.Visible;
        HRow.Visibility = Horizontal ? Visibility.Visible : Visibility.Collapsed;
        var cells = Cells(f, live, parts, show, hour, st);
        if (Horizontal) { HRow.ItemsSource = cells; return; }
        // Vertical: tabela (rótulo à esquerda, valor à direita) e, se escolhidos, os gráficos de frametime e FPS embaixo.
        VRows.ItemsSource = cells;
        bool graph = show.Contains("graph"), fpsGraph = show.Contains("fpsgraph");
        OGraph.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
        OGraphFps.Visibility = fpsGraph ? Visibility.Visible : Visibility.Collapsed;
        if (graph) DrawGraph(OGraph, f?.RecentFrameTimes, false, st.Fps);
        if (fpsGraph) DrawGraph(OGraphFps, f?.RecentFrameTimes, true, st.Value);
    }

    private OverlayStyle? _style;

    /// <summary>Fonte e contorno do texto; só mexe na árvore visual quando mudam.</summary>
    private void ApplyStyle(OverlayStyle st)
    {
        if (_style is { } old && old.Font == st.Font && old.Shadow == st.Shadow) { _style = st; return; }
        _style = st;
        Root.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily(st.Font));
        Root.Effect = st.Shadow ? new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, BlurRadius = 3, Opacity = 1, Color = Colors.Black } : null;
    }

    /// <summary>false = some de capturas de tela, gravações e transmissões (Windows 10 2004 ou mais novo).</summary>
    public void SetCaptureVisible(bool visible)
    {
        _captureVisible = visible;
        if (_hwnd == IntPtr.Zero) return;
        SetWindowDisplayAffinity(_hwnd, visible ? WDA_NONE : WDA_EXCLUDEFROMCAPTURE);
    }
    private bool _captureVisible;

    private static readonly Brush Plain = Frozen(0xF5, 0xF5, 0xF5), Rec = Frozen(0xF8, 0x71, 0x71),
        Cool = Frozen(0x4A, 0xDE, 0x80), Warm = Frozen(0xFB, 0xBF, 0x24), Hot = Frozen(0xF8, 0x71, 0x71);
    private static Brush Frozen(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

    /// <summary>Células do overlay (também usadas na prévia da aba Overlay): estado, FPS, estatísticas, sensores e jogo.</summary>
    public static List<OverlayCell> Cells(FrameStats? f, OverlayLive? live, IReadOnlyList<OverlayPart> parts, IReadOnlySet<string> show,
                                          string? hour, OverlayStyle st)
    {
        var cells = new List<OverlayCell>();
        if (live?.Status is { Length: > 0 } status) cells.Add(new("●", status, Rec, Rec));
        string N(double? v) => v is double x && x > 0 ? $"{x:0}" : "—";
        if (show.Contains("fps")) cells.Add(new("FPS", N(f?.Fps), st.Fps, st.Label));
        if (show.Contains("frametime")) cells.Add(new("FRAME", f != null ? $"{f.FrameTimeMs:0.0} ms" : "—", st.Value, st.Label));
        if (show.Contains("low1")) cells.Add(new("1% LOW", N(f?.Low1Fps), st.Value, st.Label));
        if (show.Contains("low01")) cells.Add(new("0,1% LOW", N(f?.Low01Fps), st.Value, st.Label));
        if (show.Contains("fpsmin")) cells.Add(new("MÍN", f != null ? N(live?.Min) : "—", st.Value, st.Label));
        if (show.Contains("fpsavg")) cells.Add(new("MÉD", f != null ? N(live?.Avg) : "—", st.Value, st.Label));
        if (show.Contains("fpsmax")) cells.Add(new("MÁX", f != null ? N(live?.Max) : "—", st.Value, st.Label));
        if (show.Contains("jitter")) cells.Add(new("VARIAÇÃO", f != null ? $"±{f.JitterMs:0.0} ms" : "—", st.Value, st.Label));
        foreach (var p in parts)
            cells.Add(new(p.Short, p.Bare, p.Level switch { 1 => Cool, 2 => Warm, 3 => Hot, _ => st.Value }, st.Label));
        if (show.Contains("game") && f != null) cells.Add(new("JOGO", f.ProcessName, st.Value, st.Label));
        if (hour != null) cells.Add(new("HORA", hour, st.Value, st.Label));
        return cells;
    }

    private static readonly Brush GridLine = Frozen(0x40, 0x40, 0x40);

    /// <summary>Frametimes recentes (picos para cima = engasgos) ou o FPS de cada frame (vales = quedas).</summary>
    private static void DrawGraph(System.Windows.Controls.Canvas c, IReadOnlyList<double>? ft, bool asFps, Brush stroke)
    {
        c.Children.Clear();
        double w = c.ActualWidth > 0 ? c.ActualWidth : 200, h = c.Height;
        c.Children.Add(new Line { X1 = 0, X2 = w, Y1 = h - 0.5, Y2 = h - 0.5, Stroke = GridLine });
        if (ft == null || ft.Count < 2) return;

        var values = asFps ? ft.Select(x => x > 0 ? 1000 / x : 0).ToList() : (IReadOnlyList<double>)ft;
        // Frametime: escala que mostra picos sem achatar o normal. FPS: até o maior valor, com folga.
        double max = asFps ? Math.Max(1, values.Max() * 1.1) : Math.Max(values.Max(), values.Average() * 2);
        var line = new Polyline { Stroke = stroke, StrokeThickness = 1.2 };
        double step = w / (values.Count - 1);
        for (int i = 0; i < values.Count; i++)
            line.Points.Add(new Point(i * step, h - 1 - (h - 2) * Math.Min(1, values[i] / max)));
        c.Children.Add(line);
    }

    private const uint WDA_NONE = 0, WDA_EXCLUDEFROMCAPTURE = 0x11;
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int index, int value);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, WINEVENT_OUTOFCONTEXT = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    private delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}

/// <summary>Célula do overlay em barra: rótulo pequeno, valor e a cor do valor.</summary>
public sealed record OverlayCell(string Label, string Value, Brush Fg, Brush LabelFg);

/// <summary>Mínimo, média e máximo desde que o jogo abriu, e o aviso de benchmark/gravação em andamento.</summary>
public sealed record OverlayLive(double Min, double Avg, double Max, string? Status);

/// <summary>Aparência do overlay: cores do FPS, dos valores e dos rótulos, fonte e contorno no texto.</summary>
public sealed record OverlayStyle(Brush Fps, Brush Value, Brush Label, string Font, bool Shadow)
{
    public static OverlayStyle From(AppSettings s) => new(Parse(s.OverlayFpsColor, "#A66BFF"), Parse(s.OverlayValueColor, "#F5F5F5"),
        Parse(s.OverlayLabelColor, "#8C8C8C"), string.IsNullOrWhiteSpace(s.OverlayFont) ? "Segoe UI" : s.OverlayFont, s.OverlayShadow);

    private static readonly Dictionary<string, Brush> Cache = new();
    public static Brush Parse(string? hex, string fallback)
    {
        string key = string.IsNullOrWhiteSpace(hex) ? fallback : hex;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var b)) return b;
            try { b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(key)); }
            catch { b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback)); }
            b.Freeze();
            return Cache[key] = b;
        }
    }
}
