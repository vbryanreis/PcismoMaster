using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Funções no estilo do RivaTuner na aba Overlay, sem injetar nada no jogo: limitador de FPS pelo driver NVIDIA,
/// benchmark com CSV por frame, captura de tela, gravação de vídeo (ffmpeg), atalhos, aparência e perfis por jogo.
/// </summary>
public partial class MainWindow
{
    private readonly LiveFps _ovLive = new();
    private string? _ovLastGame;                 // último jogo com FPS detectado (sugestão para o perfil)
    private OverlayGameProfile? _ovProfile;      // perfil do jogo que está na tela (null = configuração geral)
    private string? _ovProfileKey;
    private HashSet<string>? _ovProfileShow;
    private bool _ovAutoShown;                   // o overlay foi ligado sozinho pelo perfil do jogo
    private bool _ovBenchPending;                  // esperando um jogo aparecer na tela para começar
    private DateTime _ovBenchStart;
    private string _ovBenchGame = "";
    private readonly ScreenCapture.Recorder _rec = new();
    private bool _recBusy;
    private (string Text, DateTime Until)? _ovFlash;
    private IntPtr _fgHook;
    private WinEventProc? _fgProc;

    private static string BenchDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PcismoMaster", "Benchmarks");

    private IReadOnlySet<string> OvActiveShow => _ovProfileShow ?? (IReadOnlySet<string>)OvShow;
    private string OvActiveCorner => _ovProfile?.Corner ?? _settings.OverlayCorner;
    private string OvActiveLayout => _ovProfile?.Layout ?? _settings.OverlayLayout;

    /// <summary>Benchmark esperando/gravando ou vídeo gravando: o timer continua mesmo com o overlay desligado.</summary>
    private bool OvBackgroundWork => _ovBenchPending || _frames.Recording || _rec.Running;

    private void Flash(string text, int seconds = 4) => _ovFlash = (text, DateTime.Now.AddSeconds(seconds));

    // ------------------------------------------------------------------ a cada atualização do overlay

    /// <summary>Mín/méd/máx do jogo, começo e fim automático do benchmark e o aviso que aparece no overlay.</summary>
    private OverlayLive OverlayTick(FrameStats? f)
    {
        if (f != null) { _ovLive.Add(f.ProcessId, f.Fps); _ovLastGame = f.ProcessName; }
        if (_ovBenchPending && f != null) BeginBench(f);
        else if (_frames.Recording && _settings.BenchSeconds > 0 && (DateTime.Now - _ovBenchStart).TotalSeconds >= _settings.BenchSeconds) StopBench();

        var status = new List<string>();
        if (_ovBenchPending) status.Add("BENCHMARK: abra o jogo");
        else if (_frames.Recording)
            status.Add($"BENCHMARK {Elapsed(_ovBenchStart)}" + (_settings.BenchSeconds > 0 ? $" / {_settings.BenchSeconds} s" : ""));
        if (_rec.Running) status.Add($"REC {Elapsed(_rec.Started)}");
        if (_ovFlash is { } fl) { if (DateTime.Now < fl.Until) status.Add(fl.Text); else _ovFlash = null; }
        return new OverlayLive(_ovLive.Min, _ovLive.Avg, _ovLive.Max, status.Count > 0 ? string.Join("  ·  ", status) : null);
    }

    private static string Elapsed(DateTime from)
    {
        var t = DateTime.Now - from;
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    // ------------------------------------------------------------------ benchmark

    private void ToggleBench()
    {
        if (_frames.Recording) { StopBench(); return; }
        if (_ovBenchPending) { _ovBenchPending = false; Flash("Benchmark cancelado"); RefreshOverlayTools(); SyncOverlayTimer(); return; }
        _frames.Start();
        if (_frames.GetForegroundStats() is { } f) BeginBench(f);
        else _ovBenchPending = true;
        RefreshOverlayTools();
        SyncOverlayTimer();
    }

    private void BeginBench(FrameStats f)
    {
        _ovBenchPending = false;
        _ovBenchGame = f.ProcessName;
        _ovBenchStart = DateTime.Now;
        _frames.StartRecording(f.ProcessId);
        _ovLive.Reset();
        RefreshOverlayTools();
    }

    private async void StopBench()
    {
        var ts = _frames.StopRecording();
        RefreshOverlayTools();
        if (ts == null) return;
        if (_settings.BenchSeconds > 0 && ts.Count > 0) // corta no tempo exato escolhido
        {
            double end = ts[0] + _settings.BenchSeconds * 1000.0;
            int keep = ts.FindIndex(x => x > end);
            if (keep > 0) ts.RemoveRange(keep, ts.Count - keep);
        }
        var sum = FrameMath.Summarize(_ovBenchGame, _ovBenchStart, ts);
        if (sum == null) { Flash("Benchmark curto demais (menos de 1 s)"); return; }
        try
        {
            Directory.CreateDirectory(BenchDir);
            string file = Path.Combine(BenchDir, ScreenCapture.FileStem(_ovBenchGame) + ".csv");
            string game = _ovBenchGame;
            await Task.Run(() => File.WriteAllText(file, FrameMath.Csv(game, ts)));
            sum = sum with { CsvPath = file };
        }
        catch (Exception ex) { Flash("Não deu para salvar o CSV: " + ex.Message, 6); }
        var hist = _settings.BenchHistory ??= new List<BenchSummary>();
        hist.Insert(0, sum);
        if (hist.Count > 30) hist.RemoveRange(30, hist.Count - 30);
        _settings.Save();
        BuildBenchHistory();
        Flash($"{sum.Game}: méd {sum.Avg:0} · 1% {sum.Low1:0} · 0,1% {sum.Low01:0} FPS", 10);
        RefreshOverlayTools();
        SyncOverlayTimer();
    }

    // ------------------------------------------------------------------ limitador, captura e gravação

    private async void ToggleLimiter()
    {
        if (!OvNvidia) { Flash("Limitador de FPS: só em placas NVIDIA"); return; }
        try
        {
            var (now, _) = await Task.Run(FpsLimiter.ReadGlobal);
            if (now == 0 && _settings.LimiterLastFps == 0) { Flash("Escolha um limite na aba Overlay primeiro"); return; }
            uint target = now > 0 ? 0 : _settings.LimiterLastFps;
            if (now > 0) { _settings.LimiterLastFps = now; _settings.Save(); }
            await Task.Run(() => FpsLimiter.SetGlobal(target));
            Flash(target > 0 ? $"Limitador: {target} FPS" : "Limitador desligado");
            if (PageOverlay.IsVisible) _ = LoadLimiterAsync();
        }
        catch (Exception ex) { Flash("Limitador: " + ex.Message, 6); }
    }

    private string? ForegroundName()
    {
        int pid = FrameMonitor.ForegroundPid();
        return pid == 0 ? null : _frames.ProcessName(pid);
    }

    private async void TakeScreenshot()
    {
        try
        {
            string file = await ScreenCapture.ScreenshotAsync(ForegroundName());
            Flash("Captura salva");
            _ovLastShot = file;
            RefreshOverlayTools();
        }
        catch (Exception ex) { Flash("A captura falhou: " + ex.Message, 6); }
    }
    private string? _ovLastShot;

    private async void ToggleRecording()
    {
        if (_recBusy) return;
        _recBusy = true;
        try
        {
            if (_rec.Running)
            {
                string? file = await _rec.StopAsync();
                Flash("Vídeo salvo");
                _ovLastVideo = file;
            }
            else if (await _rec.StartAsync(ForegroundName(), _settings.RecordFps, OvNvidia) is { } why) { Flash(why, 8); _ovRecError = why; }
            else _ovRecError = null;
        }
        finally { _recBusy = false; }
        RefreshOverlayTools();
        SyncOverlayTimer();
    }
    private string? _ovLastVideo, _ovRecError;

    // Guardados: o PATH e a NVAPI não são consultados a cada atualização da aba (sem NVIDIA, cada consulta falhava com exceção).
    private bool? _ovNvidia, _ovFfmpeg;
    private bool OvNvidia => _ovNvidia ??= FpsLimiter.Available;
    private bool OvHasFfmpeg => _ovFfmpeg ??= ExternalTools.Has("ffmpeg");

    // ------------------------------------------------------------------ atalhos extras

    private void RegisterToolHotkeys()
    {
        for (int i = 0; i < OverlayActions.All.Length; i++)
            if (_settings.OverlayHotkeys?.GetValueOrDefault(OverlayActions.All[i].Key) is { Length: 2 } k && k[1] != 0) RegisterToolHotkey(i, k[0], k[1]);
    }

    private bool RegisterToolHotkey(int index, uint mods, uint vk)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HotkeyId + 1 + index);
        return vk == 0 || RegisterHotKey(hwnd, HotkeyId + 1 + index, mods | 0x4000, vk);
    }

    /// <summary>WM_HOTKEY de um atalho extra; false se o id não é dos atalhos extras.</summary>
    private bool OnToolHotkey(int id)
    {
        int i = id - HotkeyId - 1;
        if (i < 0 || i >= OverlayActions.All.Length) return false;
        switch (OverlayActions.All[i].Key)
        {
            case "bench": ToggleBench(); break;
            case "limit": ToggleLimiter(); break;
            case "shot": TakeScreenshot(); break;
            case "rec": ToggleRecording(); break;
        }
        SyncOverlayTimer();
        return true;
    }

    /// <summary>Lê a tecla apertada numa caixa de atalho; false enquanto só tem Ctrl/Alt/Shift.</summary>
    private static bool ReadHotkey(KeyEventArgs e, out uint mods, out uint vk, out Key key)
    {
        e.Handled = true;
        key = e.Key == Key.System ? e.SystemKey : e.Key;
        mods = vk = 0;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return false;
        var m = Keyboard.Modifiers;
        mods = (m.HasFlag(ModifierKeys.Control) ? OverlayHotkey.Ctrl : 0) | (m.HasFlag(ModifierKeys.Alt) ? OverlayHotkey.Alt : 0)
             | (m.HasFlag(ModifierKeys.Shift) ? OverlayHotkey.Shift : 0);
        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return true;
    }

    private static string HotkeyText(uint[]? k) =>
        k is { Length: 2 } && k[1] != 0 ? OverlayHotkey.Describe(k[0], KeyInterop.KeyFromVirtualKey((int)k[1]).ToString()) : "Nenhum";

    // ------------------------------------------------------------------ perfis por jogo

    /// <summary>Só escuta a troca de janela ativa quando existe algum perfil de jogo.</summary>
    private void SyncProfileWatcher()
    {
        bool need = _settings.OverlayGames is { Count: > 0 };
        if (need && _fgHook == IntPtr.Zero)
        {
            _fgProc ??= (_, _, hwnd, _, _, _, _) => OnForegroundChanged(hwnd);
            _fgHook = SetWinEventHook(3, 3, IntPtr.Zero, _fgProc, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND, fora do processo
            OnForegroundChanged(GetForegroundWindow());
        }
        else if (!need && _fgHook != IntPtr.Zero)
        {
            UnhookWinEvent(_fgHook);
            _fgHook = IntPtr.Zero;
            SetActiveProfile(null);
        }
        else if (need) OnForegroundChanged(GetForegroundWindow()); // perfis mudaram
    }

    private void OnForegroundChanged(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        string? key = null;
        if (pid != 0 && pid != Environment.ProcessId)
            try { using var p = Process.GetProcessById((int)pid); key = OverlayGameProfile.Key(p.ProcessName); } catch { }
        SetActiveProfile(key != null && _settings.OverlayGames?.ContainsKey(key) == true ? key : null);
    }

    private void SetActiveProfile(string? key, bool force = false)
    {
        if (key == _ovProfileKey && !force) return;
        _ovProfileKey = key;
        _ovProfile = key != null ? _settings.OverlayGames?.GetValueOrDefault(key) : null;
        _ovProfileShow = _ovProfile?.Items?.ToHashSet();
        if (_ovProfile?.AutoShow == true && ChkOverlay.IsChecked != true) { ChkOverlay.IsChecked = true; _ovAutoShown = true; }
        else if (_ovAutoShown && _ovProfile?.AutoShow != true) { _ovAutoShown = false; ChkOverlay.IsChecked = false; }
        ApplyOverlayOptions();
        if (ChkOverlay.IsChecked == true) UpdateOverlay();
    }

    private void SaveGameProfile(string name, OverlayGameProfile? existing)
    {
        string key = OverlayGameProfile.Key(name);
        if (key.Length == 0) return;
        var games = _settings.OverlayGames ??= new Dictionary<string, OverlayGameProfile>();
        var p = existing ?? games.GetValueOrDefault(key) ?? new OverlayGameProfile();
        p.Items = OverlayMetrics.All.Select(m => m.Key).Where(OvShow.Contains).ToList();
        p.Corner = _settings.OverlayCorner;
        p.Layout = _settings.OverlayLayout;
        games[key] = p;
        _settings.Save();
        SyncProfileWatcher();
        if (_ovProfileKey == key) SetActiveProfile(key, force: true);
        BuildGameProfiles();
    }

    private async void DeleteGameProfile(string key)
    {
        if (_settings.OverlayGames?.GetValueOrDefault(key) is not { } p) return;
        if (MessageBox.Show(this, $"Excluir o perfil de {key}?" + (p.FpsLimit > 0 ? "\n\nO limite de FPS deste jogo no driver volta para o global." : ""),
                "Perfis por jogo", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (p.FpsLimit > 0)
            try { await Task.Run(() => FpsLimiter.SetGame(key, 0)); }
            catch (Exception ex) { MessageBox.Show(this, "Não deu para tirar o limite do driver: " + ex.Message, "Perfis por jogo"); }
        _settings.OverlayGames.Remove(key);
        _settings.Save();
        if (_ovProfileKey == key) SetActiveProfile(null);
        SyncProfileWatcher();
        BuildGameProfiles();
    }

    // ------------------------------------------------------------------ tela

    private Button? _ovBenchBtn, _ovRecBtn;
    private TextBlock? _ovBenchInfo, _ovCaptureInfo, _ovLimiterInfo, _ovProfileHint;
    private StackPanel? _ovBenchHistory, _ovLimiterBody, _ovProfilesList, _ovAppearance;
    private TextBox? _ovProfileName;

    private StackPanel OvCard(string title, string intro)
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Label") });
        body.Children.Add(new TextBlock { Text = intro, Foreground = (Brush)FindResource("Faint"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) });
        OvToolsPanel.Children.Add(new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 14), Child = body });
        return body;
    }

    private TextBlock OvSmall(string text = "", string brush = "Muted") =>
        new() { Text = text, Foreground = (Brush)FindResource(brush), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    private static void OpenFolder(string dir, string? select = null)
    {
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", select != null && File.Exists(select) ? $"/select,\"{select}\"" : $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private WrapPanel OvChips(string group, IEnumerable<(string Label, string Tag)> items, string current, Action<string> pick)
    {
        var wrap = new WrapPanel();
        foreach (var (label, tag) in items)
        {
            var rb = new RadioButton { Style = (Style)FindResource("Chip"), GroupName = group, Content = label, Tag = tag, IsChecked = tag == current, Margin = new Thickness(0, 0, 6, 6) };
            rb.Checked += (_, _) => pick(tag);
            wrap.Children.Add(rb);
        }
        return wrap;
    }

    private ComboBox FpsCombo(uint current, string offLabel, Action<uint> pick)
    {
        var values = new List<uint> { 0 };
        values.AddRange(NvidiaPanel.FpsPresets);
        if (current > 0 && !values.Contains(current)) values.Add(current);
        values.Sort();
        var cb = new ComboBox { Width = 210, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var v in values) cb.Items.Add(new ComboBoxItem { Content = v == 0 ? offLabel : $"{v} FPS", Tag = v });
        cb.SelectedIndex = values.IndexOf(current);
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem { Tag: uint v }) pick(v); };
        return cb;
    }

    /// <summary>Monta os cartões novos da aba Overlay (uma vez, na abertura do app).</summary>
    private void BuildOverlayTools()
    {
        OvToolsPanel.Children.Clear();

        // Limitador
        _ovLimiterBody = OvCard("LIMITADOR DE FPS", "Pelo driver NVIDIA (a mesma \"Taxa Máxima de Quadros\" do Painel NVIDIA): nada é injetado no jogo. " +
            "Fica gravado no driver e vale mesmo com o PcismoMaster fechado. Com G-SYNC/FreeSync, 3 a 4 FPS abaixo da taxa do monitor dá a menor latência sem tearing. " +
            "Limite por jogo: em Perfis por jogo, abaixo.");
        _ovLimiterInfo = OvSmall(OvNvidia ? "Lendo o driver…" : "Precisa de placa e driver NVIDIA.");
        _ovLimiterBody.Children.Add(_ovLimiterInfo);

        // Benchmark
        var bench = OvCard("BENCHMARK", "Mede o jogo que está na tela e salva o resultado (FPS médio, mínimo e máximo de 1 s, 1% e 0,1% low) e um CSV " +
            "com cada frame, que abre no CapFrameX ou no Excel. Pelo botão daqui, a medição começa sozinha quando você voltar ao jogo; pelo atalho, na hora.");
        bench.Children.Add(new TextBlock { Text = "DURAÇÃO", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 0, 0, 6) });
        bench.Children.Add(OvChips("OvBenchDur", new[] { ("Até parar", "0"), ("30 s", "30"), ("60 s", "60"), ("2 min", "120"), ("5 min", "300") },
            _settings.BenchSeconds.ToString(), t => { _settings.BenchSeconds = int.Parse(t); _settings.Save(); }));
        var benchButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        _ovBenchBtn = TweakButton("Começar benchmark", true, (_, _) => ToggleBench());
        benchButtons.Children.Add(_ovBenchBtn);
        benchButtons.Children.Add(TweakButton("Abrir pasta", false, (_, _) => OpenFolder(BenchDir)));
        bench.Children.Add(benchButtons);
        _ovBenchInfo = OvSmall();
        bench.Children.Add(_ovBenchInfo);
        bench.Children.Add(new TextBlock { Text = "RESULTADOS", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 14, 0, 6) });
        _ovBenchHistory = new StackPanel();
        bench.Children.Add(_ovBenchHistory);

        // Capturas
        var cap = OvCard("CAPTURA DE TELA E VÍDEO", "Do monitor onde o jogo está. Fotos em PNG na pasta Imagens\\PcismoMaster; vídeos em MP4 na pasta Vídeos\\PcismoMaster, " +
            "gravados pelo ffmpeg (instale em Instalar apps) com a placa de vídeo (NVENC) quando é NVIDIA. O vídeo não grava o som. " +
            "Jogos em tela cheia exclusiva antiga podem sair pretos.");
        var inCap = new CheckBox { Content = "Mostrar o overlay nas capturas, gravações e transmissões", IsChecked = _settings.OverlayInCaptures, Margin = new Thickness(0, 0, 0, 10) };
        inCap.Click += (_, _) => { _settings.OverlayInCaptures = inCap.IsChecked == true; _settings.Save(); _overlay?.SetCaptureVisible(_settings.OverlayInCaptures); };
        cap.Children.Add(inCap);
        cap.Children.Add(new TextBlock { Text = "FPS DO VÍDEO", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 0, 0, 6) });
        cap.Children.Add(OvChips("OvRecFps", new[] { ("30", "30"), ("60", "60") }, _settings.RecordFps.ToString(), t => { _settings.RecordFps = int.Parse(t); _settings.Save(); }));
        var capButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        _ovRecBtn = TweakButton("Gravar vídeo", true, (_, _) => ToggleRecording());
        capButtons.Children.Add(_ovRecBtn);
        capButtons.Children.Add(TweakButton("Abrir fotos", false, (_, _) => OpenFolder(ScreenCapture.ShotsDir, _ovLastShot)));
        capButtons.Children.Add(TweakButton("Abrir vídeos", false, (_, _) => OpenFolder(ScreenCapture.VideosDir, _ovLastVideo)));
        cap.Children.Add(capButtons);
        _ovCaptureInfo = OvSmall();
        cap.Children.Add(_ovCaptureInfo);

        // Atalhos
        var keys = OvCard("MAIS ATALHOS", "Clique na caixa e aperte a tecla ou combinação (Esc cancela). Funcionam com o jogo em primeiro plano; " +
            "escolha teclas que o jogo não usa. O atalho de ligar/desligar o overlay fica no topo da aba.");
        foreach (var (key, label) in OverlayActions.All)
        {
            int index = Array.FindIndex(OverlayActions.All, a => a.Key == key);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var hint = OvSmall("", "Faint");
            var box = new TextBox { IsReadOnly = true, Cursor = Cursors.Hand, FontFamily = (FontFamily)FindResource("Mono"), IsReadOnlyCaretVisible = false,
                Text = HotkeyText(_settings.OverlayHotkeys?.GetValueOrDefault(key)), ToolTip = "Clique e aperte a tecla ou combinação (Esc cancela)" };
            void Store(uint mods, uint vk)
            {
                var map = _settings.OverlayHotkeys ??= new Dictionary<string, uint[]>();
                if (vk == 0) map.Remove(key); else map[key] = new[] { mods, vk };
                _settings.Save();
            }
            box.PreviewKeyDown += (_, e) =>
            {
                if (!ReadHotkey(e, out uint mods, out uint vk, out Key k)) return;
                var old = _settings.OverlayHotkeys?.GetValueOrDefault(key);
                if (k != Key.Escape)
                {
                    if (RegisterToolHotkey(index, mods, vk)) { Store(mods, vk); hint.Text = ""; }
                    else
                    {
                        RegisterToolHotkey(index, old?[0] ?? 0, old?[1] ?? 0);
                        hint.Text = $"{OverlayHotkey.Describe(mods, k.ToString())} já é usado (por outro programa ou outro atalho).";
                    }
                }
                box.Text = HotkeyText(_settings.OverlayHotkeys?.GetValueOrDefault(key));
                Keyboard.ClearFocus();
            };
            var clear = TweakButton("Limpar", false, (_, _) => { RegisterToolHotkey(index, 0, 0); Store(0, 0); box.Text = HotkeyText(null); hint.Text = ""; });
            clear.Margin = new Thickness(10, 0, 0, 0);
            clear.Padding = new Thickness(10, 4, 10, 4);
            var name = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Fg") };
            Grid.SetColumn(box, 1); Grid.SetColumn(clear, 2); Grid.SetColumn(hint, 3);
            hint.Margin = new Thickness(10, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name); row.Children.Add(box); row.Children.Add(clear); row.Children.Add(hint);
            keys.Children.Add(row);
        }

        // Aparência
        _ovAppearance = OvCard("APARÊNCIA", "Cores, fonte e contorno do texto. O tamanho e a opacidade do fundo ficam em Posição, acima.");
        BuildAppearance();

        // Perfis por jogo
        var prof = OvCard("PERFIS POR JOGO", "Salve o overlay atual (itens, posição e formato) para um jogo: quando ele estiver na tela, o perfil vale sozinho. " +
            "Cada perfil pode ligar o overlay sozinho e ter o próprio limite de FPS no driver NVIDIA.");
        var add = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var save = TweakButton("Salvar perfil com o overlay atual", true, (_, _) =>
        {
            string n = _ovProfileName!.Text.Trim();
            if (n.Length == 0) { _ovProfileHint!.Text = "Escreva o nome do executável do jogo (ex.: cs2.exe)."; return; }
            SaveGameProfile(n, null);
            _ovProfileName.Text = "";
        });
        save.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(save, Dock.Right);
        _ovProfileName = new TextBox { FontFamily = (FontFamily)FindResource("Mono"), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Executável do jogo, ex.: cs2.exe" };
        add.Children.Add(save);
        add.Children.Add(_ovProfileName);
        prof.Children.Add(add);
        _ovProfileHint = OvSmall("", "Faint");
        prof.Children.Add(_ovProfileHint);
        _ovProfilesList = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        prof.Children.Add(_ovProfilesList);

        BuildGameProfiles();
        BuildBenchHistory();
        RefreshOverlayTools();
        SyncProfileWatcher();
        if (OvNvidia) _ = LoadLimiterAsync();
    }

    private async Task LoadLimiterAsync()
    {
        if (_ovLimiterBody == null) return;
        (uint Global, uint Background) v;
        try { v = await Task.Run(FpsLimiter.ReadGlobal); }
        catch (Exception ex) { _ovLimiterInfo!.Text = "Não deu para ler o driver: " + ex.Message; return; }

        while (_ovLimiterBody.Children.Count > 2) _ovLimiterBody.Children.RemoveAt(2); // título e explicação ficam
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        void Row(int r, string label, uint value, string off, Action<uint> write)
        {
            var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Fg"), Margin = new Thickness(0, 0, 0, 8) };
            var cb = FpsCombo(value, off, fps => _ = WriteLimiter(() => write(fps), fps));
            cb.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetRow(t, r); Grid.SetRow(cb, r); Grid.SetColumn(cb, 1);
            grid.Children.Add(t); grid.Children.Add(cb);
        }
        Row(0, "Todos os jogos (global)", v.Global, "Desligado", FpsLimiter.SetGlobal);
        Row(1, "Jogo em segundo plano", v.Background, "Desligado", FpsLimiter.SetBackground);

        var custom = new TextBox { Width = 80, Margin = new Thickness(12, 0, 0, 8), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Outro valor (10 a 1000) e Enter" };
        custom.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (uint.TryParse(custom.Text.Trim(), out uint fps) && fps is >= 10 and <= 1000) _ = WriteLimiter(() => FpsLimiter.SetGlobal(fps), fps, reload: true);
            else _ovLimiterInfo!.Text = "Digite um número de 10 a 1000.";
        };
        var customLabel = new StackPanel { Orientation = Orientation.Horizontal };
        customLabel.Children.Add(custom);
        customLabel.Children.Add(new TextBlock { Text = "outro valor + Enter", Foreground = (Brush)FindResource("Faint"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 8) });
        Grid.SetColumn(customLabel, 2);
        grid.Children.Add(customLabel);

        _ovLimiterBody.Children.Add(grid);
        _ovLimiterInfo!.Text = v.Global > 0 ? $"Limite global ativo: {v.Global} FPS." : "Sem limite global.";
        _ovLimiterBody.Children.Add(_ovLimiterInfo);
    }

    private async Task WriteLimiter(Action write, uint fps, bool reload = false)
    {
        try
        {
            await Task.Run(write);
            if (fps > 0) { _settings.LimiterLastFps = fps; _settings.Save(); }
            _ovLimiterInfo!.Text = "Gravado no driver. Se o jogo já estava aberto e não mudou, reabra o jogo.";
            if (reload) await LoadLimiterAsync();
        }
        catch (Exception ex) { _ovLimiterInfo!.Text = "Não deu para gravar no driver: " + ex.Message; }
    }

    private static readonly string[] OvPalette = { "#A66BFF", "#4FA3FF", "#22D3EE", "#4ADE80", "#FBBF24", "#FB923C", "#F87171", "#FF79C6", "#F5F5F5", "#8C8C8C" };
    private static readonly string[] OvFonts = { "Segoe UI", "Bahnschrift", "Consolas", "Arial", "Verdana", "Tahoma" };
    /// <summary>Sub-opções de cor abertas pelo triângulo (começam fechadas a cada abertura do app).</summary>
    private bool _ovColValOpen, _ovColLblOpen, _ovColFpsOpen;

    private void BuildAppearance()
    {
        var host = _ovAppearance!;
        while (host.Children.Count > 2) host.Children.RemoveAt(2);
        var s = _settings;
        // level: 0 = grupo (VALORES/RÓTULOS), 1 = FPS/GPU/CPU/RAM, 2 = sub-opção de FPS. A coluna do nome tem largura fixa,
        // com o recuo dentro dela, para as bolinhas ficarem alinhadas em todas as linhas. toggle = linha com triângulo que abre/fecha.
        void Colors(string label, string current, Action<string> set, int level = 1, bool? open = null, Action? toggle = null, string? hint = null)
        {
            const double nameCol = 200;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var name = new StackPanel { Orientation = Orientation.Horizontal, Width = nameCol, VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent }; // fundo transparente: a linha toda responde ao clique
            name.Children.Add(new Border { Width = level * 18 });
            name.Children.Add(new TextBlock { Text = toggle == null ? "" : open == true ? "▾" : "▸", Width = 16, FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 12, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center });
            name.Children.Add(level == 0
                ? new TextBlock { Text = label, Style = (Style)FindResource("Label"), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center }
                : new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = level == 2 ? 12 : 13,
                    Foreground = (Brush)FindResource(level == 2 ? "Muted" : "Fg") });
            if (toggle != null)
            {
                name.Cursor = Cursors.Hand;
                name.ToolTip = open == true ? "Esconder as opções" : "Mostrar mais opções";
                name.MouseLeftButtonUp += (_, _) => { toggle(); BuildAppearance(); };
            }
            row.Children.Add(name);
            foreach (var hex in OvPalette)
            {
                bool sel = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                var sw = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand,
                    Background = OverlayStyle.Parse(hex, "#F5F5F5"), BorderThickness = new Thickness(sel ? 3 : 1), ToolTip = hex,
                    BorderBrush = sel ? (Brush)FindResource("Fg") : new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) };
                sw.MouseLeftButtonUp += (_, _) => { set(hex); _settings.Save(); BuildAppearance(); UpdateOverlay(); };
                row.Children.Add(sw);
            }
            if (hint != null)
                row.Children.Add(new TextBlock { Text = hint, FontSize = 11, Foreground = (Brush)FindResource("Faint"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            host.Children.Add(row);
        }

        // Dois grupos (Valores e Rótulos). Fechados (padrão), a cor do grupo vale para FPS, GPU, CPU e RAM de uma vez;
        // o triângulo abre uma cor para cada um, e o de FPS (Valores) abre a sub-opção de FPS/FRAME/1% LOW.
        // Cada cor cai na cor do grupo quando ainda não foi escolhida, então a migração das cores antigas é transparente.
        bool valCustom = new[] { s.OverlayColFpsVal, s.OverlayColGpuVal, s.OverlayColCpuVal, s.OverlayColRamVal }.Any(c => c != null);
        Colors("VALORES", s.OverlayValueColor, h =>
        {
            s.OverlayValueColor = h; // pinta os quatro grupos: tira as cores próprias de cada um
            s.OverlayColFpsVal = s.OverlayColGpuVal = s.OverlayColCpuVal = s.OverlayColRamVal = null;
        }, level: 0, open: _ovColValOpen, toggle: () => _ovColValOpen = !_ovColValOpen,
            hint: valCustom && !_ovColValOpen ? "cores próprias por grupo" : null);
        if (_ovColValOpen)
        {
            Colors("FPS", s.OverlayColFpsVal ?? s.OverlayValueColor, h => s.OverlayColFpsVal = h, open: _ovColFpsOpen, toggle: () => _ovColFpsOpen = !_ovColFpsOpen);
            if (_ovColFpsOpen) Colors("FPS, FRAME e 1% LOW", s.OverlayColFpsHead ?? s.OverlayFpsColor, h => s.OverlayColFpsHead = h, level: 2);
            Colors("GPU", s.OverlayColGpuVal ?? s.OverlayValueColor, h => s.OverlayColGpuVal = h);
            Colors("CPU", s.OverlayColCpuVal ?? s.OverlayValueColor, h => s.OverlayColCpuVal = h);
            Colors("RAM", s.OverlayColRamVal ?? s.OverlayValueColor, h => s.OverlayColRamVal = h);
        }
        bool lblCustom = new[] { s.OverlayColFpsLbl, s.OverlayColGpuLbl, s.OverlayColCpuLbl, s.OverlayColRamLbl }.Any(c => c != null);
        Colors("RÓTULOS", s.OverlayLabelColor, h =>
        {
            s.OverlayLabelColor = h;
            s.OverlayColFpsLbl = s.OverlayColGpuLbl = s.OverlayColCpuLbl = s.OverlayColRamLbl = null;
        }, level: 0, open: _ovColLblOpen, toggle: () => _ovColLblOpen = !_ovColLblOpen,
            hint: lblCustom && !_ovColLblOpen ? "cores próprias por grupo" : null);
        if (_ovColLblOpen)
        {
            Colors("FPS", s.OverlayColFpsLbl ?? s.OverlayLabelColor, h => s.OverlayColFpsLbl = h);
            Colors("GPU", s.OverlayColGpuLbl ?? s.OverlayLabelColor, h => s.OverlayColGpuLbl = h);
            Colors("CPU", s.OverlayColCpuLbl ?? s.OverlayLabelColor, h => s.OverlayColCpuLbl = h);
            Colors("RAM", s.OverlayColRamLbl ?? s.OverlayLabelColor, h => s.OverlayColRamLbl = h);
        }
        host.Children.Add(new TextBlock { Text = "FONTE", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 6, 0, 6) });
        host.Children.Add(OvChips("OvFont", OvFonts.Select(f => (f, f)), _settings.OverlayFont, f => { _settings.OverlayFont = f; _settings.Save(); UpdateOverlay(); }));
        var shadow = new CheckBox { Content = "Contorno no texto (lê melhor com o fundo transparente)", IsChecked = _settings.OverlayShadow, Margin = new Thickness(0, 6, 0, 8) };
        shadow.Click += (_, _) => { _settings.OverlayShadow = shadow.IsChecked == true; _settings.Save(); UpdateOverlay(); };
        host.Children.Add(shadow);
        var reset = TweakButton("Voltar ao padrão", false, (_, _) =>
        {
            _settings.OverlayFpsColor = "#A66BFF"; _settings.OverlayValueColor = "#F5F5F5"; _settings.OverlayLabelColor = "#8C8C8C";
            _settings.OverlayColFpsHead = _settings.OverlayColFpsVal = _settings.OverlayColGpuVal = _settings.OverlayColCpuVal = _settings.OverlayColRamVal = null;
            _settings.OverlayColFpsLbl = _settings.OverlayColGpuLbl = _settings.OverlayColCpuLbl = _settings.OverlayColRamLbl = null;
            _settings.OverlayFont = "Segoe UI"; _settings.OverlayShadow = false; _settings.Save();
            BuildAppearance(); UpdateOverlay();
        });
        host.Children.Add(reset);
    }

    private void BuildGameProfiles()
    {
        if (_ovProfilesList == null) return;
        _ovProfilesList.Children.Clear();
        var games = _settings.OverlayGames;
        if (games is not { Count: > 0 })
        {
            _ovProfilesList.Children.Add(OvSmall("Nenhum perfil ainda.", "Faint"));
            return;
        }
        foreach (var (key, p) in games.OrderBy(g => g.Key))
        {
            var box = new Border { BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 6) };
            var body = new StackPanel();
            box.Child = body;
            string where = (p.Corner ?? "TL") switch { "TC" => "centro superior", "TR" => "superior direito", "BL" => "inferior esquerdo", "BC" => "centro inferior", "BR" => "inferior direito", _ => "superior esquerdo" };
            var head = new WrapPanel();
            head.Children.Add(new TextBlock { Text = key + ".exe", FontWeight = FontWeights.Bold, FontSize = 14, Foreground = (Brush)FindResource("Fg"), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(new TextBlock { Text = $"{p.Items?.Count ?? 0} itens · {where} · {(p.Layout == "H" ? "barra" : "lista")}" + (_ovProfileKey == key ? " · na tela agora" : ""),
                Foreground = (Brush)FindResource("Muted"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            body.Children.Add(head);

            var opts = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var auto = new CheckBox { Content = "Ligar o overlay sozinho com este jogo", IsChecked = p.AutoShow, Margin = new Thickness(0, 0, 20, 8), VerticalAlignment = VerticalAlignment.Center };
            auto.Click += (_, _) => { p.AutoShow = auto.IsChecked == true; _settings.Save(); if (_ovProfileKey == key) SetActiveProfile(key, force: true); };
            opts.Children.Add(auto);
            if (OvNvidia)
            {
                opts.Children.Add(new TextBlock { Text = "Limite de FPS:", VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 8, 8) });
                var status = OvSmall("", "Faint");
                var cb = FpsCombo(p.FpsLimit, "Usar o global", async fps =>
                {
                    try
                    {
                        await Task.Run(() => FpsLimiter.SetGame(key, fps));
                        p.FpsLimit = fps; _settings.Save();
                        status.Text = "Gravado no driver.";
                    }
                    catch (Exception ex) { status.Text = "Não deu para gravar: " + ex.Message; }
                });
                cb.Margin = new Thickness(0, 0, 10, 8);
                opts.Children.Add(cb);
                status.VerticalAlignment = VerticalAlignment.Center;
                opts.Children.Add(status);
            }
            body.Children.Add(opts);

            var buttons = new WrapPanel();
            var upd = TweakButton("Atualizar com o overlay atual", false, (_, _) => SaveGameProfile(key, p));
            var del = TweakButton("Excluir", false, (_, _) => DeleteGameProfile(key));
            upd.Padding = del.Padding = new Thickness(10, 4, 10, 4);
            buttons.Children.Add(upd); buttons.Children.Add(del);
            body.Children.Add(buttons);
            _ovProfilesList.Children.Add(box);
        }
    }

    /// <summary>Textos e botões que mudam com o estado (benchmark, gravação, último jogo). Barato: roda a cada atualização com a aba aberta.</summary>
    private void RefreshOverlayTools()
    {
        if (_ovBenchBtn == null) return;
        _ovBenchBtn.Content = _frames.Recording ? "Parar benchmark" : _ovBenchPending ? "Cancelar (aguardando o jogo)" : "Começar benchmark";
        _ovBenchInfo!.Text = _frames.Recording ? $"Medindo {_ovBenchGame}… {Elapsed(_ovBenchStart)}"
                           : _ovBenchPending ? "Volte para o jogo: a medição começa quando ele estiver na tela." : "";

        _ovRecBtn!.Content = _rec.Running ? "Parar gravação" : "Gravar vídeo";
        _ovCaptureInfo!.Text = _rec.Running ? $"Gravando… {Elapsed(_rec.Started)} → {Path.GetFileName(_rec.File)}"
            : _ovRecError ?? (OvHasFfmpeg ? $"ffmpeg encontrado · vídeo com {(OvNvidia ? "NVENC (placa de vídeo)" : "x264 (processador)")}"
                                                         : "Para gravar vídeo, instale o ffmpeg na aba Instalar apps. A captura de tela funciona sem ele.");
        if (_ovProfileHint != null && _ovProfileName != null)
        {
            _ovProfileHint.Text = _ovLastGame != null ? $"Último jogo detectado: {_ovLastGame}.exe" : "Abra um jogo para sugerir o nome aqui.";
            if (_ovProfileName.Text.Length == 0 && !_ovProfileName.IsKeyboardFocused && _ovLastGame != null) _ovProfileName.Text = _ovLastGame + ".exe";
        }
    }

    private void BuildBenchHistory()
    {
        if (_ovBenchHistory == null) return;
        var list = _settings.BenchHistory ?? new List<BenchSummary>();
        _ovBenchHistory.Children.Clear();
        if (list.Count == 0) { _ovBenchHistory.Children.Add(OvSmall("Nenhum benchmark ainda.", "Faint")); return; }
        foreach (var b in list.Take(12))
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(buttons, Dock.Right);
            if (b.CsvPath != null)
            {
                var csv = TweakButton("CSV", false, (_, _) => OpenFolder(BenchDir, b.CsvPath));
                csv.Padding = new Thickness(10, 3, 10, 3); csv.Margin = new Thickness(8, 0, 0, 0);
                buttons.Children.Add(csv);
            }
            var rm = TweakButton("×", false, (_, _) => { _settings.BenchHistory?.Remove(b); _settings.Save(); BuildBenchHistory(); });
            rm.Padding = new Thickness(10, 3, 10, 3); rm.Margin = new Thickness(6, 0, 0, 0); rm.ToolTip = "Tirar da lista (o CSV continua na pasta)";
            buttons.Children.Add(rm);
            row.Children.Add(buttons);
            var t = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Fg") };
            t.Inlines.Add(new System.Windows.Documents.Run($"{b.Game}  ") { FontWeight = FontWeights.Bold });
            t.Inlines.Add(new System.Windows.Documents.Run($"{b.Start:dd/MM HH:mm} · {b.Seconds:0} s   ") { Foreground = (Brush)FindResource("Muted") });
            t.Inlines.Add(new System.Windows.Documents.Run($"méd {b.Avg:0} · mín {b.Min:0} · máx {b.Max:0} · 1% {b.Low1:0} · 0,1% {b.Low01:0} FPS"));
            row.Children.Add(t);
            _ovBenchHistory.Children.Add(row);
        }
    }

    private delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
