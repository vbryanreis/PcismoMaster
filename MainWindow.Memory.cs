using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ResetExplode;

/// <summary>
/// TWEAKING · Limpeza, parte de cima: memória RAM em rosca (em uso, modificada, em espera, livre), o uso dos últimos 60 s
/// e o botão que limpa a memória e fecha os programas marcados (antes ficava na aba Ferramentas › Otimização).
/// </summary>
public partial class MainWindow
{
    public sealed record MemLegendItem(Brush Brush, string Label, string Value, string Hint);

    private readonly List<double> _memHistory = new(); // fração usada, 1 por segundo (só enquanto a página está aberta)
    private bool _memBusy;

    /// <summary>Ao abrir a Limpeza: lista os programas (fora da thread da tela) e desenha a memória.</summary>
    private void InitMemory()
    {
        RefreshApps();
        UpdateRamStatus();
    }

    /// <summary>Chamado a cada segundo pelo timer enquanto a Limpeza está na tela.</summary>
    private void UpdateRamStatus()
    {
        var (total, avail) = Optimizer.MemoryStatus();
        if (total == 0) return;
        var b = Optimizer.Breakdown();
        double used = total - avail;
        OptRamText.Text = $"{used / Gb:0.0} de {total / Gb:0.0} GB";

        _memHistory.Add(used / total);
        if (_memHistory.Count > 60) _memHistory.RemoveAt(0);

        Brush inUse = (Brush)FindResource("RingRam"), modified = (Brush)FindResource("TempWarn"),
              standby = (Brush)FindResource("AccentCyan"), free = (Brush)FindResource("Line");
        var parts = b == null
            ? new List<(double Bytes, Brush Brush)> { (used, inUse), (avail, free) }
            : new List<(double Bytes, Brush Brush)> { (b.InUse, inUse), (b.Modified, modified), (b.Standby, standby), (b.Free, free) };
        DrawMemGauge(parts, total, used / total);

        MemLegend.ItemsSource = b == null
            ? new List<MemLegendItem>
              {
                  new(inUse, "Em uso", $"{used / Gb:0.0} GB", "Memória ocupada pelos programas e pelo Windows."),
                  new(free, "Disponível", $"{avail / Gb:0.0} GB", "Livre para os programas usarem."),
              }
            : new List<MemLegendItem>
              {
                  new(inUse, "Em uso", $"{b.InUse / Gb:0.0} GB", "Memória ocupada pelos programas e pelo Windows."),
                  new(standby, "Em espera (cache)", $"{b.Standby / Gb:0.0} GB", "Arquivos guardados na RAM para abrir mais rápido. O Windows libera sozinho quando precisa; limpar ajuda quando a RAM está quase cheia."),
                  new(modified, "Modificada", $"{b.Modified / Gb:0.0} GB", "Dados que ainda vão ser gravados no disco antes de liberar."),
                  new(free, "Livre", $"{b.Free / Gb:0.0} GB", "Sem uso nenhum."),
              };
        DrawMemSpark();
    }

    private const double Gb = 1073741824.0;

    /// <summary>Rosca com uma fatia por tipo de memória e o % em uso no meio.</summary>
    private void DrawMemGauge(List<(double Bytes, Brush Brush)> parts, double total, double usedFrac)
    {
        MemGauge.Children.Clear();
        double size = MemGauge.Width, cx = size / 2, cy = size / 2, r = size / 2 - 10, thick = 14;
        MemGauge.Children.Add(new Ellipse { Width = r * 2, Height = r * 2, Stroke = (Brush)FindResource("Line"), StrokeThickness = thick,
                                            Margin = new Thickness(cx - r, cy - r, 0, 0) });
        double start = 0;
        foreach (var (bytes, brush) in parts.Take(parts.Count - 1)) // a última fatia (livre) é o próprio fundo da rosca
        {
            double frac = bytes / total;
            if (frac > 0.004)
                MemGauge.Children.Add(new Path { Data = ArcBetween(start, Math.Min(1, start + frac), cx, cy, r), Stroke = brush,
                                                 StrokeThickness = thick, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat });
            start += frac;
        }
        var fg = usedFrac >= 0.9 ? (Brush)FindResource("TempCrit") : usedFrac >= 0.8 ? (Brush)FindResource("TempWarn") : (Brush)FindResource("Fg");
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = $"{usedFrac:P0}", FontFamily = (FontFamily)FindResource("Mono"), FontSize = 30, FontWeight = FontWeights.SemiBold,
                                           Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = "em uso", Foreground = (Brush)FindResource("Muted"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(stack, cx - stack.DesiredSize.Width / 2);
        Canvas.SetTop(stack, cy - stack.DesiredSize.Height / 2);
        MemGauge.Children.Add(stack);
    }

    /// <summary>Arco do círculo entre duas frações (0 = topo, sentido horário).</summary>
    private static Geometry ArcBetween(double from, double to, double cx, double cy, double r)
    {
        to = Math.Min(to, from + 0.9999);
        Point At(double f) => new(cx + r * Math.Sin(f * 2 * Math.PI), cy - r * Math.Cos(f * 2 * Math.PI));
        var fig = new PathFigure { StartPoint = At(from) };
        fig.Segments.Add(new ArcSegment(At(to), new Size(r, r), 0, to - from > 0.5, SweepDirection.Clockwise, true));
        var g = new PathGeometry(new[] { fig });
        g.Freeze();
        return g;
    }

    /// <summary>Linha com área do uso de RAM nos últimos 60 s (escala de 0 a 100%).</summary>
    private void DrawMemSpark()
    {
        MemSpark.Children.Clear();
        double w = MemSpark.ActualWidth, h = MemSpark.ActualHeight;
        if (w < 10 || h < 10 || _memHistory.Count < 2) { MemSparkPeak.Text = ""; return; }
        double step = w / 59;
        double x0 = w - (_memHistory.Count - 1) * step;
        var line = new Polyline { Stroke = (Brush)FindResource("RingRam"), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
        var area = new Polygon { Fill = (Brush)FindResource("AccentSoft") };
        area.Points.Add(new Point(x0, h));
        for (int i = 0; i < _memHistory.Count; i++)
        {
            var p = new Point(x0 + i * step, h - _memHistory[i] * (h - 2));
            line.Points.Add(p);
            area.Points.Add(p);
        }
        area.Points.Add(new Point(w, h));
        MemSpark.Children.Add(area);
        MemSpark.Children.Add(line);
        MemSparkPeak.Text = $"pico {_memHistory.Max():P0}";
    }

    /// <summary>Lista os programas abertos fora da thread da janela (percorre todos os processos do Windows).</summary>
    private async void RefreshApps()
    {
        _apps = await Task.Run(Optimizer.ListApps);
        OptApps.ItemsSource = _apps;
    }

    private void BtnRefreshApps_Click(object sender, RoutedEventArgs e) => RefreshApps();

    private async void BtnOptimize_Click(object sender, RoutedEventArgs e)
    {
        if (_memBusy) return;
        var toClose = _apps.Where(a => a.Selected).ToList();
        string list = toClose.Count == 0 ? "nenhum programa marcado"
                                         : string.Join("\n", toClose.Select(a => $"  • {a.Title}"));
        var r = MessageBox.Show(this, "Limpar a memória vai:\n\n" +
                                "1. Esvaziar a memória em espera e enxugar a RAM dos programas.\n" +
                                $"2. Fechar os programas marcados:\n{list}\n\n" +
                                "Salve o que estiver aberto nesses programas antes. Continuar?",
                                "Limpar memória", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        _memBusy = true;
        BtnOptimize.IsEnabled = false;
        BtnOptimize.Content = "LIMPANDO…";
        OptResult.Visibility = Visibility.Collapsed;
        try
        {
            var closeResult = await Task.Run(() => Optimizer.CloseApps(toClose));
            var mem = await Task.Run(Optimizer.CleanMemory); // depois de fechar, para contar a memória liberada por eles também

            var parts = new List<string> { $"✔ {mem.FreedBytes / 1048576.0:N0} MB liberados" };
            parts.Add(mem.StandbyPurged ? "memória em espera esvaziada" : $"memória em espera não esvaziada ({mem.StandbyError})");
            if (closeResult.Closed.Count > 0) parts.Add($"fechados: {string.Join(", ", closeResult.Closed)}");
            if (closeResult.NotClosed.Count > 0)
                parts.Add($"não fecharam (podem ter algo não salvo ou pedir confirmação): {string.Join(", ", closeResult.NotClosed)}");
            OptResult.Text = string.Join(" · ", parts) + ".";
            OptResult.Foreground = (Brush)FindResource(mem.StandbyPurged ? "ChartGpu" : "TempWarn");
        }
        catch (Exception ex)
        {
            OptResult.Text = $"Falhou: {ex.Message}";
            OptResult.Foreground = (Brush)FindResource("TempCrit");
        }
        finally
        {
            OptResult.Visibility = Visibility.Visible;
            BtnOptimize.IsEnabled = true;
            BtnOptimize.Content = "LIMPAR MEMÓRIA";
            _memBusy = false;
        }
        RefreshApps();
        UpdateRamStatus();
    }

    /// <summary>
    /// A antiga aba Otimização guardava os valores originais em AppSettings.TweakBackup. Os ajustes dela agora estão no
    /// TWEAKING; quem alterou algo por lá ainda pode voltar ao original por este cartão (só aparece se houver o que desfazer).
    /// </summary>
    private void AddLegacyTweakRestore(Panel host)
    {
        if (_settings.TweakBackup.Count == 0) return;
        var box = new Border { BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Margin = new Thickness(0, 12, 0, 0) };
        var stack = new StackPanel();
        stack.Children.Add(OptText("A antiga aba Otimização alterou ajustes neste PC (plano de energia, Game DVR, mouse, jogos em janela ou HAGS). " +
                                   "Para voltar todos ao valor de antes:", "Muted", 12, 8));
        Button btn = null!;
        btn = TweakButton("RESTAURAR AJUSTES DA ANTIGA OTIMIZAÇÃO", false, (_, _) =>
        {
            if (MessageBox.Show(this, "Voltar os ajustes alterados pela antiga aba Otimização para como estavam?", "Windows",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            bool restart = _tweaks.Any(t => t.NeedsRestart && _settings.TweakBackup.Keys.Any(k => k == t.Id || k.StartsWith(t.Id + ".")));
            WindowsTweaks.RestoreAll(_tweaks, _settings);
            box.Visibility = Visibility.Collapsed;
            MessageBox.Show(this, "Ajustes restaurados." + (restart ? " Alguns só voltam depois de reiniciar o PC." : ""), "Windows");
            _ = RefreshOptStatesAsync();
        });
        stack.Children.Add(btn);
        box.Child = stack;
        host.Children.Add(box);
    }
}
