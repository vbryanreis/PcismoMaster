using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ResetExplode;

/// <summary>
/// Cores do programa (botão da paleta na barra de título). À esquerda, o que pintar (destaque e as três cores dos ícones);
/// à direita, um seletor de cor: quadrado de saturação × brilho, barra com todos os tons, prévia e código #RRGGBB.
/// Arrastar já mostra a cor no app inteiro; salva ao soltar o mouse.
/// </summary>
public partial class MainWindow
{
    private sealed record ColorTarget(string Title, string Hint, string DefaultHex, Func<string?> Get, Action<string?> Set);

    private void LoadCustomColors()
    {
        ThemeManager.CustomAccent = _settings.ColorAccent;
        ThemeManager.CustomIconA = _settings.ColorIconA;
        ThemeManager.CustomIconB = _settings.ColorIconB;
        ThemeManager.CustomIconC = _settings.ColorIconC;
        ThemeManager.AccentButtons = _settings.ColorAccentButtons;
    }

    private void ApplyCustomColors(bool save = true)
    {
        LoadCustomColors();
        ThemeManager.Apply(ThemeManager.IsLight);
        ApplyModeAccent();
        if (save) try { _settings.Save(); } catch { }
    }

    // ---- HSV ----

    private static Color FromHsv(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x),
            < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return ((h + 360) % 360, max == 0 ? 0 : d / max, max);
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ---- Janela ----

    private void Colors_Click(object sender, RoutedEventArgs e)
    {
        bool light = ThemeManager.IsLight;
        var targets = new[]
        {
            new ColorTarget("Destaque", "Etiquetas, links, seleção e realce do modo.", light ? "#2F5BE0" : "#7C9CFF", () => _settings.ColorAccent, v => _settings.ColorAccent = v),
            new ColorTarget("Ícones · cor 1", "A cor principal dos ícones.", light ? "#2F5BE0" : "#3D6BFF", () => _settings.ColorIconA, v => _settings.ColorIconA = v),
            new ColorTarget("Ícones · cor 2", "A cor de realce dos ícones.", light ? "#E8452A" : "#FF5A36", () => _settings.ColorIconB, v => _settings.ColorIconB = v),
            new ColorTarget("Ícones · cor 3", "A cor clara dos ícones (escura no modo claro).", light ? "#1E2A4A" : "#EFE6D2", () => _settings.ColorIconC, v => _settings.ColorIconC = v),
        };
        ColorTarget current = targets[0];
        double hue = 0, sat = 0, val = 0;
        bool syncing = false;

        var win = new Window { Title = "Cores do programa", Owner = this, Width = 720, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        win.SetResourceReference(Window.BackgroundProperty, "Bg");
        win.SetResourceReference(Window.ForegroundProperty, "Fg");
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        win.Content = root;
        root.Children.Add(new TextBlock { Text = "Cores do programa", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        root.Children.Add(ThemeChoice(win));
        root.Children.Add(OptText(ThemeManager.Legacy
            ? "No tema Legacy as cores são as da 0.12.0. As cores escolhidas aqui valem quando você voltar ao tema atual."
            : "Escolha à esquerda o que pintar e arraste no quadrado e na barra de tons. A cor muda no app na hora.", "Muted", 12, 16));

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(body);
        if (ThemeManager.Legacy) { body.IsEnabled = false; body.Opacity = 0.4; }

        // ---- Esquerda: o que pintar ----
        var list = new StackPanel();
        body.Children.Add(list);
        var rows = new List<(ColorTarget T, Border Row, Border Chip, TextBlock Code)>();
        Action<ColorTarget>? onSelect = null; // ligado ao Select depois que o seletor existe
        foreach (var t in targets)
        {
            var chip = new Border { Width = 28, Height = 28, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            chip.SetResourceReference(Border.BorderBrushProperty, "LineStrong");
            var name = new TextBlock { Text = t.Title, FontWeight = FontWeights.SemiBold };
            var code = new TextBlock { FontSize = 11, FontFamily = (FontFamily)FindResource("Mono") };
            code.SetResourceReference(TextBlock.ForegroundProperty, "Faint");
            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(name); txt.Children.Add(code);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(chip); sp.Children.Add(txt);
            var row = new Border { Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Child = sp };
            row.SetResourceReference(Border.BorderBrushProperty, "Line");
            row.MouseLeftButtonUp += (_, _) => onSelect?.Invoke(t);
            list.Children.Add(row);
            rows.Add((t, row, chip, code));
        }
        var btns = new CheckBox { Content = new TextBlock { Text = "Botões principais na cor de destaque", TextWrapping = TextWrapping.Wrap }, IsChecked = _settings.ColorAccentButtons, Margin = new Thickness(0, 10, 0, 0) };
        btns.Click += (_, _) => { _settings.ColorAccentButtons = btns.IsChecked == true; ApplyCustomColors(); };
        list.Children.Add(btns);

        // ---- Direita: seletor ----
        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        var hint = OptText("", "Muted", 12, 10);
        right.Children.Add(hint);

        const double W = 420, H = 220;
        var hueLayer = new Border();
        var whiteLayer = new Border { Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
        var blackLayer = new Border { Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
        var svMarker = new Ellipse { Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0 } };
        var sv = new Grid { Width = W, Height = H, Cursor = Cursors.Cross, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
        sv.Children.Add(hueLayer); sv.Children.Add(whiteLayer); sv.Children.Add(blackLayer); sv.Children.Add(svMarker);
        right.Children.Add(sv);

        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
        var hueMarker = new Border { Width = 6, Height = 22, BorderBrush = Brushes.White, BorderThickness = new Thickness(2), IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent };
        var hueBar = new Grid { Width = W, Height = 22, Margin = new Thickness(0, 12, 0, 0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
        hueBar.Children.Add(new Border { Background = rainbow, Margin = new Thickness(0, 3, 0, 3) });
        hueBar.Children.Add(hueMarker);
        right.Children.Add(hueBar);

        // prévia + código + padrão
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var swatch = new Border { Width = 56, Height = 36, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0) };
        swatch.SetResourceReference(Border.BorderBrushProperty, "LineStrong");
        var box = new TextBox { Width = 110, VerticalContentAlignment = VerticalAlignment.Center, FontFamily = (FontFamily)FindResource("Mono"), ToolTip = "Código da cor, ex.: #3D6BFF (Enter para usar)" };
        var reset = TweakButton("Padrão", false, (_, _) => { current.Set(null); ApplyCustomColors(); Select(current); });
        reset.Margin = new Thickness(12, 0, 0, 0);
        bottom.Children.Add(swatch); bottom.Children.Add(box); bottom.Children.Add(reset);
        right.Children.Add(bottom);

        // prévia dos ícones
        var preview = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        foreach (var k in new[] { "home", "gear", "palette", "trash", "shield", "bolt", "box", "target", "layers", "flame" })
            if (FlatIcons.Create(k, 26) is { } ic) { ic.Margin = new Thickness(0, 0, 12, 0); preview.Children.Add(ic); }
        right.Children.Add(preview);

        var bar = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        bar.Children.Add(TweakButton("Voltar tudo ao padrão", false, (_, _) =>
        {
            if (ThemeManager.Legacy) return;
            foreach (var t in targets) t.Set(null);
            _settings.ColorAccentButtons = false;
            btns.IsChecked = false;
            ApplyCustomColors();
            Select(current);
        }));
        bar.Children.Add(TweakButton("Fechar", true, (_, _) => win.Close()));
        root.Children.Add(bar);

        // ---- Lógica ----
        Color ColorOf(ColorTarget t) => ThemeManager.ParseColor(t.Get()) ?? (Color)ColorConverter.ConvertFromString(t.DefaultHex);

        void RefreshRows()
        {
            foreach (var (t, row, chip, code) in rows)
            {
                var c = ColorOf(t);
                chip.Background = new SolidColorBrush(c);
                code.Text = Hex(c) + (t.Get() == null ? "  · padrão" : "");
                row.SetResourceReference(Border.BorderBrushProperty, t == current ? "Fg" : "Line");
                row.SetResourceReference(Border.BackgroundProperty, t == current ? "Hover" : "Bg");
            }
        }

        void Draw()
        {
            var c = FromHsv(hue, sat, val);
            hueLayer.Background = new SolidColorBrush(FromHsv(hue, 1, 1));
            svMarker.Margin = new Thickness(sat * W - 8, (1 - val) * H - 8, 0, 0);
            hueMarker.Margin = new Thickness(hue / 360 * W - 3, 0, 0, 0);
            swatch.Background = new SolidColorBrush(c);
            syncing = true; box.Text = Hex(c); syncing = false;
        }

        void Select(ColorTarget t)
        {
            current = t;
            hint.Text = t.Title + ": " + t.Hint;
            (hue, sat, val) = ToHsv(ColorOf(t));
            Draw();
            RefreshRows();
        }

        void Commit(bool save)
        {
            current.Set(Hex(FromHsv(hue, sat, val)));
            ApplyCustomColors(save);
            RefreshRows();
        }

        void PickSv(Point p) { sat = Math.Clamp(p.X / W, 0, 1); val = Math.Clamp(1 - p.Y / H, 0, 1); Draw(); Commit(false); }
        void PickHue(Point p) { hue = Math.Clamp(p.X / W, 0, 1) * 359.9; Draw(); Commit(false); }

        sv.MouseLeftButtonDown += (_, a) => { sv.CaptureMouse(); PickSv(a.GetPosition(sv)); };
        sv.MouseMove += (_, a) => { if (sv.IsMouseCaptured) PickSv(a.GetPosition(sv)); };
        sv.MouseLeftButtonUp += (_, _) => { sv.ReleaseMouseCapture(); Commit(true); };
        hueBar.MouseLeftButtonDown += (_, a) => { hueBar.CaptureMouse(); PickHue(a.GetPosition(hueBar)); };
        hueBar.MouseMove += (_, a) => { if (hueBar.IsMouseCaptured) PickHue(a.GetPosition(hueBar)); };
        hueBar.MouseLeftButtonUp += (_, _) => { hueBar.ReleaseMouseCapture(); Commit(true); };
        void UseBox()
        {
            if (syncing || ThemeManager.ParseColor(box.Text) is not Color c) return;
            (hue, sat, val) = ToHsv(c);
            Draw();
            Commit(true);
        }
        box.KeyDown += (_, a) => { if (a.Key == Key.Enter) UseBox(); };
        box.LostFocus += (_, _) => UseBox();

        onSelect = Select;
        Select(current);
        win.ShowDialog();
    }
}
