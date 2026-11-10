using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Tema Legacy: o visual da 0.12.0 (roxo → azul, hover azul, cantos arredondados e ícones de glifo) por cima do app atual.
/// As funções são as mesmas; só a aparência volta. As peças:
/// - ThemeLegacy.xaml (cópia do Theme.xaml da 0.12.0) entra por cima do Theme.xaml em <see cref="ThemeManager.UseLegacy"/>;
/// - a paleta antiga fica em ThemeManager.LegacyPalette;
/// - os cantos usam os tokens R* (XAML) e ThemeManager.R(…) (código): 0 no tema atual, o valor antigo no Legacy;
/// - FlatIcons não desenha nada no Legacy (ficam os glifos da fonte);
/// - aqui: cabeçalho, menu lateral e título da página.
/// Os estilos são lidos ao montar a janela, então trocar de tema reinicia o app.
/// </summary>
public partial class MainWindow
{
    /// <summary>Nome da página no cabeçalho: CAIXA ALTA no tema atual, normal no Legacy.</summary>
    private static string PageTitle(string name) =>
        ThemeManager.Legacy ? name : name.ToUpper(new System.Globalization.CultureInfo("pt-BR"));

    /// <summary>Cabeçalho e menu lateral da 0.12.0. Chamado logo depois do InitializeComponent.</summary>
    private void ApplyLegacyChrome()
    {
        if (!ThemeManager.Legacy) return;

        // Cabeçalho: sem a faixa com linha embaixo; logo maior, "Pcismo" em negrito + "Master" fino e a página em cinza.
        TitleBarLine.Visibility = Visibility.Collapsed;
        TitleSep.Visibility = Visibility.Collapsed;
        TitleBrandBar.Margin = new Thickness(30, 0, 0, 0);
        TitleBrandBar.VerticalAlignment = VerticalAlignment.Center;
        TitleLogo.Width = TitleLogo.Height = 22;
        TitleLogo.Margin = new Thickness(0, 0, 12, 0);
        TitleBrand.FontWeight = FontWeights.Normal;
        TitleBrand.FontSize = 13;
        TitleBrand.Inlines.Clear();
        TitleBrand.Inlines.Add(new Run("Pcismo") { FontWeight = FontWeights.Bold });
        TitleBrand.Inlines.Add(new Run("Master") { FontWeight = FontWeights.Light });
        TitlePage.ClearValue(TextBlock.FontFamilyProperty);
        TitlePage.FontSize = 13;
        TitlePage.Margin = new Thickness(16, 0, 0, 0);
        TitlePage.SetResourceReference(TextBlock.ForegroundProperty, "Faint");

        // Menu lateral: o painel arredondado solto da borda, como antes.
        Sidebar.Margin = new Thickness(14, 0, 0, 14);
        Sidebar.BorderThickness = new Thickness(1);
        Sidebar.CornerRadius = new CornerRadius(20);
    }

    /// <summary>Troca o tema (atual ↔ Legacy): salva e reabre o app, porque os estilos só são lidos ao abrir.</summary>
    private void SwitchLegacyTheme(bool legacy, Window? owner)
    {
        if (legacy == ThemeManager.Legacy) return;
        string nome = legacy ? "Legacy (o visual da 0.12.0)" : "atual";
        if (MessageBox.Show(owner ?? this, $"Trocar para o tema {nome}? O PcismoMaster reinicia para aplicar.",
                "Tema", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _settings.LegacyTheme = legacy;
        try { _settings.Save(); }
        catch (Exception ex) { MessageBox.Show(owner ?? this, $"Não deu para salvar o tema: {ex.Message}", "Tema"); return; }
        RestartApp();
    }

    /// <summary>Abre o app de novo e fecha este. A cópia nova espera esta soltar a instância única (ver App.OnStartup).</summary>
    private void RestartApp()
    {
        if (Environment.ProcessPath is not { } exe)
        {
            MessageBox.Show(this, "Feche e abra o PcismoMaster para aplicar o tema.", "Tema");
            return;
        }
        try { Process.Start(new ProcessStartInfo(exe, $"--restart {Environment.ProcessId}") { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Não deu para reabrir sozinho ({ex.Message}). Feche e abra o PcismoMaster para aplicar o tema.", "Tema");
            return;
        }
        Application.Current.Shutdown();
    }
}

public partial class MainWindow
{
    /// <summary>Seção "Tema" da janela Cores do programa: o tema atual ou o Legacy (visual da 0.12.0).</summary>
    private FrameworkElement ThemeChoice(Window owner)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 14) };
        var label = new TextBlock { Text = "TEMA", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        panel.Children.Add(label);
        var grid = new UniformGrid { Columns = 2 };
        panel.Children.Add(grid);

        var atual = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEE));
        var legacy = new LinearGradientBrush(Color.FromRgb(0xA6, 0x47, 0xFE), Color.FromRgb(0x29, 0xB4, 0xD9), 0);
        grid.Children.Add(ThemeOption("Atual", "Grafite, cantos retos e ícones chapados.", atual, new SolidColorBrush(Color.FromRgb(0x13, 0x13, 0x15)),
            !ThemeManager.Legacy, new Thickness(0, 0, 6, 0), () => SwitchLegacyTheme(false, owner)));
        grid.Children.Add(ThemeOption("Legacy", "O visual da 0.12.0: roxo → azul, hover azul, cantos arredondados e os ícones antigos.", legacy,
            new SolidColorBrush(Color.FromRgb(0x11, 0x0F, 0x1D)), ThemeManager.Legacy, new Thickness(6, 0, 0, 0), () => SwitchLegacyTheme(true, owner)));
        var note = new TextBlock { Text = "Trocar de tema reinicia o PcismoMaster. As funções são as mesmas nos dois.", FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Faint");
        panel.Children.Add(note);
        return panel;
    }

    private static Border ThemeOption(string title, string hint, Brush accent, Brush cardBg, bool selected, Thickness margin, Action pick)
    {
        // Miniatura: um cartão do tema com a faixa de destaque
        var mini = new Border { Width = 46, Height = 34, Background = cardBg, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0),
            CornerRadius = new CornerRadius(title == "Legacy" ? 7 : 0), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true,
            Child = new Border { Height = 8, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 6, 6), Background = accent,
                CornerRadius = new CornerRadius(title == "Legacy" ? 4 : 0) } };
        mini.SetResourceReference(Border.BorderBrushProperty, "LineStrong");
        var name = new TextBlock { Text = selected ? title + "  ✓" : title, FontWeight = FontWeights.SemiBold };
        var sub = new TextBlock { Text = hint, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Faint");
        var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txt.Children.Add(name); txt.Children.Add(sub);
        var dock = new DockPanel();
        DockPanel.SetDock(mini, Dock.Left);
        dock.Children.Add(mini); dock.Children.Add(txt);
        var row = new Border { Padding = new Thickness(10, 8, 10, 8), Margin = margin, BorderThickness = new Thickness(1), Child = dock,
            Cursor = selected ? null : System.Windows.Input.Cursors.Hand, CornerRadius = ThemeManager.R(10),
            ToolTip = selected ? "Tema em uso" : "Usar o tema " + title };
        row.SetResourceReference(Border.BorderBrushProperty, selected ? "Fg" : "Line");
        row.SetResourceReference(Border.BackgroundProperty, selected ? "Hover" : "Bg");
        if (!selected) row.MouseLeftButtonUp += (_, _) => pick();
        return row;
    }
}
