using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ResetExplode;

public sealed record FrameStats(int ProcessId, string ProcessName, double Fps, double FrameTimeMs, double Low1Fps,
                                IReadOnlyList<double> RecentFrameTimes, double Low01Fps = 0, double JitterMs = 0);

/// <summary>
/// Mede o FPS de qualquer processo sem injetar código, como o PresentMon: escuta via ETW os eventos
/// "Present" que o Windows emite a cada frame entregue por DXGI (DirectX 10/11/12) e Direct3D 9.
/// Jogos Vulkan/OpenGL só aparecem se apresentarem via DXGI (comum em drivers recentes, não garantido).
/// </summary>
public sealed class FrameMonitor : IDisposable
{
    private const string SessionName = "PcismoMaster-Frames";
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private const int DxgiPresentStart = 42, D3D9PresentStart = 1;
    private const double HistoryMs = 10_000; // janela para o 1% low

    private TraceEventSession? _session;
    private Thread? _thread;
    private readonly object _lock = new();
    private readonly Dictionary<int, Queue<double>> _presents = new(); // pid → timestamps (ms, relógio do ETW)
    private readonly Dictionary<int, long> _lastSeen = new();           // pid → Stopwatch do último evento recebido

    public bool Running => _thread?.IsAlive == true;
    public string? Error { get; private set; }

    public void Start()
    {
        if (Running) return;
        try
        {
            // Opção Create derruba uma sessão antiga com o mesmo nome (ex.: app fechado à força).
            _session = new TraceEventSession(SessionName, TraceEventSessionOptions.Create) { StopOnDispose = true };
            _session.EnableProvider(DxgiProvider, TraceEventLevel.Informational, ulong.MaxValue, null);
            _session.EnableProvider(D3D9Provider, TraceEventLevel.Informational, ulong.MaxValue, null);
            _session.Source.AllEvents += OnEvent;
            _thread = new Thread(() =>
            {
                try { _session.Source.Process(); }
                catch (Exception ex) { Error = ex.Message; }
            }) { IsBackground = true, Name = "ETW frames" };
            _thread.Start();
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _session?.Dispose();
            _session = null;
        }
    }

    private void OnEvent(TraceEvent e)
    {
        int id = (int)e.ID;
        bool present = (e.ProviderGuid == DxgiProvider && id == DxgiPresentStart) ||
                       (e.ProviderGuid == D3D9Provider && id == D3D9PresentStart);
        if (!present) return;

        double t = e.TimeStampRelativeMSec;
        lock (_lock)
        {
            if (!_presents.TryGetValue(e.ProcessID, out var q)) _presents[e.ProcessID] = q = new Queue<double>();
            q.Enqueue(t);
            while (q.Count > 0 && q.Peek() < t - HistoryMs) q.Dequeue();
            _lastSeen[e.ProcessID] = Stopwatch.GetTimestamp();
            if (_rec != null && e.ProcessID == _recPid && _rec.Count < MaxRecordedFrames) _rec.Add(t);
        }
    }

    /// <summary>Estatísticas do processo, ou null se ele não apresentou frames nos últimos 2 s.</summary>
    public FrameStats? GetStats(int pid)
    {
        double[] ts;
        lock (_lock)
        {
            if (!_presents.TryGetValue(pid, out var q) || q.Count < 3) return null;
            if (Stopwatch.GetElapsedTime(_lastSeen[pid]).TotalSeconds > 2) return null;
            ts = q.ToArray();
        }

        double last = ts[^1];
        var frameTimes = FrameMath.FrameTimes(ts);

        // FPS do último segundo (pelo relógio dos eventos, imune ao atraso de entrega do ETW).
        int first = Array.FindIndex(ts, x => x >= last - 1000);
        double span = last - ts[first];
        int frames = ts.Length - 1 - first;
        double fps = span > 0 ? frames * 1000 / span : 0;

        // 1% low: FPS equivalente ao percentil 99 do frametime nos últimos 10 s.
        // 0,1% low = percentil 99,9; variação = desvio padrão do frametime, ambos na mesma janela.
        var sorted = (double[])frameTimes.Clone();
        Array.Sort(sorted);
        double low1 = FrameMath.LowFps(sorted, 0.99), low01 = FrameMath.LowFps(sorted, 0.999);

        var recent = frameTimes.Skip(Math.Max(0, frameTimes.Length - 120)).ToList();
        return new FrameStats(pid, ProcessName(pid), fps, fps > 0 ? 1000 / fps : 0, low1, recent, low01, FrameMath.Jitter(frameTimes));
    }

    // ---------------- gravação para benchmark ----------------

    private const int MaxRecordedFrames = 2_000_000; // ~3 h a 165 FPS (16 MB); depois para de guardar
    private List<double>? _rec;
    private int _recPid;

    public bool Recording { get { lock (_lock) return _rec != null; } }

    /// <summary>Começa a guardar o instante de cada frame do processo (para o benchmark).</summary>
    public void StartRecording(int pid) { lock (_lock) { _rec = new List<double>(16384); _recPid = pid; } }

    /// <summary>Para a gravação e devolve os instantes (ms); null se não estava gravando.</summary>
    public List<double>? StopRecording() { lock (_lock) { var r = _rec; _rec = null; return r; } }

    /// <summary>Processo da janela ativa (0 = nenhum ou o próprio app).</summary>
    public static int ForegroundPid()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == Environment.ProcessId ? 0 : (int)pid;
    }

    /// <summary>Estatísticas do programa em primeiro plano (o jogo que está na tela).</summary>
    public FrameStats? GetForegroundStats()
    {
        if (Stopwatch.GetElapsedTime(_lastPrune).TotalSeconds > 30) { _lastPrune = Stopwatch.GetTimestamp(); Prune(); }
        int pid = ForegroundPid();
        return pid == 0 ? null : GetStats(pid);
    }

    private readonly Dictionary<int, string> _names = new();

    /// <summary>Nome do processo, guardado por PID (antes abria um Process novo, sem liberar, várias vezes por segundo).</summary>
    public string ProcessName(int pid)
    {
        lock (_names)
        {
            if (_names.TryGetValue(pid, out var name)) return name;
            try { using var p = Process.GetProcessById(pid); name = p.ProcessName; }
            catch { name = $"PID {pid}"; }
            return _names[pid] = name;
        }
    }

    /// <summary>Esquece processos que não apresentam frames há mais de 30 s (navegador, Discord, jogos já fechados).</summary>
    private void Prune()
    {
        lock (_lock)
        {
            foreach (int pid in _lastSeen.Where(kv => Stopwatch.GetElapsedTime(kv.Value).TotalSeconds > 30).Select(kv => kv.Key).ToList())
            {
                _lastSeen.Remove(pid); _presents.Remove(pid);
                lock (_names) _names.Remove(pid);
            }
        }
    }
    private long _lastPrune = Stopwatch.GetTimestamp();

    public void Dispose()
    {
        try { _session?.Source.StopProcessing(); } catch { }
        _session?.Dispose();
        _session = null;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
