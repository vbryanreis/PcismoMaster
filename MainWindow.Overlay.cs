using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibreHardwareMonitor.Hardware;

namespace ResetExplode;

/// <summary>Overlay: o que mostrar (escolhido na aba Overlay) e a montagem das linhas de sensores.</summary>
public partial class MainWindow
{
    private HashSet<string>? _ovShow;
    private HashSet<string> OvShow => _ovShow ??= _settings.OverlayItems is { } l ? l.ToHashSet() : OverlayMetrics.Defaults();

    /// <summary>Cria as caixas de seleção da aba Overlay, agrupadas por componente.</summary>
    private void BuildOverlayPicker()
    {
        OvItemsPanel.Children.Clear();
        foreach (var group in OverlayMetrics.All.GroupBy(m => m.Group))
        {
            OvItemsPanel.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(), Style = (Style)FindResource("Label"), Margin = new Thickness(0, 10, 0, 4),
            });
            var wrap = new WrapPanel();
            foreach (var m in group)
            {
                var cb = new CheckBox { Content = m.Label, Tag = m.Key, IsChecked = OvShow.Contains(m.Key), Margin = new Thickness(0, 0, 18, 6) };
                cb.Click += (_, _) =>
                {
                    if (cb.IsChecked == true) OvShow.Add(m.Key); else OvShow.Remove(m.Key);
                    SaveOverlayItems();
                };
                wrap.Children.Add(cb);
            }
            OvItemsPanel.Children.Add(wrap);
        }
    }

    private void SaveOverlayItems()
    {
        _settings.OverlayItems = OverlayMetrics.All.Select(m => m.Key).Where(OvShow.Contains).ToList();
        _settings.Save();
        UpdateOverlay();
    }

    private void OvPreset_Click(object sender, RoutedEventArgs e)
    {
        var keys = (string)((FrameworkElement)sender).Tag switch
        {
            "all" => OverlayMetrics.All.Select(m => m.Key).ToHashSet(),
            "min" => new HashSet<string> { "fps", "frametime", "low1" },
            _ => OverlayMetrics.Defaults(),
        };
        _ovShow = keys;
        SaveOverlayItems();
        BuildOverlayPicker();
    }

    // ------------------------------------------------------------------ leitura dos sensores

    private SensorRow? GpuRow(SensorType type, string name, bool startsWith = false) =>
        _gpus.Count == 0 ? null : _monitor.Rows.FirstOrDefault(r => r.Hardware == _gpus[0].Name && r.Type == type &&
                                                                   (startsWith ? r.Name.StartsWith(name) : r.Name == name) && r.RawValue != null);

    private uint? MainGpuClock(int nvmlType, string lhmName)
    {
        if (_gpus.Count > 0 && _gpus[0].NvmlHandle != IntPtr.Zero && Nvml.Clock(_gpus[0].NvmlHandle, nvmlType) is uint c) return c;
        return GpuRow(SensorType.Clock, lhmName)?.RawValue is float v && v > 0 ? (uint)v : null;
    }

    /// <summary>VRAM usada e total em GB: NVML quando disponível, senão LibreHardwareMonitor (MB).</summary>
    private (double Used, double Total)? MainGpuVram()
    {
        if (_gpus.Count == 0) return null;
        if (_gpus[0].NvmlHandle != IntPtr.Zero && Nvml.MemoryInfo(_gpus[0].NvmlHandle) is Nvml.Memory m && m.Total > 0)
            return (m.Used / 1073741824.0, m.Total / 1073741824.0);
        if (GpuRow(SensorType.SmallData, "GPU Memory Used")?.RawValue is float u && GpuRow(SensorType.SmallData, "GPU Memory Total")?.RawValue is float t && t > 0)
            return (u / 1024.0, t / 1024.0);
        return null;
    }

    private (float? Avg, float? Max) CpuClocks()
    {
        var cores = _monitor.Rows.Where(r => r.HardwareType == HardwareType.Cpu && r.Type == SensorType.Clock &&
                                             r.Name.StartsWith("Core #") && !r.Name.Contains("Effective") && r.RawValue > 0)
                                 .Select(r => r.RawValue!.Value).ToList();
        return cores.Count > 0 ? (cores.Average(), cores.Max()) : (null, null);
    }

    /// <summary>Monta os valores escolhidos de GPU / VRAM / CPU / RAM; o overlay junta em linhas (vertical) ou células (barra).</summary>
    private List<OverlayPart> BuildOverlayParts(FrameStats? f)
    {
        var show = OvShow;
        var s = BottleneckAnalyzer.Sample(_monitor, _gpus.FirstOrDefault(), f);
        var parts = new List<OverlayPart>();
        string T(float? v) => v is float x ? $"{x:0} °C" : "—";
        string P(double? v) => v is double x ? $"{x:0}%" : "—";
        void Add(string key, string group, string shortLabel, string text, string? bare = null, float? temp = null)
        {
            if (show.Contains(key)) parts.Add(new OverlayPart(group, shortLabel, text, bare ?? text, temp));
        }

        Add("gpu_load", "GPU", "GPU", P(s.GpuLoad));
        Add("gpu_temp", "GPU", "GPU °C", T(s.GpuTemp), temp: s.GpuTemp);
        if (show.Contains("gpu_hotspot"))
        {
            var hs = GpuRow(SensorType.Temperature, "GPU Hot Spot")?.RawValue;
            Add("gpu_hotspot", "GPU", "HOTSPOT", "HS " + T(hs), T(hs), hs);
        }
        if (show.Contains("gpu_clock"))
        {
            var c = MainGpuClock(Nvml.ClockGraphics, "GPU Core");
            Add("gpu_clock", "GPU", "GPU MHz", c is uint a ? $"{a} MHz" : "— MHz", c?.ToString() ?? "—");
        }
        if (show.Contains("gpu_memclock"))
        {
            var mc = MainGpuClock(Nvml.ClockMem, "GPU Memory");
            Add("gpu_memclock", "GPU", "MEM MHz", mc is uint a ? $"mem {a} MHz" : "mem —", mc?.ToString() ?? "—");
        }
        if (show.Contains("gpu_power")) Add("gpu_power", "GPU", "GPU W", MainGpuPower() is float w ? $"{w:0} W" : "— W");
        if (show.Contains("gpu_volt")) Add("gpu_volt", "GPU", "GPU V", GpuRow(SensorType.Voltage, "GPU Core")?.RawValue is float v ? $"{v:0.000} V" : "— V");
        if (show.Contains("gpu_fan"))
        {
            string fan = GpuRow(SensorType.Fan, "GPU Fan", true)?.RawValue is float rpm && rpm > 0 ? $"{rpm:0} RPM"
                       : GpuRow(SensorType.Control, "GPU Fan", true)?.RawValue is float pct ? $"{pct:0}%" : "—";
            Add("gpu_fan", "GPU", "FAN", fan.EndsWith('%') || fan == "—" ? "fan " + fan : fan, fan);
        }
        if (show.Contains("gpu_perfcap") && MainGpuPerfCap() is { Length: > 0 } cap) Add("gpu_perfcap", "GPU", "LIMITE", cap);

        if (show.Contains("vram_used")) Add("vram_used", "VRAM", "VRAM", MainGpuVram() is { } a ? $"{a.Used:0.0} GB" : "—");
        Add("vram_pct", "VRAM", "VRAM %", P(s.VramPct));
        if (show.Contains("vram_temp"))
        {
            var vt = GpuRow(SensorType.Temperature, "GPU Memory")?.RawValue;
            Add("vram_temp", "VRAM", "VRAM °C", T(vt), temp: vt);
        }

        var (avg, max) = CpuClocks();
        Add("cpu_load", "CPU", "CPU", P(s.CpuTotal));
        Add("cpu_maxcore", "CPU", "NÚCLEO", s.CpuMaxCore is float mx ? $"núcleo {mx:0}%" : "núcleo —", s.CpuMaxCore is float m2 ? $"{m2:0}%" : "—");
        Add("cpu_temp", "CPU", "CPU °C", T(s.CpuTemp), temp: s.CpuTemp);
        Add("cpu_clock", "CPU", "CPU GHz", avg is float ca ? $"{ca / 1000:0.00} GHz" : "— GHz", avg is float c2 ? $"{c2 / 1000:0.00}" : "—");
        Add("cpu_clockmax", "CPU", "PICO GHz", max is float cm ? $"pico {cm / 1000:0.00} GHz" : "pico —", max is float c3 ? $"{c3 / 1000:0.00}" : "—");
        Add("cpu_power", "CPU", "CPU W", _monitor.CpuPower is float cw && cw > 0 ? $"{cw:0} W" : "— W"); // 0 = sem leitura (precisa de administrador)

        if (show.Contains("ram_used") || show.Contains("ram_pct"))
        {
            var (total, availB) = Optimizer.MemoryStatus();
            Add("ram_used", "RAM", "RAM", total > 0 ? $"{(total - availB) / 1073741824.0:0.0} GB" : "—");
            Add("ram_pct", "RAM", "RAM %", P(s.RamPct));
        }
        return parts;
    }

    // ------------------------------------------------------------------ prévia, timer e atalho

    /// <summary>O timer de 500 ms só roda com o overlay ligado ou com a aba Overlay aberta (prévia); fora disso, nada é lido.</summary>
    private void SyncOverlayTimer()
    {
        bool need = ChkOverlay.IsChecked == true || PageOverlay.IsVisible;
        if (need)
        {
            _overlayTimer ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(500),
                System.Windows.Threading.DispatcherPriority.Normal, (_, _) => UpdateOverlay(), Dispatcher);
            _overlayTimer.Start();
            UpdateOverlay();
        }
        else _overlayTimer?.Stop();
    }

    private void OvSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => OvOption_Changed(sender, e);

    private void PageOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => SyncOverlayTimer();

    /// <summary>Prévia na aba com os números reais deste PC, no formato e na opacidade escolhidos.</summary>
    private void UpdateOverlayPreview(FrameStats? f, IReadOnlyList<OverlayPart> parts, string? hour)
    {
        bool h = _settings.OverlayLayout == "H";
        OvPreview.ItemsSource = OverlayWindow.Cells(f, parts, OvShow, hour);
        OvPreview.MaxWidth = h ? double.PositiveInfinity : 230;
        OvPreviewBox.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb((byte)Math.Round(_settings.OverlayOpacity * 255), 0, 0, 0));
        OvPreviewBox.HorizontalAlignment = _settings.OverlayCorner.EndsWith("R") ? HorizontalAlignment.Right
                                         : _settings.OverlayCorner.EndsWith("C") ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        OvPreviewBox.VerticalAlignment = _settings.OverlayCorner.StartsWith("B") ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    private bool RegisterOverlayHotkey(uint mods, uint vk)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HotkeyId);
        return RegisterHotKey(hwnd, HotkeyId, mods | 0x4000, vk); // 0x4000 = sem repetição ao segurar
    }

    private void ShowOverlayHotkey()
    {
        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)_settings.OverlayHotkeyVk).ToString();
        OvHotkeyBox.Text = OverlayHotkey.Describe(_settings.OverlayHotkeyMods, key);
        OvHotkeyHint.Text = $"Atalho ativo: {OvHotkeyBox.Text}";
    }

    /// <summary>Clique na caixa e aperte a tecla (com ou sem Ctrl/Alt/Shift); Esc cancela.</summary>
    private void OvHotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt
                or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
                or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) return;
        if (key == System.Windows.Input.Key.Escape) { ShowOverlayHotkey(); Keyboard.ClearFocus(); return; }

        var m = System.Windows.Input.Keyboard.Modifiers;
        uint mods = (m.HasFlag(ModifierKeys.Control) ? OverlayHotkey.Ctrl : 0) | (m.HasFlag(ModifierKeys.Alt) ? OverlayHotkey.Alt : 0)
                  | (m.HasFlag(ModifierKeys.Shift) ? OverlayHotkey.Shift : 0);
        uint vk = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        if (RegisterOverlayHotkey(mods, vk))
        {
            _settings.OverlayHotkeyMods = mods;
            _settings.OverlayHotkeyVk = vk;
            _settings.Save();
            ShowOverlayHotkey();
        }
        else
        {
            RegisterOverlayHotkey(_settings.OverlayHotkeyMods, _settings.OverlayHotkeyVk); // volta ao anterior
            ShowOverlayHotkey();
            OvHotkeyHint.Text = $"{OverlayHotkey.Describe(mods, key.ToString())} já é usado por outro programa; escolha outra.";
        }
        Keyboard.ClearFocus();
    }
}
