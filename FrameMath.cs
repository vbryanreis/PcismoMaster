using System.Globalization;
using System.Text;

namespace ResetExplode;

/// <summary>Resultado de um benchmark (como o do RivaTuner/CapFrameX). FPS mínimo e máximo = pior e melhor segundo.</summary>
public sealed record BenchSummary(string Game, DateTime Start, double Seconds, int Frames, double Avg, double Min, double Max,
                                  double Low1, double Low01, string? CsvPath = null);

/// <summary>Contas de FPS a partir dos instantes (ms) em que cada frame foi apresentado. Sem estado: fácil de testar.</summary>
public static class FrameMath
{
    /// <summary>Frametimes (ms) entre apresentações seguidas.</summary>
    public static double[] FrameTimes(IReadOnlyList<double> ts)
    {
        if (ts.Count < 2) return Array.Empty<double>();
        var ft = new double[ts.Count - 1];
        for (int i = 1; i < ts.Count; i++) ft[i - 1] = ts[i] - ts[i - 1];
        return ft;
    }

    /// <summary>FPS "low" do percentil: 1% low = FPS equivalente ao frametime do percentil 99 (0,1% low = 99,9).</summary>
    public static double LowFps(double[] sortedFrameTimes, double percentile)
    {
        if (sortedFrameTimes.Length == 0) return 0;
        double ft = sortedFrameTimes[(int)Math.Min(sortedFrameTimes.Length - 1, Math.Floor(sortedFrameTimes.Length * percentile))];
        return ft > 0 ? 1000 / ft : 0;
    }

    /// <summary>Variação do frametime (desvio padrão, ms): quanto maior, mais irregular (engasgos), mesmo com FPS alto.</summary>
    public static double Jitter(IReadOnlyList<double> ft)
    {
        if (ft.Count < 2) return 0;
        double avg = 0; foreach (var x in ft) avg += x; avg /= ft.Count;
        double v = 0; foreach (var x in ft) v += (x - avg) * (x - avg);
        return Math.Sqrt(v / ft.Count);
    }

    /// <summary>FPS de cada segundo inteiro da gravação (o último pedaço, se for menor que meio segundo, fica de fora).</summary>
    public static List<double> PerSecondFps(IReadOnlyList<double> ts)
    {
        var list = new List<double>();
        if (ts.Count < 2) return list;
        double start = ts[0], end = ts[^1];
        int i = 0;
        for (double w = start; w < end; w += 1000)
        {
            double we = Math.Min(w + 1000, end);
            if (we - w < 500) break;
            int n = 0;
            while (i < ts.Count && ts[i] <= we) { if (ts[i] > w) n++; i++; } // frames que terminam em (w, we]
            list.Add(n * 1000 / (we - w));
        }
        return list;
    }

    /// <summary>Resumo do benchmark; null se tiver menos de 10 frames ou durar menos de 1 s.</summary>
    public static BenchSummary? Summarize(string game, DateTime start, IReadOnlyList<double> ts)
    {
        if (ts.Count < 10) return null;
        double ms = ts[^1] - ts[0];
        if (ms < 1000) return null;
        var ft = FrameTimes(ts);
        var sorted = (double[])ft.Clone();
        Array.Sort(sorted);
        var perSec = PerSecondFps(ts);
        double avg = ft.Length * 1000 / ms;
        return new BenchSummary(game, start, ms / 1000, ft.Length, avg,
            perSec.Count > 0 ? perSec.Min() : avg, perSec.Count > 0 ? perSec.Max() : avg,
            LowFps(sorted, 0.99), LowFps(sorted, 0.999));
    }

    /// <summary>CSV por frame no formato do PresentMon/CapFrameX (abre no CapFrameX, Excel e afins).</summary>
    public static string Csv(string game, IReadOnlyList<double> ts)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("Application,Frame,TimeInSeconds,MsBetweenPresents\r\n");
        for (int i = 1; i < ts.Count; i++)
            sb.Append(game).Append(',').Append(i).Append(',')
              .Append(((ts[i] - ts[0]) / 1000).ToString("0.000000", inv)).Append(',')
              .Append((ts[i] - ts[i - 1]).ToString("0.000", inv)).Append("\r\n");
        return sb.ToString();
    }
}

/// <summary>Mínimo, média e máximo do FPS desde que o jogo apareceu na tela (zera ao trocar de jogo), como no RivaTuner.</summary>
public sealed class LiveFps
{
    public int Pid { get; private set; }
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Avg => _n > 0 ? _sum / _n : 0;
    private double _sum;
    private int _n;

    public void Add(int pid, double fps)
    {
        if (pid != Pid) { Reset(); Pid = pid; }
        if (fps <= 0) return;
        if (_n == 0 || fps < Min) Min = fps;
        if (fps > Max) Max = fps;
        _sum += fps; _n++;
    }

    public void Reset() { _sum = 0; _n = 0; Min = Max = 0; }
}
