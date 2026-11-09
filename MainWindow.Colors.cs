using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Cores do programa (botão da paleta na barra de título): cor de destaque e as três cores dos ícones chapados,
/// cada uma com cores prontas, código #RRGGBB e "Padrão". Vale na hora e fica salvo nas configurações.
/// </summary>
public partial class MainWindow
{
    private static readonly (string Name, string Hex)[] ColorPresets =
    {
        ("Azul", "#3D6BFF"), ("Laranja", "#FF5A36"), ("Vermelho", "#E5484D"), ("Rosa", "#FF5C9A"), ("Roxo", "#9B6BFF"),
        ("Ciano", "#3FC8E0"), ("Verde", "#4CC97A"), ("Amarelo", "#F2B93B"), ("Creme", "#EFE6D2"), ("Branco", "#ECECEE"), ("Grafite", "#5A5A63"),
    };

    private void LoadCustomColors()
    {
        ThemeManager.CustomAccent = _settings.ColorAccent;
        ThemeManager.CustomIconA = _settings.ColorIconA;
        ThemeManager.CustomIconB = _settings.ColorIconB;
        ThemeManager.CustomIconC = _settings.ColorIconC;
        ThemeManager.AccentButtons = _settings.ColorAccentButtons;
    }

    private void ApplyCustomColors()
    {
        LoadCustomColors();
        ThemeManager.Apply(ThemeManager.IsLight);
        ApplyModeAccent();
        try { _settings.Save(); } catch { }
    }

    private void Colors_Click(object sender, RoutedEventArgs e)
    {
        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        var win = new Window { Title = "Cores do programa", Owner = this, Width = 560, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 760 } };
        win.SetResourceReference(Window.BackgroundProperty, "Bg");
        win.SetResourceReference(Window.ForegroundProperty, "Fg");

        root.Children.Add(new TextBlock { Text = "Cores do programa", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        root.Children.Add(OptText("Escolha uma cor pronta ou digite o código (#RRGGBB). Vale na hora, no modo escuro e no claro. \"Padrão\" volta à cor original.", "Muted", 12, 14));

        // Prévia com os ícones novos
        var preview = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var k in new[] { "home", "gear", "palette", "trash", "shield", "bolt", "box", "target" })
            if (FlatIcons.Create(k, 30) is { } ic) { ic.Margin = new Thickness(0, 0, 14, 0); preview.Children.Add(ic); }
        var sample = new Border { Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 16), Child = preview };
        sample.SetResourceReference(Border.BackgroundProperty, "Card");
        root.Children.Add(sample);

        ColorRow(root, "Cor de destaque", "Etiquetas, links, seleção e o realce do modo.", () => _settings.ColorAccent, v => _settings.ColorAccent = v);
        var btns = new CheckBox { Content = "Botões principais na cor de destaque (em vez de branco/preto)", IsChecked = _settings.ColorAccentButtons, Margin = new Thickness(0, -4, 0, 16) };
        btns.Click += (_, _) => { _settings.ColorAccentButtons = btns.IsChecked == true; ApplyCustomColors(); };
        root.Children.Add(btns);
        ColorRow(root, "Ícones · cor 1", "A cor principal dos ícones (azul no padrão).", () => _settings.ColorIconA, v => _settings.ColorIconA = v);
        ColorRow(root, "Ícones · cor 2", "A cor de realce dos ícones (laranja no padrão).", () => _settings.ColorIconB, v => _settings.ColorIconB = v);
        ColorRow(root, "Ícones · cor 3", "A cor clara dos ícones (creme no padrão; escura no modo claro).", () => _settings.ColorIconC, v => _settings.ColorIconC = v);

        var bar = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        bar.Children.Add(TweakButton("Voltar tudo ao padrão", false, (_, _) =>
        {
            _settings.ColorAccent = _settings.ColorIconA = _settings.ColorIconB = _settings.ColorIconC = null;
            _settings.ColorAccentButtons = false;
            ApplyCustomColors();
            win.Close();
            Colors_Click(sender, e);
        }));
        bar.Children.Add(TweakButton("Fechar", true, (_, _) => win.Close()));
        root.Children.Add(bar);
        win.ShowDialog();
    }

    /// <summary>Uma linha de cor: título, explicação, quadradinhos das cores prontas, campo #RRGGBB e "Padrão".</summary>
    private void ColorRow(Panel root, string title, string hint, Func<string?> get, Action<string?> set)
    {
        root.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
        root.Children.Add(OptText(hint, "Faint", 11.5, 6));
        var swatches = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        var box = new TextBox { Width = 110, Text = get() ?? "", Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Código da cor, ex.: #3D6BFF" };
        void Pick(string? hex)
        {
            set(hex);
            box.Text = hex ?? "";
            ApplyCustomColors();
        }
        foreach (var (name, hex) in ColorPresets)
        {
            var b = new Button { Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 6), ToolTip = $"{name} ({hex})", Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderThickness = new Thickness(1) };
            b.SetResourceReference(Button.BorderBrushProperty, "LineStrong");
            b.Template = SwatchTemplate();
            b.Click += (_, _) => Pick(hex);
            swatches.Children.Add(b);
        }
        root.Children.Add(swatches);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        row.Children.Add(box);
        var use = TweakButton("Usar", false, (_, _) =>
        {
            if (ThemeManager.ParseColor(box.Text) is Color c) Pick($"#{c.R:X2}{c.G:X2}{c.B:X2}");
            else MessageBox.Show(this, "Código de cor inválido. Use o formato #RRGGBB, por exemplo #3D6BFF.", "Cores do programa");
        });
        use.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(use);
        var reset = TweakButton("Padrão", false, (_, _) => Pick(null));
        reset.Margin = new Thickness(0);
        row.Children.Add(reset);
        root.Children.Add(row);
    }
}
