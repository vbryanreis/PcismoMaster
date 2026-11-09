using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Aba JOGOS · Biblioteca: cartões com a capa de cada jogo, "Preparar e jogar" e a janela de ajustes por jogo.</summary>
public partial class MainWindow
{
    private List<LibGame> _libGames = new();
    private LibGame? _lg;          // jogo da janela de ajustes
    private GameProfile? _lp;      // cópia em edição (vira o perfil só ao salvar)
    private bool _lLoading, _launcherInited;
    private CancellationTokenSource? _launchCts;
    private Button? _playingButton;
    private readonly Dictionary<string, string?> _exeGuess = new();
    private readonly Dictionary<string, ImageSource?> _covers = new();
    private CheckBox _tCore0 = null!, _tPhys = null!, _tPCore = null!, _tPrio = null!, _tPlan = null!, _tGpu = null!, _tFso = null!;

    private static readonly (string Label, string Token, string? Hint)[] ExtraPresets =
    {
        ("Pular intro (-novid)", "-novid", null), ("Prioridade alta (-high)", "-high", null), ("Sem limite de FPS (+fps_max 0)", "+fps_max 0", null),
        ("Sem joystick (-nojoy)", "-nojoy", null), ("Abrir console (-console)", "-console", null),
        ("Programas de terceiros (-allow_third_party_software)", "-allow_third_party_software",
         "CS2: libera programas que o modo confiável bloqueia, como a captura de jogo do OBS, ReShade e alguns overlays.\n" +
         "Atenção: a Valve diz que usar esta opção pode reduzir o seu Fator de Confiança (Trust Factor) no matchmaking."),
    };

    private void InitLauncher()
    {
        if (_launcherInited) return;
        _launcherInited = true;
        foreach (var (label, token, hint) in ExtraPresets)
        {
            var b = new Button
            {
                Content = label, Tag = token, Style = (Style)FindResource("OutlineButton"),
                Padding = new Thickness(10, 4, 10, 4), FontSize = 12, Margin = new Thickness(0, 0, 6, 6),
                ToolTip = hint,
            };
            b.Click += (_, _) =>
            {
                string t = (string)b.Tag;
                if (!LExtra.Text.Contains(t, StringComparison.OrdinalIgnoreCase))
                    LExtra.Text = (LExtra.Text.Trim() + " " + t).Trim();
            };
            LExtraPresets.Children.Add(b);
        }
        BuildToggleRows();
        PageLauncher.SizeChanged += (_, _) => LDialog.MaxHeight = Math.Max(300, PageLauncher.ActualHeight - 48);
        (_settings.LibListView ? LViewList : LViewCards).IsChecked = true;
        _ = ScanGamesAsync();
    }

    private void LView_Changed(object sender, RoutedEventArgs e)
    {
        if (!_launcherInited) return;
        bool list = LViewList.IsChecked == true;
        if (_settings.LibListView != list) { _settings.LibListView = list; try { _settings.Save(); } catch { } }
        ApplyGameFilter();
    }

    // ---------------- Biblioteca ----------------

    private async Task ScanGamesAsync()
    {
        BtnScanGames.IsEnabled = false;
        LGameCount.Text = "Procurando seus jogos…";
        try
        {
            var custom = _settings.CustomGames.ToList();
            _libGames = await Task.Run(() => GameLibrary.Scan(custom));
            ApplyGameFilter();
            LGameCount.Text = _libGames.Count == 0
                ? "Nenhum jogo encontrado. Use \"Adicionar jogo (.exe)…\"."
                : $"{_libGames.Count} jogo(s)";
        }
        catch (Exception ex) { LGameCount.Text = "Falhou: " + ex.Message; }
        finally { BtnScanGames.IsEnabled = true; }
    }

    private void ApplyGameFilter()
    {
        string q = LGameSearch.Text.Trim();
        bool list = _settings.LibListView;
        LGameGrid.Children.Clear();
        _playingButton = null;
        foreach (var g in _libGames.Where(g => q.Length == 0 || g.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            LGameGrid.Children.Add(list ? GameRow(g) : GameCard(g));
    }

    /// <summary>Linha do modo lista: banner horizontal, nome, loja e pasta, e os mesmos botões do cartão.</summary>
    private FrameworkElement GameRow(LibGame g)
    {
        var banner = new Border { Width = 184, Height = 86, CornerRadius = new CornerRadius(0), Background = (Brush)FindResource("AccentGradient"),
            Child = new TextBlock { Text = Initials(g.Name), FontSize = 26, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent"),
                Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        _ = LoadArtAsync(g, banner, vertical: false);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
        info.Children.Add(new TextBlock { Text = g.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        info.Children.Add(new TextBlock { Text = g.Store.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 3, 0, 3) });
        info.Children.Add(new TextBlock { Text = g.InstallDir, FontSize = 11.5, Foreground = (Brush)FindResource("Muted"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = g.InstallDir });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(banner);
        Grid.SetColumn(info, 1); grid.Children.Add(info);
        var buttons = GameButtons(g);
        buttons.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(buttons, 2); grid.Children.Add(buttons);

        var row = new Border { Style = (Style)FindResource("CardBorder"), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10), Child = grid };
        // A lista ocupa a largura toda (o WrapPanel não estica sozinho).
        row.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth)) { Source = LGameGrid });
        return row;
    }

    private static string Initials(string name)
        => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpperInvariant(s[0])));

    /// <summary>"Preparar e jogar" e "Ajustes" (mesmos nos cartões e na lista).</summary>
    /// <param name="fill">Cartão: os dois botões dividem a largura do cartão (nunca passam da borda).</param>
    private Grid GameButtons(LibGame g, bool fill = false)
    {
        var play = new Button { Content = _playingKey == g.Key ? "Parar" : "Preparar e jogar", Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(8, 6, 8, 6), FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0) };
        play.Click += (_, _) => PlayGame(g, play);
        if (_playingKey == g.Key) _playingButton = play;
        var settings = new Button { Content = "Ajustes", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(10, 6, 10, 6), FontSize = 11.5 };
        settings.Click += (_, _) => OpenGameSettings(g);
        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = fill ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttons.Children.Add(play);
        Grid.SetColumn(settings, 1);
        buttons.Children.Add(settings);
        return buttons;
    }

    private void LGameSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (LGameGrid == null) return;
        ApplyGameFilter();
    }

    private async void BtnScanGames_Click(object sender, RoutedEventArgs e) => await ScanGamesAsync();

    private async void BtnAddExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe", Title = "Escolha o .exe do jogo" };
        if (dlg.ShowDialog(this) != true) return;
        if (!_settings.CustomGames.Any(c => c.Exe.Equals(dlg.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.CustomGames.Add(new CustomGame { Name = Path.GetFileNameWithoutExtension(dlg.FileName), Exe = dlg.FileName });
            _settings.Save();
        }
        await ScanGamesAsync();
    }

    /// <summary>Cartão do jogo: capa, nome, loja e os botões "Preparar e jogar" e "Ajustes".</summary>
    private FrameworkElement GameCard(LibGame g)
    {
        const double w = 184, coverH = 260;
        var cover = new Border { Height = coverH, CornerRadius = new CornerRadius(0), Background = (Brush)FindResource("AccentGradient") };
        var initials = new TextBlock
        {
            Text = Initials(g.Name),
            FontSize = 44, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent"), Opacity = 0.85,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        cover.Child = initials;
        _ = LoadArtAsync(g, cover, vertical: true);

        var info = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };
        info.Children.Add(new TextBlock { Text = g.Name.ToUpperInvariant(), FontWeight = FontWeights.SemiBold, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = g.Name });
        info.Children.Add(new TextBlock { Text = g.Store.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 2, 0, 0) });
        var body = new StackPanel();
        body.Children.Add(cover);
        body.Children.Add(info);
        var card = new Border
        {
            Width = w, CornerRadius = new CornerRadius(0), Background = (Brush)FindResource("Card"),
            BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1), Child = body,
        };

        var buttons = GameButtons(g, fill: true);
        buttons.Margin = new Thickness(0, 8, 0, 0);

        var stack = new StackPanel { Width = w, Margin = new Thickness(0, 0, 16, 20) };
        stack.Children.Add(card);
        stack.Children.Add(buttons);
        return stack;
    }

    /// <summary>Carrega a capa (cartões) ou o banner (lista) fora da thread da janela, já reduzida ao tamanho do quadro
    /// (pouca memória). Se não houver no disco, a arte é baixada uma vez da Steam.</summary>
    private async Task LoadArtAsync(LibGame g, Border cover, bool vertical)
    {
        string key = g.Key + (vertical ? "|v" : "|h");
        if (!_covers.TryGetValue(key, out var img))
        {
            img = await Task.Run<ImageSource?>(() =>
            {
                try
                {
                    if (g.ArtPath(vertical) is not string path) return null;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.DecodePixelWidth = vertical ? 340 : 368;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch { return null; }
            });
            _covers[key] = img;
        }
        if (img == null) return;
        var brush = new ImageBrush(img) { Stretch = Stretch.UniformToFill, AlignmentY = vertical ? AlignmentY.Top : AlignmentY.Center };
        brush.Freeze();
        cover.Background = brush;
        cover.Child = null;
    }

    // ---------------- Preparar e jogar ----------------

    private string? _playingKey;

    private async void PlayGame(LibGame g, Button button)
    {
        if (_launchCts != null)
        {
            if (_playingKey == g.Key) _launchCts.Cancel();
            else LStatus.Text = "Já tem um jogo sendo acompanhado. Clique em \"Parar\" nele antes.";
            return;
        }
        if (!_settings.GameProfiles.TryGetValue(g.Key, out var p)) { p = new GameProfile(); _settings.GameProfiles[g.Key] = p; }

        _launchCts = new CancellationTokenSource();
        _playingKey = g.Key;
        _playingButton = button;
        button.Content = "Parar";
        try
        {
            await GameLauncher.RunAsync(g, p,
                t => Dispatcher.BeginInvoke(new Action(() => LStatus.Text = t)),
                exe => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!string.Equals(p.ExePath, exe, StringComparison.OrdinalIgnoreCase))
                    {
                        p.ExePath = exe;
                        try { _settings.Save(); } catch { }
                    }
                })),
                _launchCts.Token);
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
            _playingKey = null;
            if (_playingButton != null) _playingButton.Content = "Preparar e jogar";
            _playingButton = null;
        }
    }

    // ---------------- Janela de ajustes ----------------

    /// <summary>Linhas com ícone, título, selo (SEGURO/AVANÇADO), descrição e o interruptor à direita, como no exemplo.</summary>
    private void BuildToggleRows()
    {
        CheckBox Row(Panel target, string glyph, string title, bool advanced, string desc)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = glyph, FontFamily = (FontFamily)FindResource("Icons"), FontSize = 16, Foreground = (Brush)FindResource("Accent"),
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            var badge = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(7, 2, 7, 2),
                Child = new TextBlock { Text = advanced ? "AVANÇADO" : "SEGURO" } };
            if (advanced) { badge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xCF, 0x4D)); TextElement.SetForeground(badge, (Brush)FindResource("TempWarn")); }
            head.Children.Add(badge);
            var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            text.Children.Add(head);
            var d = new TextBlock { Text = desc, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            text.Children.Add(d);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            var sw = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Tag = d };
            Grid.SetColumn(sw, 2);
            grid.Children.Add(sw);
            target.Children.Add(grid);
            return sw;
        }

        _tCore0 = Row(LPerfRows, "", "Liberar o núcleo 0", false, "Mantém o jogo fora do núcleo 0, o mais ocupado pelo Windows e pelos drivers.");
        _tPhys = Row(LPerfRows, "", "Só núcleos físicos (sem SMT/Hyper-Threading)", true,
            "Usa uma thread por núcleo físico: frametimes mais estáveis em alguns jogos, menos FPS em outros.");
        _tPCore = Row(LPerfRows, "", "Só núcleos de desempenho (sem E-cores)", false, "Deixa o jogo só nos P-cores das CPUs Intel híbridas.");
        _tPrio = Row(LPerfRows, "", "Prioridade alta", false, "Pede ao Windows mais tempo de processador para o jogo.");
        _tPlan = Row(LPerfRows, "", "Plano de energia Alto desempenho", false, "Só enquanto o jogo estiver aberto; depois volta ao plano anterior.");
        _tGpu = Row(LGfxRows, "", "Placa de vídeo dedicada", false, "Força a GPU de alto desempenho neste jogo (útil em notebooks).");
        _tFso = Row(LGfxRows, "", "Desativar otimizações de tela cheia", true,
            "Desliga o modo de tela cheia otimizada do Windows para este .exe. Pode reduzir o atraso em alguns jogos e piorar em outros.");

        if (!CpuTopology.IsHybrid)
        {
            _tPCore.IsEnabled = false;
            ((TextBlock)_tPCore.Tag).Text = "Seu processador não tem E-cores; não se aplica.";
        }
        if (!CpuTopology.HasSmt)
        {
            _tPhys.IsEnabled = false;
            ((TextBlock)_tPhys.Tag).Text = "Seu processador não tem SMT/Hyper-Threading; não se aplica.";
        }
    }

    private async void OpenGameSettings(LibGame g)
    {
        _lg = g;
        _lp = (_settings.GameProfiles.TryGetValue(g.Key, out var saved) ? saved : new GameProfile()).Clone();
        var p = _lp;
        _lLoading = true;
        LDlgTitle.Text = $"Ajustes do jogo: {g.Name}";
        LDlgDesc.Text = $"Valem para {g.Name} quando aberto por \"Preparar e jogar\". Nada é alterado nos arquivos do jogo nem na memória dele: " +
                        "o Windows só recebe instruções de como agendar o processo.";
        _tCore0.IsChecked = p.FreeCore0;
        _tPhys.IsChecked = p.PhysicalOnly && _tPhys.IsEnabled;
        _tPCore.IsChecked = p.PCoresOnly && _tPCore.IsEnabled;
        _tPrio.IsChecked = p.Priority == "high";
        _tPlan.IsChecked = p.HighPerfPlan;
        _tGpu.IsChecked = p.GpuHigh;
        _tFso.IsChecked = p.DisableFso;
        SetChip(LModePanel, p.WinMode);
        SetChip(LResPanel, p.Res);
        SetChip(LHzPanel, p.Hz.ToString());
        SetChip(LStylePanel, p.Style);
        LExtra.Text = p.Extra;
        LHelpers.ItemsSource = null;
        LHelpers.ItemsSource = p.Helpers;
        LSteamBox.Visibility = g.IsSteam ? Visibility.Visible : Visibility.Collapsed;
        LSteamNow.Text = g.IsSteam ? "Opções atuais na Steam: " + (SteamInfo.GetLaunchOptions(g.Id) is { Length: > 0 } cur ? cur : "(nenhuma)") : "";
        LGfxNote.Text = g.IsSteam
            ? "Os argumentos só passam a valer na Steam depois de APLICAR NAS OPÇÕES DA STEAM (a Steam é fechada nessa hora)."
            : g.LaunchUri != null
                ? "Esta loja abre o jogo por um atalho que não aceita argumentos: use as opções de inicialização da própria loja."
                : "Os argumentos são enviados direto ao .exe em \"Preparar e jogar\".";
        _lLoading = false;
        UpdateLauncherPreview();
        LOverlay.Visibility = Visibility.Visible;
        await ShowExeAsync(g, p);
    }

    private static void SetChip(Panel panel, string tag)
    {
        var chips = panel.Children.OfType<RadioButton>().ToList();
        var hit = chips.FirstOrDefault(c => (string)c.Tag == tag) ?? chips.FirstOrDefault();
        if (hit != null) hit.IsChecked = true;
    }

    private async Task ShowExeAsync(LibGame g, GameProfile p)
    {
        string? exe = p.ExePath ?? g.ExePath;
        if (exe == null)
        {
            LExeText.Text = "Procurando o executável…";
            if (!_exeGuess.TryGetValue(g.Key, out exe))
            {
                string dir = g.InstallDir, name = g.Name;
                exe = await Task.Run(() => GameLauncher.FindMainExe(dir, name));
                _exeGuess[g.Key] = exe;
            }
            if (_lg != g) return; // a janela já é de outro jogo
            LExeText.Text = exe != null ? $"{exe} (detectado; confirme ou troque)" : "Não identificado. Ele é descoberto ao jogar, ou escolha em \"Trocar .exe\".";
        }
        else LExeText.Text = exe;
    }

    /// <summary>Lê os campos da janela para a cópia em edição (os argumentos e a prévia mudam na hora).</summary>
    private void ReadDialogIntoProfile()
    {
        if (_lp == null) return;
        _lp.FreeCore0 = _tCore0.IsChecked == true;
        _lp.PhysicalOnly = _tPhys.IsChecked == true;
        _lp.PCoresOnly = _tPCore.IsChecked == true;
        if (_tPrio.IsChecked == true) _lp.Priority = "high";
        else if (_lp.Priority == "high") _lp.Priority = "normal";
        _lp.HighPerfPlan = _tPlan.IsChecked == true;
        _lp.GpuHigh = _tGpu.IsChecked == true;
        _lp.DisableFso = _tFso.IsChecked == true;
        _lp.WinMode = CheckedTag(LModePanel) ?? "default";
        _lp.Res = CheckedTag(LResPanel) ?? "default";
        _lp.Hz = int.TryParse(CheckedTag(LHzPanel), out int hz) ? hz : 0;
        _lp.Style = CheckedTag(LStylePanel) ?? "source";
        _lp.Extra = LExtra.Text.Trim();
    }

    private void LProfile_Changed(object sender, RoutedEventArgs e) { if (!_lLoading) { ReadDialogIntoProfile(); UpdateLauncherPreview(); } }
    private void LExtra_Changed(object sender, TextChangedEventArgs e) { if (!_lLoading) { ReadDialogIntoProfile(); UpdateLauncherPreview(); } }

    private void UpdateLauncherPreview()
    {
        if (_lp == null || LPreview == null) return;
        string a = GameLauncher.BuildArgs(_lp);
        LPreview.Text = a.Length > 0 ? a : "(nenhum argumento)";
    }

    private void BtnDlgCancel_Click(object sender, RoutedEventArgs e)
    {
        LOverlay.Visibility = Visibility.Collapsed;
        _lp = null; _lg = null;
    }

    /// <summary>Salva o perfil e grava já no Windows o que é "por .exe" (GPU dedicada e tela cheia).</summary>
    private void BtnDlgSave_Click(object sender, RoutedEventArgs e)
    {
        if (_lg == null || _lp == null) return;
        ReadDialogIntoProfile();
        _settings.GameProfiles[_lg.Key] = _lp;
        try { _settings.Save(); } catch { }
        string? exe = _lp.ExePath ?? _lg.ExePath ?? (_exeGuess.TryGetValue(_lg.Key, out var guess) ? guess : null);
        if (exe != null && File.Exists(exe))
        {
            try { GameLauncher.SetGpuPreference(exe, _lp.GpuHigh); } catch { }
            try { GameLauncher.SetDisableFso(exe, _lp.DisableFso); } catch { }
        }
        LStatus.Text = $"Ajustes de {_lg.Name} salvos.";
        LOverlay.Visibility = Visibility.Collapsed;
        _lp = null; _lg = null;
    }

    private void BtnRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (_lp == null) return;
        _tPrio.IsChecked = _tPlan.IsChecked = _tGpu.IsChecked = _tFso.IsChecked = true;
        ReadDialogIntoProfile();
    }

    private async void BtnPickExe_Click(object sender, RoutedEventArgs e)
    {
        if (_lg == null || _lp == null) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Programas (*.exe)|*.exe", Title = "Escolha o .exe do jogo",
            InitialDirectory = Directory.Exists(_lg.InstallDir) ? _lg.InstallDir : "",
        };
        if (dlg.ShowDialog(this) != true) return;
        _lp.ExePath = dlg.FileName;
        await ShowExeAsync(_lg, _lp);
    }

    // ---------------- Programas auxiliares ----------------

    private void BtnAddHelper_Click(object sender, RoutedEventArgs e)
    {
        if (_lp == null) return;
        var dlg = new OpenFileDialog { Filter = "Programas e scripts|*.exe;*.bat;*.cmd;*.lnk", Title = "Programa para abrir junto com o jogo" };
        if (dlg.ShowDialog(this) != true) return;
        _lp.Helpers.Add(new HelperProgram { Path = dlg.FileName, CloseWithGame = LHelperClose.IsChecked == true });
        LHelpers.ItemsSource = null;
        LHelpers.ItemsSource = _lp.Helpers;
    }

    private void BtnRemoveHelper_Click(object sender, RoutedEventArgs e)
    {
        if (_lp == null || ((FrameworkElement)sender).Tag is not HelperProgram h) return;
        _lp.Helpers.Remove(h);
        LHelpers.ItemsSource = null;
        LHelpers.ItemsSource = _lp.Helpers;
    }

    // ---------------- Steam: opções de inicialização ----------------

    private async void BtnSteamApply_Click(object sender, RoutedEventArgs e)
    {
        var g = _lg;
        if (g == null || !g.IsSteam || _lp == null) return;
        ReadDialogIntoProfile();
        string opts = GameLauncher.BuildArgs(_lp);
        BtnSteamApply.IsEnabled = false;
        try
        {
            if (SteamInfo.IsRunning)
            {
                var ask = MessageBox.Show(this,
                    "A Steam precisa ser fechada para gravar as opções de inicialização (senão ela as sobrescreve ao sair).\n\n" +
                    "Fechar a Steam agora? Jogos abertos serão encerrados. Feito um backup do arquivo antes de alterar.",
                    "Jogos · Steam", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ask != MessageBoxResult.Yes) return;
                LSteamNow.Text = "Fechando a Steam…";
                if (!await SteamInfo.ShutdownAsync()) { LSteamNow.Text = "A Steam não fechou. Feche-a manualmente e tente de novo."; return; }
            }
            string? err = await Task.Run(() => SteamInfo.SetLaunchOptions(g.Id, opts));
            LSteamNow.Text = err ?? "Opções atuais na Steam: " + (SteamInfo.GetLaunchOptions(g.Id) is { Length: > 0 } cur ? cur : "(nenhuma)");
        }
        finally { BtnSteamApply.IsEnabled = true; }
    }
}
