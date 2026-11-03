using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ResetExplode;

/// <summary>
/// Aba JOGOS · Promoções: preços das lojas pela API do IsThereAnyDeal. Sub-abas: promoções com filtros, pesquisa com
/// comparação de lojas, jogos grátis e jogos da Biblioteca em promoção. Só lê dados; os links abrem a loja no navegador.
/// </summary>
public partial class MainWindow
{
    private readonly ItadClient _itad = new();
    private bool _dealsInited, _dealsFreeLoaded, _dealsLibLoaded;
    private int _dealsReq, _dealsSearchReq; // descartam respostas velhas quando o filtro/pesquisa muda no meio da consulta
    private int _dealsNextOffset;
    private readonly Dictionary<string, ImageSource?> _dealImgs = new();
    private readonly SemaphoreSlim _dealImgGate = new(6);
    private static readonly HttpClient DealImgHttp = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly string DealsCountry = ItadClient.Country;
    /// <summary>IDs das lojas mais conhecidas no IsThereAnyDeal: Steam, Epic, GOG, Nuuvem, Microsoft, Humble, Fanatical, Green Man Gaming, GamersGate.</summary>
    private static readonly int[] PinnedShops = { 61, 16, 35, 50, 48, 37, 6, 36, 24 };

    /// <summary>Oferta pronta para virar cartão (vem da lista de promoções ou da Biblioteca).</summary>
    private sealed record DealCardData(string GameId, string Title, string Slug, ItadDeal Deal, ItadPrice? HistoryLow,
                                       string? BannerUrl, LibGame? Lib = null);

    private void InitDeals()
    {
        if (_dealsInited) return;
        _dealsInited = true;
        _itad.UserKey = _settings.ItadKey;
        if (_itad.UserKey != null) DealsKeyBox.Password = _itad.UserKey;
        UpdateDealsKeyUi(showIfMissing: true);

        // Preço máximo na moeda do país (R$ no Brasil).
        bool first = true;
        foreach (var max in new double?[] { null, 10, 25, 50, 100, 200 })
        {
            var chip = new RadioButton
            {
                Style = (Style)FindResource("ChipAccent"), GroupName = "DealsPrice", IsChecked = first,
                Content = max is double m ? "Até " + ItadClient.FormatAmount(m, DealsCountry) : "Qualquer",
                Tag = max?.ToString(CultureInfo.InvariantCulture) ?? "",
            };
            chip.Checked += DealsFilter_Changed;
            DealsPricePanel.Children.Add(chip);
            first = false;
        }

        _ = LoadDealShopsAsync();
        if (_itad.Configured) _ = LoadDealsAsync(reset: true);
        else ShowDealsNeedKey(DealsStatus);
    }

    // ---------------- Chave da API ----------------

    private void UpdateDealsKeyUi(bool showIfMissing)
    {
        DealsKeyHint.Visibility = DealsKeyBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        DealsKeyText.Text = !string.IsNullOrWhiteSpace(_itad.UserKey)
            ? "Usando a sua chave (guardada cifrada pelo Windows; só este usuário consegue ler). Para voltar à chave do app, apague e salve vazio."
            : ItadClient.EmbeddedKey != null
                ? "Esta versão já vem com a chave do PcismoMaster: não precisa fazer nada. Se preferir, use a sua própria chave grátis."
                : "Para buscar os preços, o IsThereAnyDeal pede uma chave grátis: crie uma conta, registre um app e cole aqui a API key.";
        if (showIfMissing && !_itad.Configured) DealsKeyCard.Visibility = Visibility.Visible;
    }

    private void ShowDealsNeedKey(TextBlock status)
    {
        status.Text = "Falta a chave da API do IsThereAnyDeal: cole a sua chave grátis no quadro acima e clique em SALVAR CHAVE.";
        DealsKeyCard.Visibility = Visibility.Visible;
    }

    private void BtnDealsKeyToggle_Click(object sender, RoutedEventArgs e) =>
        DealsKeyCard.Visibility = DealsKeyCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void DealsKeyBox_Changed(object sender, RoutedEventArgs e) =>
        DealsKeyHint.Visibility = DealsKeyBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void BtnDealsKeySave_Click(object sender, RoutedEventArgs e)
    {
        _settings.ItadKey = DealsKeyBox.Password;
        try { _settings.Save(); }
        catch (Exception ex) { DealsKeyText.Text = "Não deu para salvar a chave: " + ex.Message; return; }
        _itad.UserKey = _settings.ItadKey;
        _itad.ClearCache();
        _dealsFreeLoaded = _dealsLibLoaded = false;
        UpdateDealsKeyUi(showIfMissing: false);
        if (!_itad.Configured) { ShowDealsNeedKey(DealsStatus); return; }
        DealsKeyCard.Visibility = Visibility.Collapsed;
        if (DealsShopPanel.Children.Count == 0) _ = LoadDealShopsAsync();
        ReloadDealsTab();
    }

    private void DealsGetKey_Click(object sender, RoutedEventArgs e) => OpenDealUrl("https://isthereanydeal.com/apps/");
    private void DealsItad_Click(object sender, RoutedEventArgs e) => OpenDealUrl("https://isthereanydeal.com/");

    // ---------------- Sub-abas ----------------

    private void DealsTab_Checked(object sender, RoutedEventArgs e)
    {
        if (DealsPanelDeals == null) return; // disparado durante o InitializeComponent
        static Visibility Vis(RadioButton tab) => tab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DealsPanelDeals.Visibility = Vis(DealsTabDeals);
        DealsPanelSearch.Visibility = Vis(DealsTabSearch);
        DealsPanelFree.Visibility = Vis(DealsTabFree);
        DealsPanelLib.Visibility = Vis(DealsTabLib);
        if (!_dealsInited) return;
        if (DealsTabFree.IsChecked == true && !_dealsFreeLoaded) _ = LoadFreeAsync();
        else if (DealsTabLib.IsChecked == true && !_dealsLibLoaded) _ = LoadLibDealsAsync();
        else if (DealsTabSearch.IsChecked == true) DealsSearchBox.Focus();
    }

    private void ReloadDealsTab()
    {
        if (DealsTabSearch.IsChecked == true) { if (DealsSearchBox.Text.Trim().Length > 1) _ = SearchDealsAsync(); }
        else if (DealsTabFree.IsChecked == true) _ = LoadFreeAsync();
        else if (DealsTabLib.IsChecked == true) _ = LoadLibDealsAsync();
        else _ = LoadDealsAsync(reset: true);
    }

    // ---------------- Promoções com filtros ----------------

    private async Task LoadDealShopsAsync()
    {
        try
        {
            var shops = await _itad.ShopsAsync(DealsCountry);
            DealsShopPanel.Children.Clear();
            // As lojas mais conhecidas primeiro (a Epic tem poucas promoções e ficaria lá no fim); o resto por número de promoções.
            var ordered = shops.Where(s => s.Deals > 0)
                .OrderBy(s => Array.IndexOf(PinnedShops, s.Id) is int i && i >= 0 ? i : int.MaxValue)
                .ThenByDescending(s => s.Deals).ToList();
            const int visible = 10;
            var extra = new List<ToggleButton>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var s = ordered[i];
                var chip = new ToggleButton { Style = (Style)FindResource("ChipToggle"), Content = s.Title, Tag = s.Id, ToolTip = $"{s.Deals} promoções agora" };
                chip.Checked += DealsFilter_Changed;
                chip.Unchecked += DealsFilter_Changed;
                if (i >= visible) { chip.Visibility = Visibility.Collapsed; extra.Add(chip); }
                DealsShopPanel.Children.Add(chip);
            }
            if (extra.Count > 0)
            {
                var more = new Button { Content = $"+ {extra.Count} lojas", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(12, 4, 12, 4),
                    FontSize = 12, Margin = new Thickness(0, 0, 6, 6) };
                more.Click += (_, _) => { foreach (var c in extra) c.Visibility = Visibility.Visible; more.Visibility = Visibility.Collapsed; };
                DealsShopPanel.Children.Add(more);
            }
        }
        catch (ItadException ex)
        {
            DealsShopPanel.Children.Clear();
            DealsShopPanel.Children.Add(new TextBlock { Text = "Não deu para carregar as lojas: " + ex.Message, Foreground = (Brush)FindResource("Muted"), FontSize = 12 });
        }
    }

    private ItadDealsQuery CurrentDealsQuery() => new()
    {
        Sort = CheckedTag(DealsSortPanel) ?? "-hot",
        CutMin = int.TryParse(CheckedTag(DealsCutPanel), out int cut) ? cut : 0,
        PriceMax = double.TryParse(CheckedTag(DealsPricePanel), NumberStyles.Float, CultureInfo.InvariantCulture, out double max) ? max : null,
        Types = int.TryParse(CheckedTag(DealsTypePanel), out int type) ? new List<int> { type } : new List<int>(),
        Shops = DealsShopPanel.Children.OfType<ToggleButton>().Where(b => b.IsChecked == true && b.Tag is int).Select(b => (int)b.Tag).ToList(),
        HistoricalLowOnly = DealsHlOnly.IsChecked == true,
    };

    private void DealsFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_dealsInited) return; // chips marcados no XAML disparam durante o InitializeComponent
        _ = LoadDealsAsync(reset: true);
    }

    private void BtnDealsRefresh_Click(object sender, RoutedEventArgs e)
    {
        _itad.ClearCache();
        _ = LoadDealsAsync(reset: true);
    }

    private void BtnDealsMore_Click(object sender, RoutedEventArgs e) => _ = LoadDealsAsync(reset: false);

    private async Task LoadDealsAsync(bool reset)
    {
        if (!_itad.Configured) { ShowDealsNeedKey(DealsStatus); return; }
        int req = ++_dealsReq;
        var q = CurrentDealsQuery();
        q.Offset = reset ? 0 : _dealsNextOffset;
        if (reset) { DealsGrid.Children.Clear(); BtnDealsMore.Visibility = Visibility.Collapsed; }
        DealsStatus.Text = reset ? "Buscando promoções…" : "Carregando mais…";
        BtnDealsMore.IsEnabled = false;
        try
        {
            var page = await _itad.DealsAsync(q, DealsCountry);
            if (req != _dealsReq) return;
            foreach (var it in page.List)
                DealsGrid.Children.Add(DealCard(new DealCardData(it.Id, it.Title, it.Slug, it.Deal, it.Deal.HistoryLow, it.Assets?.Banner)));
            _dealsNextOffset = page.NextOffset;
            BtnDealsMore.Visibility = page.HasMore ? Visibility.Visible : Visibility.Collapsed;
            DealsStatus.Text = DealsGrid.Children.Count == 0
                ? "Nenhuma promoção com esses filtros. Tente diminuir o desconto mínimo ou aumentar o preço máximo."
                : $"{DealsGrid.Children.Count} promoções · preços para {RegionName()}. Clique num jogo para abrir a oferta na loja.";
        }
        catch (ItadException ex) { if (req == _dealsReq) DealsStatus.Text = ex.Message; }
        finally { if (req == _dealsReq) BtnDealsMore.IsEnabled = true; }
    }

    private static string RegionName()
    {
        try { return new RegionInfo(DealsCountry).NativeName; } catch { return DealsCountry; }
    }

    /// <summary>Cartão da oferta: banner, desconto, selo de menor preço, preço antigo/atual, loja e prazo. Clicar abre a loja.</summary>
    private FrameworkElement DealCard(DealCardData d)
    {
        const double w = 236, bannerH = 110;
        var deal = d.Deal;
        var banner = new Border
        {
            Height = bannerH, CornerRadius = new CornerRadius(10, 10, 0, 0), Background = (Brush)FindResource("AccentGradient"),
            Child = new TextBlock { Text = Initials(d.Title), FontSize = 32, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent"),
                Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        if (d.BannerUrl == null && d.Lib != null) _ = LoadArtAsync(d.Lib, banner, vertical: false);
        else _ = LoadDealImageAsync(d.BannerUrl, banner, 472);

        var top = new Grid();
        top.Children.Add(banner);
        if (deal.Cut > 0) top.Children.Add(CutBadge(deal.Cut, new Thickness(8), HorizontalAlignment.Left));
        if (FlagText(deal.Flag) is string flag)
            top.Children.Add(new Border
            {
                Background = (Brush)FindResource(deal.Flag == "S" ? "AccentCyan" : "AccentGradient"), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = flag, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent") },
            });

        var info = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };
        info.Children.Add(new TextBlock { Text = d.Title, FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = d.Title });
        info.Children.Add(new TextBlock { Text = deal.Shop.Name.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 2, 0, 0) });

        var prices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        if (deal.Cut > 0)
            prices.Children.Add(new TextBlock { Text = ItadClient.FormatPrice(deal.Regular), TextDecorations = TextDecorations.Strikethrough,
                Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 8, 2) });
        prices.Children.Add(new TextBlock { Text = ItadClient.FormatPrice(deal.Price), FontSize = 17, FontWeight = FontWeights.Bold });
        info.Children.Add(prices);

        var low = d.HistoryLow ?? deal.HistoryLow;
        if (low != null)
            info.Children.Add(new TextBlock { Text = "Menor da história: " + ItadClient.FormatPrice(low), FontSize = 11, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 3, 0, 0) });
        if (ItadClient.FormatExpiry(deal.Expiry, DateTimeOffset.Now) is string exp)
            info.Children.Add(new TextBlock { Text = exp, FontSize = 11, Margin = new Thickness(0, 2, 0, 0),
                Foreground = (Brush)FindResource(deal.Expiry - DateTimeOffset.Now < TimeSpan.FromDays(2) ? "TempWarn" : "Muted") });
        if (!string.IsNullOrWhiteSpace(deal.Voucher))
            info.Children.Add(new TextBlock { Text = "Cupom: " + deal.Voucher, FontSize = 11, Foreground = (Brush)FindResource("AccentCyan"), Margin = new Thickness(0, 2, 0, 0) });

        var compare = new Button { Content = "Comparar lojas", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        compare.Click += (_, _) => _ = CompareStoresAsync(d);
        info.Children.Add(compare);

        var body = new StackPanel();
        body.Children.Add(top);
        body.Children.Add(info);
        var card = new Border
        {
            Width = w, CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("Card"), Margin = new Thickness(0, 0, 14, 14),
            BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1), Child = body, Cursor = Cursors.Hand,
            ToolTip = $"Abrir a oferta na {deal.Shop.Name}",
        };
        card.MouseEnter += (_, _) => card.BorderBrush = (Brush)FindResource("Accent");
        card.MouseLeave += (_, _) => card.BorderBrush = (Brush)FindResource("Line");
        card.MouseLeftButtonUp += (_, _) => OpenDealUrl(deal.Url); // o botão "Comparar lojas" trata o próprio clique
        return card;
    }

    private Border CutBadge(int cut, Thickness margin, HorizontalAlignment align) => new()
    {
        Background = (Brush)FindResource("ChartGpu"), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), Margin = margin,
        HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock { Text = $"-{cut}%", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = (Brush)FindResource("Ink") },
    };

    private static string? FlagText(string? flag) => flag switch
    {
        "N" => "NOVO MENOR PREÇO",
        "H" => "MENOR DA HISTÓRIA",
        "S" => "MENOR NA LOJA",
        _ => null,
    };

    // ---------------- Pesquisar e comparar lojas ----------------

    private void DealsSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; _ = SearchDealsAsync(); }
    }

    private void BtnDealsSearch_Click(object sender, RoutedEventArgs e) => _ = SearchDealsAsync();

    private async Task SearchDealsAsync()
    {
        string q = DealsSearchBox.Text.Trim();
        if (q.Length < 2) { DealsSearchStatus.Text = "Digite pelo menos 2 letras do nome do jogo."; return; }
        if (!_itad.Configured) { ShowDealsNeedKey(DealsSearchStatus); return; }
        int req = ++_dealsSearchReq;
        DealsSearchList.Children.Clear();
        DealsSearchStatus.Text = $"Procurando \"{q}\"…";
        try
        {
            var games = (await _itad.SearchAsync(q, 20)).Where(g => !g.Mature).ToList();
            if (DealsSearchDlc.IsChecked != true) games = games.Where(g => g.Type is null or "game").ToList();
            if (req != _dealsSearchReq) return;
            if (games.Count == 0) { DealsSearchStatus.Text = $"Nenhum jogo encontrado para \"{q}\". Confira o nome (em inglês costuma achar mais)."; return; }
            var prices = (await _itad.PricesAsync(games.Select(g => g.Id), DealsCountry)).ToDictionary(p => p.Id);
            if (req != _dealsSearchReq) return;
            foreach (var g in games)
                DealsSearchList.Children.Add(GamePricesRow(g.Title, g.Slug, g.Type, g.Assets?.Banner, prices.GetValueOrDefault(g.Id)));
            DealsSearchStatus.Text = $"{games.Count} resultado(s) para \"{q}\" · preços para {RegionName()}. Clique numa loja para abrir a oferta.";
        }
        catch (ItadException ex) { if (req == _dealsSearchReq) DealsSearchStatus.Text = ex.Message; }
    }

    /// <summary>"Comparar lojas" de um cartão: abre a Pesquisa já com o preço do jogo em cada loja.</summary>
    private async Task CompareStoresAsync(DealCardData d)
    {
        DealsTabSearch.IsChecked = true;
        DealsSearchBox.Text = d.Title;
        PageDeals.ScrollToTop();
        int req = ++_dealsSearchReq;
        DealsSearchList.Children.Clear();
        DealsSearchStatus.Text = $"Comparando as lojas para {d.Title}…";
        try
        {
            var p = (await _itad.PricesAsync(new[] { d.GameId }, DealsCountry)).FirstOrDefault();
            if (req != _dealsSearchReq) return;
            DealsSearchList.Children.Add(GamePricesRow(d.Title, d.Slug, null, d.BannerUrl, p, d.Lib));
            DealsSearchStatus.Text = $"Preço em cada loja agora · preços para {RegionName()}. Clique numa loja para abrir a oferta.";
        }
        catch (ItadException ex) { if (req == _dealsSearchReq) DealsSearchStatus.Text = ex.Message; }
    }

    /// <summary>Linha da pesquisa: banner, nome, mais barato agora, menor da história e o preço em cada loja (clicável).</summary>
    private FrameworkElement GamePricesRow(string title, string slug, string? type, string? bannerUrl, ItadGamePrices? prices, LibGame? lib = null)
    {
        var banner = new Border
        {
            Width = 184, Height = 86, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("AccentGradient"), VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = Initials(title), FontSize = 26, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent"),
                Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        if (bannerUrl == null && lib != null) _ = LoadArtAsync(lib, banner, vertical: false);
        else _ = LoadDealImageAsync(bannerUrl, banner, 368);

        var info = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        if (type is "dlc" or "package")
            head.Children.Add(new Border { Background = (Brush)FindResource("Hover"), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = type == "dlc" ? "DLC" : "PACOTE", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Muted") } });
        info.Children.Add(head);

        var deals = prices?.Deals.OrderBy(x => x.Price.Amount).ToList() ?? new List<ItadDeal>();
        var low = prices?.HistoryLow?.All;
        string summary = deals.Count == 0 ? "Sem preço nas lojas cobertas agora." : $"Mais barato agora: {ItadClient.FormatPrice(deals[0].Price)} na {deals[0].Shop.Name}";
        if (low != null) summary += $"   ·   Menor da história: {ItadClient.FormatPrice(low)}";
        info.Children.Add(new TextBlock { Text = summary, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 8) });

        foreach (var x in deals.Take(12)) info.Children.Add(StorePriceLine(x, best: x == deals[0]));
        if (deals.Count > 12)
            info.Children.Add(new TextBlock { Text = $"+ {deals.Count - 12} loja(s) mais caras", FontSize = 11, Foreground = (Brush)FindResource("Faint"), Margin = new Thickness(8, 2, 0, 0) });
        if (slug.Length > 0)
        {
            var link = new Hyperlink(new Run("Ver o histórico de preços no IsThereAnyDeal")) { Foreground = (Brush)FindResource("Muted") };
            link.Click += (_, _) => OpenDealUrl($"https://isthereanydeal.com/game/{slug}/info/");
            info.Children.Add(new TextBlock(link) { FontSize = 11, Margin = new Thickness(8, 6, 0, 0) });
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(banner);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        return new Border { Style = (Style)FindResource("CardBorder"), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10), Child = grid };
    }

    /// <summary>Uma loja na comparação: nome, preço antigo, desconto e preço atual. A linha toda abre a oferta.</summary>
    private FrameworkElement StorePriceLine(ItadDeal x, bool best)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var shop = new TextBlock { Text = x.Shop.Name + (FlagText(x.Flag) is string f ? "  ·  " + f.ToLower(CultureInfo.CurrentCulture) : ""),
            FontWeight = best ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        g.Children.Add(shop);
        if (x.Cut > 0)
        {
            var old = new TextBlock { Text = ItadClient.FormatPrice(x.Regular), TextDecorations = TextDecorations.Strikethrough, Foreground = (Brush)FindResource("Muted"),
                FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
            Grid.SetColumn(old, 1); g.Children.Add(old);
            var badge = CutBadge(x.Cut, new Thickness(0), HorizontalAlignment.Right);
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 2); g.Children.Add(badge);
        }
        var price = new TextBlock { Text = ItadClient.FormatPrice(x.Price) + "  ›", FontSize = 13.5, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource(best ? "AccentCyan" : "Fg") };
        Grid.SetColumn(price, 3); g.Children.Add(price);

        var line = new Border { Padding = new Thickness(8, 5, 8, 5), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Child = g,
            Cursor = Cursors.Hand, ToolTip = $"Abrir na {x.Shop.Name}" };
        line.MouseEnter += (_, _) => line.Background = (Brush)FindResource("Hover");
        line.MouseLeave += (_, _) => line.Background = Brushes.Transparent;
        line.MouseLeftButtonUp += (_, _) => OpenDealUrl(x.Url);
        return line;
    }

    // ---------------- Grátis ----------------

    private async Task LoadFreeAsync()
    {
        if (!_itad.Configured) { ShowDealsNeedKey(DealsFreeStatus); return; }
        DealsFreeList.Children.Clear();
        DealsFreeStatus.Text = "Buscando jogos grátis…";
        try
        {
            var now = DateTimeOffset.Now;
            var list = (await _itad.GiveawaysAsync()).Where(g => g.Expiry == null || g.Expiry > now).ToList();
            DealsFreeList.Children.Clear();
            foreach (var gw in list) DealsFreeList.Children.Add(GiveawayRow(gw, now));
            _dealsFreeLoaded = true;
            DealsFreeStatus.Text = list.Count == 0
                ? "Nenhum jogo grátis no momento. Volte mais tarde (a Epic costuma liberar toda quinta)."
                : $"{list.Count} jogo(s) grátis por tempo limitado. Resgate na loja enquanto valer e ele fica seu pra sempre.";
        }
        catch (ItadException ex) { DealsFreeStatus.Text = ex.Message; }
    }

    private FrameworkElement GiveawayRow(ItadGiveaway gw, DateTimeOffset now)
    {
        string title = gw.Games.Count == 1 ? gw.Games[0].Title : gw.Title;
        var banner = new Border
        {
            Width = 184, Height = 86, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("AccentGradient"),
            Child = new TextBlock { Text = Initials(title), FontSize = 26, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OnAccent"),
                Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        _ = LoadDealImageAsync(gw.Games.FirstOrDefault(g => g.Assets?.Banner != null)?.Assets?.Banner, banner, 368);

        var info = new StackPanel { Margin = new Thickness(16, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = gw.Title });
        if (gw.Shop != null)
            info.Children.Add(new TextBlock { Text = gw.Shop.Name.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 3, 0, 3) });
        string exp = ItadClient.FormatExpiry(gw.Expiry, now) ?? "Sem data para acabar";
        info.Children.Add(new TextBlock { Text = exp, FontSize = 11.5,
            Foreground = (Brush)FindResource(gw.Expiry - now < TimeSpan.FromDays(2) ? "TempWarn" : "Muted") });
        if (!string.IsNullOrWhiteSpace(gw.Note))
            info.Children.Add(new TextBlock { Text = gw.Note, FontSize = 11, Foreground = (Brush)FindResource("Faint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });

        var get = new Button { Content = "PEGAR GRÁTIS", Style = (Style)FindResource("PrimaryButton"), Padding = new Thickness(16, 8, 16, 8), VerticalAlignment = VerticalAlignment.Center };
        get.Click += (_, _) => OpenDealUrl(gw.Url);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(banner);
        Grid.SetColumn(info, 1); grid.Children.Add(info);
        Grid.SetColumn(get, 2); grid.Children.Add(get);
        return new Border { Style = (Style)FindResource("CardBorder"), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10), Child = grid };
    }

    // ---------------- Da minha Biblioteca ----------------

    private void BtnDealsLibRefresh_Click(object sender, RoutedEventArgs e) => _ = LoadLibDealsAsync();

    /// <summary>Acha os jogos instalados no IsThereAnyDeal (Steam pelo appid, os outros pelo nome) e mostra os que estão em promoção.</summary>
    private async Task LoadLibDealsAsync()
    {
        if (!_itad.Configured) { ShowDealsNeedKey(DealsLibStatus); return; }
        BtnDealsLibRefresh.IsEnabled = false;
        DealsLibGrid.Children.Clear();
        try
        {
            if (_libGames.Count == 0)
            {
                DealsLibStatus.Text = "Procurando os jogos instalados…";
                var custom = _settings.CustomGames.ToList();
                _libGames = await Task.Run(() => GameLibrary.Scan(custom));
            }
            var games = _libGames.ToList();
            if (games.Count == 0) { DealsLibStatus.Text = "Nenhum jogo instalado encontrado (veja a aba Biblioteca)."; return; }

            DealsLibStatus.Text = $"Procurando {games.Count} jogo(s) nas lojas…";
            var idOf = new Dictionary<LibGame, string>();
            var steam = games.Where(g => g.IsSteam).ToList();
            var others = games.Where(g => !g.IsSteam).ToList();
            if (steam.Count > 0)
            {
                var map = await _itad.LookupSteamAsync(steam.Select(g => g.Id));
                foreach (var g in steam) if (map.TryGetValue(g.Id, out var gid)) idOf[g] = gid;
            }
            if (others.Count > 0)
            {
                var map = await _itad.LookupTitlesAsync(others.Select(g => g.Name));
                foreach (var g in others) if (map.TryGetValue(g.Name, out var gid)) idOf[g] = gid;
            }
            if (idOf.Count == 0) { DealsLibStatus.Text = "Nenhum dos seus jogos foi encontrado nas lojas cobertas."; return; }

            var prices = (await _itad.PricesAsync(idOf.Values, DealsCountry, dealsOnly: true)).ToDictionary(p => p.Id);
            var cards = new List<DealCardData>();
            foreach (var (g, gid) in idOf.DistinctBy(kv => kv.Value))
                if (prices.TryGetValue(gid, out var p) && p.Deals.Count > 0)
                    cards.Add(new DealCardData(gid, g.Name, "", p.Deals.MinBy(x => x.Price.Amount)!, p.HistoryLow?.All, null, g));
            foreach (var c in cards.OrderByDescending(c => c.Deal.Cut)) DealsLibGrid.Children.Add(DealCard(c));
            _dealsLibLoaded = true;
            DealsLibStatus.Text = cards.Count == 0
                ? $"Nenhum dos seus {idOf.Count} jogo(s) encontrados está em promoção agora."
                : $"{cards.Count} dos seus jogos estão em promoção agora · preços para {RegionName()}.";
        }
        catch (ItadException ex) { DealsLibStatus.Text = ex.Message; }
        catch (Exception ex) { DealsLibStatus.Text = "Falhou: " + ex.Message; }
        finally { BtnDealsLibRefresh.IsEnabled = true; }
    }

    // ---------------- Imagens e links ----------------

    /// <summary>Baixa o banner do jogo (do servidor de imagens do IsThereAnyDeal) já reduzido, com no máximo 6 downloads ao mesmo tempo.</summary>
    private async Task LoadDealImageAsync(string? url, Border target, int decodeWidth)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        if (!_dealImgs.TryGetValue(url, out var img))
        {
            await _dealImgGate.WaitAsync();
            try
            {
                if (!_dealImgs.TryGetValue(url, out img))
                {
                    byte[]? bytes = null;
                    try { bytes = await DealImgHttp.GetByteArrayAsync(url); } catch { }
                    img = bytes == null ? null : await Task.Run<ImageSource?>(() =>
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new MemoryStream(bytes);
                            bmp.DecodePixelWidth = decodeWidth;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                        catch { return null; }
                    });
                    if (_dealImgs.Count > 400) _dealImgs.Clear(); // memória sob controle em sessões longas
                    _dealImgs[url] = img;
                }
            }
            finally { _dealImgGate.Release(); }
        }
        if (img == null) return;
        var brush = new ImageBrush(img) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Center };
        brush.Freeze();
        target.Background = brush;
        target.Child = null;
    }

    /// <summary>Abre o link no navegador padrão (só http/https; os links de loja vêm como a API entrega).</summary>
    private void OpenDealUrl(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Não deu para abrir o navegador: " + ex.Message, "Promoções", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
