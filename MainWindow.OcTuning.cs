using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>Aba Overclock no estilo do MSI Afterburner: sliders, curva V/F com undervolt, curva de fans, perfis e monitoramento.</summary>
public partial class MainWindow
{
    private GpuOc? _oc;
    private OcProfile P => _settings.OcCurrent;
    private readonly DispatcherTimer _ocTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<OcSample> _ocSamples = new();
    private IReadOnlyList<(int Mv, int Mhz)>? _vfStock;
    private readonly Dictionary<string, (Slider Slider, TextBox Box)> _ocSliders = new();
    private readonly Dictionary<string, TextBlock> _ocGauges = new();
    private readonly List<(Canvas Canvas, TextBlock Value, Func<OcSample, double?> Get, Func<OcSample, double?>? Limit, string Unit, string Color)> _ocCharts = new();
    private CheckBox? _ocFanAuto;
    private bool _ocSyncing, _ocSaving, _ocFansTouched;
    private int _ocFanCurrent = -1;
    private int? _ocDrag;

    // ---------------- Montagem ----------------

    private void InitOc()
    {
        nint h = OcGpu;
        OcGpuTitle.Text = _gpus.Count > 0 ? "GPU · " + GameCatalog.ShortGpuName(_gpus[0].Name).ToUpperInvariant() : "GPU";
        if (h != IntPtr.Zero)
        {
            uint pci = Nvml.Devices().FirstOrDefault(d => d.Handle == h).Pci.PciDeviceId;
            _oc = GpuOc.Open(h, pci);
        }

        BuildOcGauges();
        BuildOcCharts();
        BuildOcProfiles();
        if (_oc == null)
        {
            OcGpuState.Text = "O overclock está disponível só para placas NVIDIA (driver oficial).";
            BtnOcApply.IsEnabled = BtnOcReset.IsEnabled = BtnOcSave.IsEnabled = false;
        }
        else
        {
            SyncOffsetsFromGpu();
            BuildOcSliders();
            OcApplyOnStart.IsChecked = _settings.OcApplyOnStart;
            OcFanCurveOn.IsChecked = P.FanCurveOn;
            OcFanCurveOn.IsEnabled = _oc.Fans > 0;
            OcFanHyst.Value = P.FanHysteresisC;
            OcGpuState.Text = OcStateText();
            if (_settings.OcApplyOnStart && _settings.OcApplied is { } applied && IsAdmin())
            {
                _settings.OcCurrent = applied.Clone();
                SyncOcSliders();
                BtnOcApply_Click(this, new RoutedEventArgs()); // fora da thread da janela; relê a curva no fim
            }
            else ReadVfCurve();
            _ocTimer.Tick += (_, _) => OcTick();
            _ocTimer.Start();
            Closed += (_, _) => { _ocTimer.Stop(); if (_ocFansTouched) _oc?.FansAuto(); };
        }

        HookVfCanvas(OcVfCanvas);
        // Ctrl+F na aba Overclock abre o editor de curva em janela própria, como no Afterburner.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && PageOc.Visibility == Visibility.Visible) { ToggleVfWindow(); e.Handled = true; }
        };
        Closed += (_, _) => _vfWindow?.Close();
        OcFanCanvas.SizeChanged += (_, _) => DrawFan();
        OcFanCanvas.MouseLeftButtonDown += OcFan_Down;
        OcFanCanvas.MouseMove += OcFan_Move;
        OcFanCanvas.MouseLeftButtonUp += (_, _) => { _ocDrag = null; OcFanCanvas.ReleaseMouseCapture(); _settings.Save(); };
        OcFanCanvas.MouseRightButtonDown += OcFan_Right;

        BuildOcGuide();
        UpdateOcHistory();
        UpdateRecentErrors();
    }

    private static bool IsAdmin() => new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
        .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    /// <summary>Chamado ao abrir a aba: atualiza o resumo e relê a curva.</summary>
    private void UpdateOcGpuState()
    {
        if (_oc == null) return;
        OcGpuState.Text = OcStateText();
        if (_vfStock == null) ReadVfCurve();
    }

    private string OcStateText()
    {
        if (_oc == null) return "";
        var parts = new List<string>();
        if (_oc.Power is { } pw) parts.Add($"energia padrão {pw.DefaultW} W (faixa {pw.MinW}–{pw.MaxW} W)");
        parts.Add(_oc.Fans > 0 ? $"{_oc.Fans} fan(s) controláveis" : "fans sem controle pelo driver");
        parts.Add(_oc.CurveSupported ? "curva V/F disponível" : "curva V/F indisponível");
        return string.Join(" · ", parts) + (_oc.Problem is { } p ? ". " + p : ".");
    }

    private void SyncOffsetsFromGpu()
    {
        if (_oc == null) return;
        var a = _oc.ReadApplied();
        P.CoreOffsetMhz = a.CoreMhz; P.MemOffsetMhz = a.MemMhz; P.VoltagePercent = a.VoltagePercent;
        P.UvCapMv = a.CapMv; P.UvCapMhz = a.CapMhz;
        if (_oc.Power is { } pw && Nvml.PowerLimitMw(_oc.NvmlGpu) is uint lim)
            P.PowerPercent = (int)Math.Round(lim / 10.0 / pw.DefaultW);
        if (_oc.TempLimit is { } tl) P.TempLimitC ??= tl.Current;
    }

    private void BuildOcGauges()
    {
        OcGauges.Children.Clear();
        foreach (var (key, label) in new[] { ("core", "GPU CLOCK"), ("mem", "MEM CLOCK"), ("mv", "VOLTAGEM"), ("temp", "TEMPERATURA"), ("power", "CONSUMO"), ("fan", "FAN") })
        {
            var value = new TextBlock { Text = "—", FontSize = 24, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)FindResource("Mono") };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Label") });
            stack.Children.Add(value);
            OcGauges.Children.Add(new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 10, 0), Child = stack });
            _ocGauges[key] = value;
        }
    }

    private void BuildOcSliders()
    {
        if (_oc == null) return;
        OcSliders.Children.Clear();
        _ocSliders.Clear();
        var pw = _oc.Power;
        var tl = _oc.TempLimit;
        bool nvapi = _oc.NvApiGpu != IntPtr.Zero;
        // Blocos do Afterburner: Clock (Mem | Core), Voltage (Core + Curve Editor) e Fan (Fan speed | Temp Limit + Power Limit).
        var mem = Cell("mem", "Mem (MHz)", _oc.MemRangeMhz.Min, _oc.MemRangeMhz.Max, nvapi, v => P.MemOffsetMhz = v, () => P.MemOffsetMhz, signed: true);
        var core = Cell("core", "Core (MHz)", _oc.CoreRangeMhz.Min, _oc.CoreRangeMhz.Max, nvapi, v => { P.CoreOffsetMhz = v; DrawVf(); }, () => P.CoreOffsetMhz, signed: true);
        Section("Clock", mem, core);

        var volt = new StackPanel();
        volt.Children.Add(Cell("volt", "Core (%)", 0, 100, nvapi, v => P.VoltagePercent = v, () => P.VoltagePercent, signed: false));
        var curveBtn = new Button { Content = "Curve Editor", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(16, 7, 16, 7), HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0), ToolTip = "Editor da curva tensão/frequência (Ctrl+F)", IsEnabled = _oc.CurveSupported };
        curveBtn.Click += BtnOcCurveEditor_Click;
        volt.Children.Add(curveBtn);
        Section("Voltage", null, volt);

        var fan = new StackPanel();
        fan.Children.Add(Cell("fan", "Fan speed (%)", (int)_oc.FanRange.Min, (int)_oc.FanRange.Max, _oc.Fans > 0, v => P.FanFixedPercent = v, () => P.FanFixedPercent, signed: false));
        _ocFanAuto = new CheckBox { Content = "Auto", IsChecked = P.FanAuto, IsEnabled = _oc.Fans > 0, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) };
        _ocFanAuto.Checked += (_, _) => { P.FanAuto = true; UpdateFanSliderState(); };
        _ocFanAuto.Unchecked += (_, _) => { P.FanAuto = false; UpdateFanSliderState(); };
        fan.Children.Add(_ocFanAuto);
        var limits = new StackPanel();
        limits.Children.Add(Cell("temp", "Temp Limit (°C)", tl?.Min ?? 0, tl?.Max ?? 100, tl != null, v => P.TempLimitC = v, () => P.TempLimitC ?? tl?.Current ?? 83, signed: false));
        limits.Children.Add(Cell("power", "Power Limit (%)", pw is { } a ? (int)Math.Ceiling(a.MinW * 100.0 / a.DefaultW) : 100, pw is { } b ? (int)(b.MaxW * 100.0 / b.DefaultW) : 100,
            pw != null, v => P.PowerPercent = v, () => P.PowerPercent, signed: false));
        Section("Fan", fan, limits, last: true);

        UpdateFanSliderState();
        if (pw != null) _ocSliders["power"].Slider.ValueChanged += (_, _) => UpdatePowerHint();
        UpdatePowerHint();

        void Section(string title, UIElement? left, UIElement right, bool last = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, last ? 0 : 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) });
            if (left != null) { Grid.SetColumn(left, 1); grid.Children.Add(left); }
            Grid.SetColumn(right, 3); grid.Children.Add(right);
            OcSliders.Children.Add(new Border
            {
                Child = grid, Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(8),
                Background = (Brush)FindResource("Bg"), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8),
            });
        }

        // "Mem (MHz)  +0  ──●──": nome, valor (dá para digitar) e slider, como uma linha do Afterburner.
        Grid Cell(string key, string label, int min, int max, bool enabled, Action<int> set, Func<int> get, bool signed)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2), IsEnabled = enabled, Opacity = enabled ? 1 : 0.4 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var name = new TextBlock { Text = label, Foreground = (Brush)FindResource("Muted"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var slider = new Slider { Minimum = min, Maximum = Math.Max(min, max), TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Value = Math.Clamp(get(), min, Math.Max(min, max)) };
            var box = new TextBox { FontFamily = (FontFamily)FindResource("Mono"), FontSize = 12, Padding = new Thickness(4, 3, 4, 3), HorizontalContentAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            string Format(double v) => signed ? $"{v:+0;-0;0}" : $"{v:0}";
            box.Text = Format(slider.Value);
            slider.ValueChanged += (_, e) => { box.Text = Format(e.NewValue); if (!_ocSyncing) set((int)e.NewValue); };
            void Commit() { if (int.TryParse(box.Text.Replace("+", ""), out int v)) slider.Value = Math.Clamp(v, slider.Minimum, slider.Maximum); box.Text = Format(slider.Value); }
            box.LostFocus += (_, _) => Commit();
            // Clicar na caixa seleciona o valor todo, para digitar o novo por cima.
            box.GotKeyboardFocus += (_, _) => box.Dispatcher.BeginInvoke(box.SelectAll, DispatcherPriority.Input);
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
            slider.MouseRightButtonUp += (_, _) => slider.Value = Math.Clamp(signed ? 0 : slider.Value, slider.Minimum, slider.Maximum);
            Grid.SetColumn(box, 1); Grid.SetColumn(slider, 2);
            grid.Children.Add(name); grid.Children.Add(box); grid.Children.Add(slider);
            _ocSliders[key] = (slider, box);
            return grid;
        }
    }

    private void UpdatePowerHint()
    {
        if (_oc?.Power is not { } pw || !_ocSliders.TryGetValue("power", out var s)) return;
        s.Slider.ToolTip = $"{pw.DefaultW * s.Slider.Value / 100:0} W";
    }

    private void UpdateFanSliderState()
    {
        if (!_ocSliders.TryGetValue("fan", out var s)) return;
        bool manual = !P.FanAuto && !P.FanCurveOn;
        s.Slider.IsEnabled = s.Box.IsEnabled = manual;
        s.Slider.Opacity = manual ? 1 : 0.45;
        if (_ocFanAuto != null) _ocFanAuto.IsEnabled = _oc?.Fans > 0 && !P.FanCurveOn;
    }

    private void SyncOcSliders()
    {
        _ocSyncing = true;
        void Set(string key, int v) { if (_ocSliders.TryGetValue(key, out var s)) s.Slider.Value = Math.Clamp(v, s.Slider.Minimum, s.Slider.Maximum); }
        Set("volt", P.VoltagePercent); Set("power", P.PowerPercent); Set("temp", P.TempLimitC ?? 0);
        Set("core", P.CoreOffsetMhz); Set("mem", P.MemOffsetMhz); Set("fan", P.FanFixedPercent);
        _ocSyncing = false;
        if (_ocFanAuto != null) _ocFanAuto.IsChecked = P.FanAuto;
        OcFanCurveOn.IsChecked = P.FanCurveOn;
        OcFanHyst.Value = P.FanHysteresisC;
        OcUvMhz.Text = P.UvCapMhz?.ToString() ?? "";
        UpdateFanSliderState();
        DrawVf(); DrawFan();
    }

    // ---------------- Aplicar, reset e perfis ----------------

    /// <summary>Roda uma operação na placa fora da thread da janela (a leitura da curva pode esperar até 3 s),
    /// com os botões desligados enquanto isso.</summary>
    private async Task<T> OcRun<T>(Func<GpuOc, T> work)
    {
        var oc = _oc!;
        BtnOcApply.IsEnabled = BtnOcReset.IsEnabled = false;
        try { return await Task.Run(() => { lock (oc.Gate) return work(oc); }); }
        finally { BtnOcApply.IsEnabled = BtnOcReset.IsEnabled = true; }
    }

    private async void BtnOcApply_Click(object sender, RoutedEventArgs e)
    {
        if (_oc == null || !RequireAdmin()) return;
        var profile = P.Clone();
        OcGpuResult.Text = "Aplicando…";
        var report = await OcRun(oc => oc.Apply(profile));
        report.Add(ApplyFanMode());
        _settings.OcApplied = profile;
        _settings.Save();
        OcGpuResult.Text = string.Join(" ", report) + " Rode o verificador de estabilidade para confirmar.";
        ReadVfCurve();
    }

    private async void BtnOcReset_Click(object sender, RoutedEventArgs e)
    {
        if (_oc == null || !RequireAdmin()) return;
        OcGpuResult.Text = "Voltando ao padrão…";
        var report = await OcRun(oc => oc.Reset());
        _ocFansTouched = false; _ocFanCurrent = -1;
        _settings.OcCurrent = new OcProfile { FanCurve = P.FanCurve, FanHysteresisC = P.FanHysteresisC };
        _settings.OcApplied = null;
        SyncOffsetsFromGpu();
        _settings.Save();
        SyncOcSliders();
        ReadVfCurve();
        OcGpuResult.Text = "Padrão de fábrica: " + string.Join(" ", report);
    }

    /// <summary>Fans: curva (controlada no timer), fixo (manual) ou automático da placa.</summary>
    private string ApplyFanMode()
    {
        if (_oc == null || _oc.Fans == 0) return "";
        if (P.FanCurveOn) { _ocFanCurrent = -1; _ocFansTouched = true; return "Fans: curva ativa."; }
        if (!P.FanAuto)
        {
            _ocFansTouched = true;
            return Nvml.Explain(_oc.SetFans((uint)P.FanFixedPercent)) is { } err ? $"Fans: não aplicado ({err})." : $"Fans: {P.FanFixedPercent}% fixo.";
        }
        if (_ocFansTouched) { _oc.FansAuto(); _ocFansTouched = false; }
        return "Fans: automático.";
    }

    private void BuildOcProfiles()
    {
        OcProfilesPanel.Children.Clear();
        for (int i = 0; i < 5; i++)
        {
            int slot = i;
            bool used = _settings.OcProfiles.Length > slot && _settings.OcProfiles[slot] != null;
            var b = new Button
            {
                Content = (slot + 1).ToString(), Width = 38, Padding = new Thickness(0, 9, 0, 9), Margin = new Thickness(0, 0, 6, 0),
                Style = (Style)FindResource(used ? "PrimaryButton" : "OutlineButton"), IsEnabled = _oc != null,
                ToolTip = used ? $"Perfil {slot + 1}: clique para carregar e aplicar" : $"Perfil {slot + 1}: vazio",
            };
            b.Click += (_, _) => OcProfileClick(slot);
            OcProfilesPanel.Children.Add(b);
        }
    }

    private void BtnOcSave_Click(object sender, RoutedEventArgs e)
    {
        _ocSaving = !_ocSaving;
        BtnOcSave.Content = _ocSaving ? "ESCOLHA 1–5…" : "SALVAR";
    }

    private void OcProfileClick(int slot)
    {
        if (_settings.OcProfiles.Length < 5) { var grown = new OcProfile?[5]; _settings.OcProfiles.CopyTo(grown, 0); _settings.OcProfiles = grown; }
        if (_ocSaving)
        {
            _settings.OcProfiles[slot] = P.Clone();
            _settings.Save();
            _ocSaving = false; BtnOcSave.Content = "SALVAR";
            BuildOcProfiles();
            OcGpuResult.Text = $"Ajustes salvos no perfil {slot + 1}.";
            return;
        }
        if (_settings.OcProfiles[slot] is not { } saved) { OcGpuResult.Text = $"O perfil {slot + 1} está vazio. Use SALVAR e escolha o número."; return; }
        _settings.OcCurrent = saved.Clone();
        SyncOcSliders();
        BtnOcApply_Click(this, new RoutedEventArgs());
    }

    private void OcApplyOnStart_Changed(object sender, RoutedEventArgs e)
    {
        _settings.OcApplyOnStart = OcApplyOnStart.IsChecked == true;
        _settings.Save();
    }

    // ---------------- Monitoramento e controle dos fans (1 s) ----------------

    private void OcTick()
    {
        // Pausa durante os benchmarks de GPU (cada consulta ao driver aparece no 1% low) e, com a janela minimizada,
        // só continua se a curva de fans precisar da temperatura.
        if (_oc == null || _gpuBenchActive) return;
        if (WindowState == WindowState.Minimized && !P.FanCurveOn) return;
        OcSample s;
        if (!Monitor.TryEnter(_oc.Gate)) return; // APLICAR/REDEFINIR em andamento
        try { s = _oc.Sample(); } catch (Exception) { return; }
        finally { Monitor.Exit(_oc.Gate); }
        _ocSamples.Add(s);
        if (_ocSamples.Count > 120) _ocSamples.RemoveAt(0);

        if (P.FanCurveOn && _oc.Fans > 0 && s.TempC is uint t)
        {
            int next = _ocFanCurrent < 0 ? FanCurveMath.SpeedAt(P.FanCurve, t) : FanCurveMath.Next(P.FanCurve, t, _ocFanCurrent, P.FanHysteresisC);
            if (next != _ocFanCurrent && _oc.SetFans((uint)next) == 0) { _ocFanCurrent = next; _ocFansTouched = true; }
        }

        if (PageOc.Visibility != Visibility.Visible) return;
        _ocGauges["core"].Text = s.CoreMhz is uint c ? $"{c} MHz" : "—";
        _ocGauges["mem"].Text = s.MemMhz is uint m ? $"{m} MHz" : "—";
        _ocGauges["mv"].Text = s.CoreMv is int mv ? $"{mv} mV" : "—";
        _ocGauges["temp"].Text = s.TempC is uint tc ? $"{tc} °C" : "—";
        _ocGauges["temp"].Foreground = s.TempC is uint tb ? TempBrush(tb) : Fg;
        _ocGauges["power"].Text = s.PowerW is double w ? $"{w:0} W" : "—";
        _ocGauges["fan"].Text = s.FanPercent is uint f ? $"{f}%" : "—";
        OcLimiter.Text = "Limitado por: " + GpuOc.Limiter(s.Limiters) + (s.UsagePercent is uint u ? $" · uso {u}%" : "");
        DrawOcCharts();
        DrawFan();
        if (_vfStock != null) DrawVf();
    }

    private void BuildOcCharts()
    {
        OcCharts.Children.Clear();
        _ocCharts.Clear();
        Add("GPU CLOCK", s => s.CoreMhz, null, "MHz", "Accent");
        Add("MEMORY CLOCK", s => s.MemMhz, null, "MHz", "RingVram");
        Add("TEMPERATURA", s => s.TempC, null, "°C", "TempHot");
        Add("CONSUMO × LIMITE (CURVA DE POTÊNCIA)", s => s.PowerW, s => s.PowerLimitW, "W", "ChartGpu");
        Add("FAN", s => s.FanPercent, null, "%", "RingRam");
        Add("VOLTAGEM", s => s.CoreMv, null, "mV", "TempWarn");

        void Add(string title, Func<OcSample, double?> get, Func<OcSample, double?>? limit, string unit, string color)
        {
            var canvas = new Canvas { Height = 80, ClipToBounds = true, Margin = new Thickness(0, 6, 0, 0) };
            var value = new TextBlock { FontFamily = (FontFamily)FindResource("Mono"), HorizontalAlignment = HorizontalAlignment.Right, Foreground = (Brush)FindResource(color) };
            var head = new Grid();
            head.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Label") });
            head.Children.Add(value);
            var stack = new StackPanel();
            stack.Children.Add(head); stack.Children.Add(canvas);
            OcCharts.Children.Add(new Border { Child = stack, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(12), Background = (Brush)FindResource("Bg"), CornerRadius = new CornerRadius(10) });
            _ocCharts.Add((canvas, value, get, limit, unit, color));
        }
    }

    private void DrawOcCharts()
    {
        foreach (var (canvas, value, get, limit, unit, color) in _ocCharts)
        {
            canvas.Children.Clear();
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            var data = _ocSamples.Select(get).ToList();
            var lim = limit == null ? null : _ocSamples.Select(limit).ToList();
            var all = data.Concat(lim ?? new()).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            value.Text = data.LastOrDefault() is double last ? $"{last:0} {unit}" + (lim?.LastOrDefault() is double l ? $" / {l:0} {unit}" : "") : "—";
            if (w <= 0 || all.Count < 2) continue;
            double min = all.Min(), max = all.Max();
            if (max - min < 1) { min -= 1; max += 1; }
            double pad = (max - min) * 0.1; min -= pad; max += pad;
            Polyline Line(List<double?> vals, Brush brush, bool dashed)
            {
                var pl = new Polyline { Stroke = brush, StrokeThickness = dashed ? 1 : 1.8 };
                if (dashed) pl.StrokeDashArray = new DoubleCollection { 4, 3 };
                for (int i = 0; i < vals.Count; i++)
                    if (vals[i] is double v) pl.Points.Add(new Point(w - (vals.Count - 1 - i) * w / 119.0, h - (v - min) / (max - min) * h));
                return pl;
            }
            if (lim != null) canvas.Children.Add(Line(lim, (Brush)FindResource("TempCrit"), dashed: true));
            canvas.Children.Add(Line(data, (Brush)FindResource(color), dashed: false));
        }
    }

    // ---------------- Curva V/F ----------------

    private void BtnOcReadCurve_Click(object sender, RoutedEventArgs e) => ReadVfCurve();

    private bool _vfReading;

    /// <summary>Lê a curva fora da thread da janela (pode esperar até 3 s a placa sair de uma troca de estado).</summary>
    private async void ReadVfCurve()
    {
        // Uma leitura que falha não apaga a curva de fábrica já conhecida (ela não muda com o overclock).
        if (_oc?.CurveSupported == true && !_vfReading)
        {
            var oc = _oc;
            if (oc.StockCurve == null && _settings.OcStockCurve is { Count: > 0 } saved)
                oc.StockCurve = saved.Select(p => (p[0], p[1])).ToList();
            _vfReading = true;
            try { _vfStock = await Task.Run(() => { lock (oc.Gate) return oc.ReadCurve()?.Stock; }) ?? _vfStock; }
            catch (Exception ex) { OcVfInfo.Text = "Não foi possível ler a curva: " + ex.Message; }
            finally { _vfReading = false; }
            if (_oc.StockCurve is { } stock && _settings.OcStockCurve?.Count != stock.Count)
            {
                _settings.OcStockCurve = stock.Select(p => new[] { p.Mv, p.Mhz }).ToList();
                _settings.Save();
            }
        }
        DrawVf();
    }

    // Margens do gráfico: espaço para a escala de MHz à esquerda, o título no topo e a escala de mV + título embaixo.
    private const double VfLeft = 46, VfRight = 26, VfTop = 22, VfBottom = 36;

    /// <summary>Faixa visível como no Afterburner: começa onde a curva sai do piso (a parte plana de baixo fica de fora),
    /// grade de 25 mV × 100 MHz e folga em cima para o overclock.</summary>
    private (double X0, double X1, double Y0, double Y1) VfRange(int[] target)
    {
        var s = _vfStock!;
        int floor = s.Skip(1).Min(p => p.Mhz), first = 1;
        while (first < s.Count - 1 && s[first].Mhz <= floor + 50) first++;
        double x0 = Math.Floor((s[first].Mv - 30) / 25.0) * 25, x1 = Math.Ceiling(s[^1].Mv / 25.0) * 25;
        double low = Math.Min(s[first].Mhz, target.Skip(first).Min());
        // O Afterburner não desce a escala abaixo de 500 MHz: o que o offset empurra para baixo disso só sai do quadro.
        double y0 = Math.Max(s.Max(p => p.Mhz) > 1500 ? 500 : 0, Math.Floor(low / 100) * 100), y1 = Math.Ceiling((Math.Max(s.Max(p => p.Mhz), target.Max()) + 250) / 100.0) * 100;
        return (x0, Math.Max(x1, x0 + 100), y0, y1);
    }

    private static Point VfPoint(Canvas c, double mv, double mhz, (double X0, double X1, double Y0, double Y1) r)
    {
        double w = c.ActualWidth - VfLeft - VfRight, h = c.ActualHeight - VfTop - VfBottom;
        return new Point(VfLeft + (mv - r.X0) / (r.X1 - r.X0) * w, VfTop + h - (mhz - r.Y0) / (r.Y1 - r.Y0) * h);
    }

    private static int VfMhzAt(Canvas c, double y, (double X0, double X1, double Y0, double Y1) r)
    {
        double h = c.ActualHeight - VfTop - VfBottom;
        return (int)(Math.Round((r.Y0 + (VfTop + h - y) / h * (r.Y1 - r.Y0)) / 5) * 5);
    }

    /// <summary>Redesenha a curva na aba e, se estiver aberta, na janela do editor (Ctrl+F).</summary>
    private void DrawVf()
    {
        if (!IsLoaded || OcVfCanvas == null) return;
        DrawVfOn(OcVfCanvas);
        if (_vfWindow?.Content is Grid g && g.Children[0] is Canvas wc) DrawVfOn(wc);
    }

    private void DrawVfOn(Canvas canvas)
    {
        canvas.Children.Clear();
        if (_vfStock == null)
        {
            if (_oc?.CurveSupported == true && string.IsNullOrEmpty(OcVfInfo.Text))
                OcVfInfo.Text = "A curva não pôde ser lida agora (a placa estava trocando de estado de energia). Clique em RELER CURVA.";
            else if (_oc?.CurveSupported != true) OcVfInfo.Text = "Editor de curva indisponível nesta placa ou driver.";
            return;
        }
        if (canvas.ActualWidth < 200 || canvas.ActualHeight < 120) return;
        int[] target = VfCurveMath.TargetMhz(_vfStock, P);
        var r = VfRange(target);
        var grid = (Brush)FindResource("Line");
        var muted = (Brush)FindResource("Muted");
        var curve = (Brush)FindResource("Fg");
        Point Pt(double mv, double mhz) => VfPoint(canvas, mv, mhz, r);

        // Grade do Afterburner: uma linha a cada 25 mV e a cada 100 MHz; os números pulam marcas quando não cabem.
        double plotW = canvas.ActualWidth - VfLeft - VfRight, plotH = canvas.ActualHeight - VfTop - VfBottom;
        int xEvery = Math.Max(1, (int)Math.Ceiling(30 / (plotW / ((r.X1 - r.X0) / 25)))), yEvery = Math.Max(1, (int)Math.Ceiling(13 / (plotH / ((r.Y1 - r.Y0) / 100))));
        for (int k = 0, mv = (int)r.X0; mv <= r.X1; mv += 25, k++)
        {
            var a = Pt(mv, r.Y0); var b = Pt(mv, r.Y1);
            canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = grid, StrokeThickness = 1 });
            if (k % xEvery == 0) Label($"{mv}", a.X - 10, a.Y + 4);
        }
        for (int k = 0, mhz = (int)r.Y0; mhz <= r.Y1; mhz += 100, k++)
        {
            var a = Pt(r.X0, mhz); var b = Pt(r.X1, mhz);
            canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = grid, StrokeThickness = 1 });
            if (k % yEvery == 0) Label($"{mhz}", 4, a.Y - 7);
        }
        Label("Frequência, MHz", VfLeft - 30, 2);
        var xTitle = new TextBlock { Text = "Tensão, mV", FontSize = 10, Foreground = muted };
        xTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(xTitle, canvas.ActualWidth - VfRight - xTitle.DesiredSize.Width); Canvas.SetTop(xTitle, canvas.ActualHeight - 14);
        canvas.Children.Add(xTitle);

        // Fábrica: linha fina; a curva que vai para a placa: linha clara com um quadradinho por ponto, como no Afterburner.
        var stock = new Polyline { Stroke = (Brush)FindResource("LineStrong"), StrokeThickness = 1 };
        var edit = new Polyline { Stroke = curve, StrokeThickness = 1.5, Opacity = 0.8 };
        for (int i = 0; i < _vfStock.Count; i++)
        {
            stock.Points.Add(Pt(_vfStock[i].Mv, _vfStock[i].Mhz));
            edit.Points.Add(Pt(_vfStock[i].Mv, target[i]));
        }
        // Só dentro da área do gráfico: a parte plana de baixo e o que passa do topo não invadem as escalas.
        stock.Clip = edit.Clip = new RectangleGeometry(new Rect(VfLeft, VfTop, plotW, plotH));
        canvas.Children.Add(stock);
        canvas.Children.Add(edit);
        for (int i = 1; i < _vfStock.Count; i++)
        {
            if (_vfStock[i].Mv < r.X0 || target[i] < r.Y0) continue;
            var p = Pt(_vfStock[i].Mv, target[i]);
            bool edited = P.PointOffsetsMhz.ContainsKey(i), sel = _vfSel == i;
            double size = sel ? 8 : 5;
            var dot = new Rectangle { Width = size, Height = size, Fill = sel ? curve : edited ? (Brush)FindResource("TempWarn") : muted };
            Canvas.SetLeft(dot, p.X - size / 2); Canvas.SetTop(dot, p.Y - size / 2);
            canvas.Children.Add(dot);
        }
        if (P.UvCapMv is int cap)
        {
            int ci = VfCurveMath.NearestIndex(_vfStock, cap);
            var a = Pt(_vfStock[ci].Mv, r.Y0); var b = Pt(_vfStock[ci].Mv, r.Y1);
            canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = (Brush)FindResource("TempWarn"), StrokeDashArray = new DoubleCollection { 4, 3 } });
            var c = Pt(_vfStock[ci].Mv, target[ci]);
            var tag = new TextBlock { Text = $"{_vfStock[ci].Mv} mV · {target[ci]} MHz", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TempWarn") };
            Canvas.SetLeft(tag, Math.Max(VfLeft, c.X - 150)); Canvas.SetTop(tag, Math.Max(VfTop, c.Y - 26));
            canvas.Children.Add(tag);
        }
        if (_vfSel is int s && s < _vfStock.Count && s != CapIndex)
        {
            var c = Pt(_vfStock[s].Mv, target[s]);
            var tag = new TextBlock { Text = $"{_vfStock[s].Mv} mV · {target[s]} MHz", FontSize = 11, Foreground = curve };
            Canvas.SetLeft(tag, Math.Max(VfLeft, c.X - 120)); Canvas.SetTop(tag, Math.Max(VfTop, c.Y - 22));
            canvas.Children.Add(tag);
        }
        if (_ocSamples.LastOrDefault() is { CoreMv: int lmv, CoreMhz: uint lmhz } && lmv >= r.X0 && lmv <= r.X1)
        {
            var p = Pt(lmv, lmhz);
            var now = new Ellipse { Width = 10, Height = 10, Stroke = (Brush)FindResource("ChartGpu"), StrokeThickness = 2 };
            Canvas.SetLeft(now, p.X - 5); Canvas.SetTop(now, p.Y - 5);
            canvas.Children.Add(now);
        }

        int capIdx = CapIndex;
        string info = (capIdx >= 0 ? $"Undervolt: teto em {_vfStock[capIdx].Mv} mV a {target[capIdx]} MHz (fábrica nessa tensão: {_vfStock[capIdx].Mhz} MHz)." : "Sem teto de undervolt.")
            + $" Pico: {target.Max()} MHz (fábrica {_vfStock.Max(p => p.Mhz)} MHz). Core {P.CoreOffsetMhz:+0;-0;0} MHz"
            + (P.PointOffsetsMhz.Count > 0 ? $", {P.PointOffsetsMhz.Count} ponto(s) editado(s)." : ".") + " Círculo verde = ponto em uso agora. Clique em APLICAR para gravar.";
        OcVfInfo.Text = info;
        if (_vfWindowInfo != null) _vfWindowInfo.Text = info;

        void Label(string text, double x, double y)
        {
            var t = new TextBlock { Text = text, FontSize = 10, Foreground = muted };
            Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
            canvas.Children.Add(t);
        }
    }

    private int? NearestVfPoint(Canvas c, Point at)
    {
        if (_vfStock == null) return null;
        int[] target = VfCurveMath.TargetMhz(_vfStock, P);
        var r = VfRange(target);
        int? best = null; double bestD = 14;
        for (int i = 1; i < _vfStock.Count; i++)
        {
            if (_vfStock[i].Mv < r.X0) continue;
            var p = VfPoint(c, _vfStock[i].Mv, target[i], r);
            double d = Math.Abs(p.X - at.X) + Math.Abs(p.Y - at.Y) * 0.3;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private int? _vfSel;
    private double? _vfDownY;
    private Window? _vfWindow;
    private TextBlock? _vfWindowInfo;

    private int CapIndex => P.UvCapMv is int cap && _vfStock != null ? VfCurveMath.NearestIndex(_vfStock, cap) : -1;

    /// <summary>Liga mouse e teclado do editor a um canvas (o da aba e o da janela Ctrl+F usam os mesmos gestos).</summary>
    private void HookVfCanvas(Canvas c)
    {
        c.SizeChanged += (_, _) => DrawVf();
        c.MouseLeftButtonDown += OcVf_Down;
        c.MouseMove += OcVf_Move;
        c.MouseLeftButtonUp += (_, _) => { _ocDrag = null; c.ReleaseMouseCapture(); _settings.Save(); };
        c.MouseRightButtonDown += OcVf_Right;
        c.Focusable = true;
        c.KeyDown += OcVf_Key;
    }

    /// <summary>Ctrl+F: abre (ou fecha) o editor de curva numa janela própria, como o do Afterburner.</summary>
    private void ToggleVfWindow()
    {
        if (_vfWindow != null) { _vfWindow.Close(); return; }
        if (_oc?.CurveSupported != true) { OcVfInfo.Text = "Editor de curva indisponível nesta placa ou driver."; return; }
        var canvas = new Canvas { ClipToBounds = true, Background = Brushes.Transparent, Margin = new Thickness(8, 6, 8, 4) };
        _vfWindowInfo = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 12, 10) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(canvas);
        Grid.SetRow(_vfWindowInfo, 1);
        root.Children.Add(_vfWindowInfo);
        _vfWindow = new Window
        {
            Title = "Editor da Curva Tensão/Frequência", Owner = this, Width = 900, Height = 620, MinWidth = 480, MinHeight = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("Bg"), ShowInTaskbar = false, Content = root,
        };
        HookVfCanvas(canvas);
        _vfWindow.PreviewKeyDown += (_, e) => { if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _vfWindow?.Close(); e.Handled = true; } };
        _vfWindow.Closed += (_, _) => { _vfWindow = null; _vfWindowInfo = null; _settings.Save(); };
        _vfWindow.Show();
        canvas.Focus();
    }

    private void BtnOcCurveEditor_Click(object sender, RoutedEventArgs e) => ToggleVfWindow();

    private void OcVf_Down(object sender, MouseButtonEventArgs e)
    {
        var c = (Canvas)sender;
        _ocDrag = NearestVfPoint(c, e.GetPosition(c));
        _vfDownY = e.GetPosition(c).Y;
        if (_ocDrag != null) { _vfSel = _ocDrag; c.CaptureMouse(); }
        c.Focus();
        DrawVf();
    }

    private void OcVf_Move(object sender, MouseEventArgs e)
    {
        var c = (Canvas)sender;
        if (_ocDrag is not int i || _vfStock == null || e.LeftButton != MouseButtonState.Pressed) return;
        // Um clique só seleciona o ponto; ele muda quando o mouse anda de verdade (como no Afterburner).
        if (_vfDownY is double y0 && Math.Abs(e.GetPosition(c).Y - y0) < 3) return;
        _vfDownY = null;
        int mhz = VfMhzAt(c, e.GetPosition(c).Y, VfRange(VfCurveMath.TargetMhz(_vfStock, P)));
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            int extra = P.PointOffsetsMhz.TryGetValue(i, out int o) ? o : 0;
            int core = Math.Clamp(mhz - _vfStock[i].Mhz - extra, _oc?.CoreRangeMhz.Min ?? -1000, _oc?.CoreRangeMhz.Max ?? 1000);
            if (_ocSliders.TryGetValue("core", out var s)) s.Slider.Value = core; else P.CoreOffsetMhz = core;
            DrawVf();
        }
        else SetVfPointMhz(i, mhz);
    }

    /// <summary>Leva um ponto a um clock. No teto do undervolt (ou acima dele, na parte plana) muda o clock do teto,
    /// como arrastar o ponto travado no Afterburner; abaixo dele, muda só aquele ponto.</summary>
    private void SetVfPointMhz(int i, int mhz)
    {
        if (_vfStock == null) return;
        if (CapIndex >= 0 && i >= CapIndex)
        {
            int ci = CapIndex;
            P.UvCapMhz = Math.Clamp(mhz, _vfStock[ci].Mhz - VfCurveMath.MaxDeltaMhz, _vfStock[ci].Mhz + VfCurveMath.MaxDeltaMhz);
            OcUvMhz.Text = P.UvCapMhz.ToString();
        }
        else
        {
            int point = Math.Clamp(mhz - _vfStock[i].Mhz - P.CoreOffsetMhz, -VfCurveMath.MaxDeltaMhz, VfCurveMath.MaxDeltaMhz);
            if (point == 0) P.PointOffsetsMhz.Remove(i); else P.PointOffsetsMhz[i] = point;
        }
        DrawVf();
    }

    /// <summary>Trava (ou destrava) o teto de undervolt no ponto, no clock em que ele está agora — a tecla L do Afterburner.</summary>
    private void LockVfAt(int i)
    {
        if (_vfStock == null) return;
        if (CapIndex == i) { P.UvCapMv = null; P.UvCapMhz = null; OcUvMhz.Text = ""; }
        else
        {
            int mhz = int.TryParse(OcUvMhz.Text, out int typed) && P.UvCapMv == null ? typed : VfCurveMath.TargetMhz(_vfStock, P)[i];
            P.UvCapMv = _vfStock[i].Mv;
            P.UvCapMhz = mhz;
            OcUvMhz.Text = mhz.ToString();
        }
        _settings.Save();
        DrawVf();
    }

    private void OcVf_Right(object sender, MouseButtonEventArgs e)
    {
        var c = (Canvas)sender;
        if (NearestVfPoint(c, e.GetPosition(c)) is not int i) return;
        _vfSel = i;
        LockVfAt(i);
    }

    private void OcVf_Key(object sender, KeyEventArgs e)
    {
        // Ctrl+D volta a curva ao padrão de fábrica (sem offset, pontos editados nem teto), como no Afterburner.
        if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
        {
            P.PointOffsetsMhz.Clear(); P.UvCapMv = null; P.UvCapMhz = null; OcUvMhz.Text = "";
            if (_ocSliders.TryGetValue("core", out var core)) core.Slider.Value = 0; else P.CoreOffsetMhz = 0;
            _settings.Save(); DrawVf();
            e.Handled = true;
            return;
        }
        if (_vfSel is not int i || _vfStock == null) return;
        int[] target = VfCurveMath.TargetMhz(_vfStock, P);
        int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 25 : 5;
        switch (e.Key)
        {
            case Key.L: LockVfAt(i); break;
            case Key.Up: SetVfPointMhz(i, target[i] + step); break;
            case Key.Down: SetVfPointMhz(i, target[i] - step); break;
            case Key.Left when i > 1: _vfSel = i - 1; DrawVf(); break;
            case Key.Right when i < _vfStock.Count - 1: _vfSel = i + 1; DrawVf(); break;
            default: return;
        }
        _settings.Save();
        e.Handled = true;
    }

    private void BtnOcImport_Click(object sender, RoutedEventArgs e)
    {
        if (_oc == null) return;
        P.PointOffsetsMhz.Clear();
        SyncOffsetsFromGpu();
        ReadVfCurve();
        SyncOcSliders();
        _settings.Save();
        OcGpuResult.Text = $"Lido da placa: Core {P.CoreOffsetMhz:+0;-0;0} MHz, memória {P.MemOffsetMhz:+0;-0;0} MHz"
            + (P.UvCapMv is int cap ? $", teto em {cap} mV a {P.UvCapMhz} MHz." : ", sem teto de undervolt.")
            + " Vale também para o que foi aplicado no MSI Afterburner.";
    }

    private void OcUvMhz_Changed(object sender, RoutedEventArgs e)
    {
        P.UvCapMhz = int.TryParse(OcUvMhz.Text, out int mhz) && mhz is > 200 and < 4000 ? mhz : null;
        OcUvMhz.Text = P.UvCapMhz?.ToString() ?? "";
        _settings.Save();
        DrawVf();
    }

    private void OcUvMhz_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) OcUvMhz_Changed(sender, e); }

    private void BtnOcUvClear_Click(object sender, RoutedEventArgs e)
    {
        P.UvCapMv = null; P.UvCapMhz = null; OcUvMhz.Text = "";
        _settings.Save(); DrawVf();
    }

    private void BtnOcPointsClear_Click(object sender, RoutedEventArgs e)
    {
        P.PointOffsetsMhz.Clear();
        _settings.Save(); DrawVf();
    }

    // ---------------- Curva de fans ----------------

    private Point FanPt(double temp, double pct)
    {
        double w = OcFanCanvas.ActualWidth, h = OcFanCanvas.ActualHeight;
        return new Point(34 + (temp - 20) / 80 * (w - 42), (h - 20) - pct / 100 * (h - 28));
    }

    private (int Temp, int Pct) FanFromPoint(Point p)
    {
        double w = OcFanCanvas.ActualWidth, h = OcFanCanvas.ActualHeight;
        return ((int)Math.Round(Math.Clamp(20 + (p.X - 34) / (w - 42) * 80, 20, 100)),
                (int)Math.Round(Math.Clamp(((h - 20) - p.Y) / (h - 28) * 100, 0, 100)));
    }

    private void DrawFan()
    {
        if (!IsLoaded || OcFanCanvas.ActualWidth < 100) return;
        OcFanCanvas.Children.Clear();
        var faint = (Brush)FindResource("Line");
        var muted = (Brush)FindResource("Muted");
        for (int t = 20; t <= 100; t += 10)
        {
            var a = FanPt(t, 0); var b = FanPt(t, 100);
            OcFanCanvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = faint });
            Label($"{t}°", a.X - 8, a.Y + 3);
        }
        for (int pct = 0; pct <= 100; pct += 20)
        {
            var a = FanPt(20, pct); var b = FanPt(100, pct);
            OcFanCanvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = faint });
            Label($"{pct}%", 0, a.Y - 8);
        }
        var pts = P.FanCurve.OrderBy(p => p.TempC).ToList();
        var line = new Polyline { Stroke = (Brush)FindResource(P.FanCurveOn ? "Accent" : "LineStrong"), StrokeThickness = 2 };
        line.Points.Add(FanPt(20, pts[0].Percent));
        foreach (var p in pts) line.Points.Add(FanPt(p.TempC, p.Percent));
        line.Points.Add(FanPt(100, pts[^1].Percent));
        OcFanCanvas.Children.Add(line);
        foreach (var p in pts)
        {
            var c = FanPt(p.TempC, p.Percent);
            var dot = new Ellipse { Width = 10, Height = 10, Fill = (Brush)FindResource(P.FanCurveOn ? "Accent" : "Muted") };
            Canvas.SetLeft(dot, c.X - 5); Canvas.SetTop(dot, c.Y - 5);
            OcFanCanvas.Children.Add(dot);
        }
        var last = _ocSamples.LastOrDefault();
        if (last?.TempC is uint temp)
        {
            double tc = Math.Clamp(temp, 20, 100);
            var a = FanPt(tc, 0); var b = FanPt(tc, 100);
            OcFanCanvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = (Brush)FindResource("TempHot"), StrokeDashArray = new DoubleCollection { 4, 3 } });
            if (last.FanPercent is uint f)
            {
                var c = FanPt(tc, f);
                var now = new Ellipse { Width = 12, Height = 12, Stroke = (Brush)FindResource("ChartGpu"), StrokeThickness = 2 };
                Canvas.SetLeft(now, c.X - 6); Canvas.SetTop(now, c.Y - 6);
                OcFanCanvas.Children.Add(now);
            }
            OcFanInfo.Text = $"Agora: {temp} °C · fan {(last.FanPercent is uint fp ? fp + "%" : "—")}"
                + (P.FanCurveOn ? $" · a curva pede {FanCurveMath.SpeedAt(P.FanCurve, temp)}%" : " · curva desligada (fans no modo dos sliders)");
        }

        void Label(string text, double x, double y)
        {
            var t = new TextBlock { Text = text, FontSize = 10, Foreground = muted };
            Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
            OcFanCanvas.Children.Add(t);
        }
    }

    private int? NearestFanPoint(Point at)
    {
        for (int i = 0; i < P.FanCurve.Count; i++)
        {
            var c = FanPt(P.FanCurve[i].TempC, P.FanCurve[i].Percent);
            if (Math.Abs(c.X - at.X) < 10 && Math.Abs(c.Y - at.Y) < 10) return i;
        }
        return null;
    }

    private void OcFan_Down(object sender, MouseButtonEventArgs e)
    {
        var at = e.GetPosition(OcFanCanvas);
        if (e.ClickCount == 2 && NearestFanPoint(at) == null)
        {
            var (t, pct) = FanFromPoint(at);
            if (P.FanCurve.All(p => p.TempC != t)) P.FanCurve = P.FanCurve.Append(new FanPoint(t, pct)).OrderBy(p => p.TempC).ToList();
            _settings.Save(); DrawFan();
            return;
        }
        _ocDrag = NearestFanPoint(at);
        if (_ocDrag != null) OcFanCanvas.CaptureMouse();
    }

    private void OcFan_Move(object sender, MouseEventArgs e)
    {
        if (_ocDrag is not int i || e.LeftButton != MouseButtonState.Pressed) return;
        var (t, pct) = FanFromPoint(e.GetPosition(OcFanCanvas));
        var sorted = P.FanCurve;
        // Cada ponto fica entre os vizinhos, para a curva nunca "voltar".
        int lo = i > 0 ? sorted[i - 1].TempC + 1 : 20, hi = i < sorted.Count - 1 ? sorted[i + 1].TempC - 1 : 100;
        sorted[i] = new FanPoint(Math.Clamp(t, lo, Math.Max(lo, hi)), pct);
        _ocFanCurrent = -1;
        DrawFan();
    }

    private void OcFan_Right(object sender, MouseButtonEventArgs e)
    {
        if (NearestFanPoint(e.GetPosition(OcFanCanvas)) is int i && P.FanCurve.Count > 2)
        {
            P.FanCurve.RemoveAt(i);
            _settings.Save(); DrawFan();
        }
    }

    private void OcFanCurveOn_Changed(object sender, RoutedEventArgs e)
    {
        if (_ocSyncing || _oc == null) return;
        bool on = OcFanCurveOn.IsChecked == true;
        if (on == P.FanCurveOn) return;
        if (on && !RequireAdmin()) { OcFanCurveOn.IsChecked = false; return; }
        P.FanCurveOn = on;
        _ocFanCurrent = -1;
        OcGpuResult.Text = ApplyFanMode();
        _settings.Save();
        UpdateFanSliderState();
        DrawFan();
    }

    private void OcFanHyst_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OcFanHystValue == null) return;
        OcFanHystValue.Text = $"{e.NewValue:0} °C";
        if (_ocSyncing || _oc == null) return;
        P.FanHysteresisC = (int)e.NewValue;
        _settings.Save();
    }

    private void BtnOcFanDefault_Click(object sender, RoutedEventArgs e)
    {
        P.FanCurve = FanCurveMath.Default();
        _ocFanCurrent = -1;
        _settings.Save(); DrawFan();
    }
}
