using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// INÍCIO: "O que você quer fazer?" com tarefas em blocos grandes e uma busca por tudo que o app faz; modo Simples/Avançado
/// (o Simples esconde sensores, overclock, scripts e ajustes técnicos) e a faixa com a explicação curta de cada página.
/// </summary>
public partial class MainWindow
{
    private StackPanel? _homeResults;

    private bool Advanced => _settings.AdvancedMode;

    private void InitHome()
    {
        HomeHost.Children.Clear();
        HomeHost.Children.Add(new TextBlock { Text = "Olá! O que você quer fazer?", Style = (Style)FindResource("H1") });
        HomeHost.Children.Add(new TextBlock { Text = "Escolha uma tarefa ou procure pelo nome. Nada muda no seu PC sem você confirmar, e quase tudo tem como desfazer.",
            Style = (Style)FindResource("Sub"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 18) });

        var search = new TextBox { Tag = "Procurar… (ex.: limpar, driver, bloatware, temperatura, jogos)", FontSize = 15, Padding = new Thickness(14, 11, 14, 11), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 420 };
        _homeResults = new StackPanel { Margin = new Thickness(0, 10, 0, 0), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        search.TextChanged += (_, _) => ShowHomeResults(search.Text);
        search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && PageCatalog.Search(search.Text, Advanced).FirstOrDefault() is { } first) GoTo(first.Nav);
            if (e.Key == Key.Escape) search.Text = "";
        };
        HomeHost.Children.Add(search);
        HomeHost.Children.Add(_homeResults);

        HomeHost.Children.Add(new TextBlock { Text = "TAREFAS MAIS COMUNS", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 26, 0, 10) });
        var tiles = new WrapPanel();
        foreach (var t in PageCatalog.Tasks) tiles.Children.Add(TaskTile(t));
        HomeHost.Children.Add(tiles);

        // Explicação dos modos, com o botão de trocar.
        var modeCard = new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 18, 12, 0), MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left };
        var mc = new StackPanel();
        mc.Children.Add(new TextBlock { Text = "MODO SIMPLES E MODO AVANÇADO", Style = (Style)FindResource("Label") });
        mc.Children.Add(new TextBlock { Name = "HomeModeText", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), FontSize = 13 });
        var row = new WrapPanel();
        var toggle = new Button { Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 0) };
        toggle.Click += Mode_Click;
        row.Children.Add(toggle);
        var help = new Button { Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(16, 8, 16, 8), Content = "Mostrar as explicações no topo das páginas" };
        help.Click += (_, _) => { _settings.HidePageHelp = false; _settings.Save(); help.Visibility = Visibility.Collapsed; };
        help.Visibility = _settings.HidePageHelp && !Advanced ? Visibility.Visible : Visibility.Collapsed;
        row.Children.Add(help);
        mc.Children.Add(row);
        modeCard.Child = mc;
        HomeHost.Children.Add(modeCard);
        _homeModeText = (TextBlock)mc.Children[1];
        _homeModeButton = toggle;
        _homeHelpButton = help;

        foreach (var p in PageCatalog.Pages)
            if (FindName(p.Nav) is RadioButton nav) nav.ToolTip = p.Description;
        ApplyMode();
    }

    private TextBlock? _homeModeText;
    private Button? _homeModeButton, _homeHelpButton;

    private Border TaskTile(HomeTask t)
    {
        var accent = (Brush)FindResource(t.Accent);
        var c = ((SolidColorBrush)accent).Color;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(0), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(0x2A, c.R, c.G, c.B)),
            Child = new TextBlock { Text = t.Icon, FontFamily = (FontFamily)FindResource("Icons"), FontSize = 20, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = t.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = t.Description, Foreground = (Brush)FindResource("Muted"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(text, 1);
        grid.Children.Add(icon);
        grid.Children.Add(text);
        var tile = new Border { Style = (Style)FindResource("CardBorder"), Width = 300, MinHeight = 96, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(18, 16, 18, 16),
            Cursor = Cursors.Hand, Child = grid, ToolTip = PageCatalog.ByNav(t.Nav)?.Description };
        tile.MouseEnter += (_, _) => tile.BorderBrush = accent;
        tile.MouseLeave += (_, _) => tile.BorderBrush = (Brush)FindResource("Line");
        tile.MouseLeftButtonUp += (_, _) => GoTo(t.Nav);
        return tile;
    }

    private void ShowHomeResults(string query)
    {
        if (_homeResults == null) return;
        _homeResults.Children.Clear();
        if (query.Trim().Length == 0) return;
        var found = PageCatalog.Search(query, Advanced);
        if (found.Count == 0)
        {
            bool inAdvanced = !Advanced && PageCatalog.Search(query, true).Count > 0;
            _homeResults.Children.Add(new TextBlock { Text = inAdvanced ? "Isso está no modo avançado. Troque de modo (botão no topo) para ver." : "Nada encontrado. Tente outra palavra.",
                Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(4, 4, 0, 0) });
            return;
        }
        foreach (var p in found.Take(8))
        {
            var row = new Border { Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = p.Icon, FontFamily = (FontFamily)FindResource("Icons"), FontSize = 15, Width = 30, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Accent") });
            var tx = new StackPanel();
            tx.Children.Add(new TextBlock { Text = p.Group.Length > 0 ? $"{p.Title}  ·  {FriendlyText.Apply(p.Group)}" : p.Title, FontWeight = FontWeights.SemiBold });
            tx.Children.Add(new TextBlock { Text = p.Description, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(tx);
            row.Child = sp;
            row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("Hover");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) => GoTo(p.Nav);
            _homeResults.Children.Add(row);
        }
    }

    private void GoTo(string nav)
    {
        if (FindName(nav) is RadioButton r) r.IsChecked = true;
    }

    // ---------------- Modo simples / avançado ----------------

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        _settings.AdvancedMode = !_settings.AdvancedMode;
        _settings.Save();
        ApplyMode();
    }

    /// <summary>Deixa o botão de modo na cor do modo atual: azul (ciano) no simples, roxo no avançado,
    /// com realce na mesma cor ao passar o mouse. Lê a cor atual da paleta, então acompanha o tema claro/escuro.</summary>
    private void ApplyModeAccent()
    {
        // Simples: o mesmo azul da temperatura (TempCool), mais azul e menos esverdeado que o ciano. Avançado: o roxo.
        var c = ((SolidColorBrush)FindResource(Advanced ? "Accent" : "TempCool")).Color;
        var border = new SolidColorBrush(c); border.Freeze();
        var soft = new SolidColorBrush(Color.FromArgb(0x33, c.R, c.G, c.B)); soft.Freeze();
        Resources["ModeAccent"] = border;
        Resources["ModeAccentSoft"] = soft;
    }

    private void ApplyMode()
    {
        bool adv = Advanced;
        foreach (var p in PageCatalog.Pages.Where(p => p.Advanced))
            if (FindName(p.Nav) is RadioButton nav) nav.Visibility = adv ? Visibility.Visible : Visibility.Collapsed;
        // Categoria sem nenhuma página visível some junto.
        foreach (var group in new[] { "GrpExplode", "GrpMaint", "GrpTweak", "GrpGames", "GrpPerf", "GrpTools", "GrpMaker" })
            if (FindName(group) is FrameworkElement g && FindName(group + "Items") is Panel items)
            {
                bool any = items.Children.OfType<RadioButton>().Any(r => r.Visibility == Visibility.Visible);
                g.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            }
        FilterBloat();

        ApplyModeAccent();
        BtnMode.Content = adv ? "Modo Avançado" : "Modo Simples";
        if (_homeModeText != null)
            _homeModeText.Text = adv
                ? "Você está no modo avançado: aparecem todas as páginas, incluindo sensores, overclock, stress test, ajustes de hardware, scripts completos e os ajustes avançados de GPU. Use com calma: alguns mexem fundo no PC."
                : "Você está no modo simples: aparece só o essencial e o que é seguro. Sensores detalhados, overclock, stress test, ajustes técnicos e os scripts completos ficam escondidos até você ligar o modo avançado.";
        if (_homeModeButton != null) _homeModeButton.Content = adv ? "Voltar para o modo simples" : "Ligar o modo avançado";

        // Se a página aberta sumiu, volta para o Início.
        var current = PageCatalog.Pages.FirstOrDefault(p => FindName(p.Nav) is RadioButton { IsChecked: true });
        if (current is { Advanced: true } && !adv) NavHome.IsChecked = true;
        UpdatePageHelp(current?.Nav);
        if (_homeHelpButton != null) _homeHelpButton.Visibility = _settings.HidePageHelp && !adv ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------- Explicação no topo da página ----------------

    private void UpdatePageHelp(string? nav)
    {
        var info = nav == null ? null : PageCatalog.ByNav(nav);
        // No modo avançado a faixa some: o subtítulo da página já diz o mesmo. No simples, ajuda quem está começando.
        bool show = info != null && nav != "NavHome" && !_settings.HidePageHelp && !Advanced;
        PageHelp.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) PageHelpText.Text = info!.Description;
    }

    private void PageHelpHide_Click(object sender, RoutedEventArgs e)
    {
        _settings.HidePageHelp = true;
        _settings.Save();
        PageHelp.Visibility = Visibility.Collapsed;
        if (_homeHelpButton != null) _homeHelpButton.Visibility = Visibility.Visible;
    }
}
