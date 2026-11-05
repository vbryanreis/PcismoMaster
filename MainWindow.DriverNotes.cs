using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Aba Drivers, topo: versão instalada x mais recente do driver de vídeo e o que mudou em cada versão nova
/// (jogos com suporte Game Ready, tecnologias, correções), das notas oficiais que a NVIDIA devolve na consulta.
/// </summary>
public partial class MainWindow
{
    private bool _drvShowOlder;

    private GpuDriverStatus? MainDriverGpu =>
        _drvGpus.FirstOrDefault(g => g.Vendor == "NVIDIA") ?? _drvGpus.FirstOrDefault(g => g.Vendor == "AMD") ?? _drvGpus.FirstOrDefault();

    private void ShowDriverVersion()
    {
        var g = MainDriverGpu;
        DrvNotesHost.Children.Clear();
        if (g == null)
        {
            DrvVerInstalled.Text = DrvVerLatest.Text = "—";
            DrvVerGpu.Text = "Placa de vídeo não encontrada";
            DrvVerDate.Text = "";
            DrvVerMessage.Text = "Escolha o fabricante abaixo para baixar o driver.";
            DrvVerBadge.Visibility = BtnDrvOlder.Visibility = BtnDrvNotesPage.Visibility = Visibility.Collapsed;
            return;
        }
        DrvVerInstalled.Text = g.NoDriver ? "nenhum" : g.Installed.Length > 0 ? g.Installed : "—";
        DrvVerGpu.Text = g.GpuName;
        DrvVerLatest.Text = g.Latest.Length > 0 ? g.Latest : "—";
        var latest = g.Releases.FirstOrDefault();
        DrvVerDate.Text = latest?.DateText is { Length: > 0 } d ? $"lançado em {d}" : "";
        DrvVerMessage.Text = g.Message;

        bool upToDate = g.Vendor == "NVIDIA" && g.Latest.Length > 0 && !g.UpdateAvailable;
        DrvVerBadge.Visibility = g.Latest.Length > 0 && g.Vendor == "NVIDIA" ? Visibility.Visible : Visibility.Collapsed;
        DrvVerBadge.Background = (Brush)FindResource(upToDate ? "AccentSoft" : "TempWarn");
        DrvVerBadgeText.Foreground = (Brush)FindResource(upToDate ? "Fg" : "Ink");
        int behind = g.NewerReleases.Count;
        DrvVerBadgeText.Text = upToDate ? "✔ Atualizado" : behind > 1 ? $"{behind} versões atrás" : "Atualização disponível";

        BtnDrvNotesPage.Visibility = latest?.DetailsUrl.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RenderDriverNotes(g);
    }

    private void RenderDriverNotes(GpuDriverStatus g)
    {
        DrvNotesHost.Children.Clear();
        if (g.Releases.Count == 0) { BtnDrvOlder.Visibility = Visibility.Collapsed; return; }
        var newer = g.NewerReleases;
        var older = g.Releases.Except(newer).ToList();

        if (newer.Count > 0)
        {
            DrvNotesHost.Children.Add(SectionLabel(g.NoDriver ? "O QUE VEM NO DRIVER MAIS RECENTE"
                : $"O QUE MUDOU DESDE O SEU DRIVER ({g.Installed}) · {newer.Count} VERSÃO(ÕES)"));
            foreach (var r in newer) DrvNotesHost.Children.Add(ReleaseCard(r, highlight: true));
        }
        else
        {
            DrvNotesHost.Children.Add(SectionLabel("NOVIDADES DA VERSÃO QUE VOCÊ TEM"));
            var current = g.Releases.FirstOrDefault(r => GpuDriverStatus.CompareVersions(r.Version, g.Installed) == 0) ?? g.Releases[0];
            DrvNotesHost.Children.Add(ReleaseCard(current, highlight: false));
            older.Remove(current);
        }

        BtnDrvOlder.Visibility = older.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BtnDrvOlder.Content = _drvShowOlder ? "Esconder versões anteriores" : $"Ver versões anteriores ({older.Count})";
        if (_drvShowOlder && older.Count > 0)
        {
            DrvNotesHost.Children.Add(SectionLabel("VERSÕES ANTERIORES"));
            foreach (var r in older) DrvNotesHost.Children.Add(ReleaseCard(r, highlight: false));
        }
        DrvNotesHost.Children.Add(new TextBlock { Text = "Itens das notas no original em inglês, como a NVIDIA publica.", Foreground = (Brush)FindResource("Faint"),
                                                  FontSize = 11, Margin = new Thickness(0, 6, 0, 0) });
    }

    private TextBlock SectionLabel(string text) =>
        new() { Text = text, Style = (Style)FindResource("Label"), Margin = new Thickness(0, 12, 0, 6) };

    /// <summary>Um driver: versão, data, o título (jogos Game Ready) e as partes (novidades, correções em jogos, gerais…).</summary>
    private Border ReleaseCard(NvRelease r, bool highlight)
    {
        var body = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var date = new TextBlock { Text = r.DateText, Foreground = (Brush)FindResource("Faint"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(date, Dock.Right);
        head.Children.Add(date);
        head.Children.Add(new TextBlock { Text = r.Version, FontFamily = (FontFamily)FindResource("Mono"), FontSize = 15, FontWeight = FontWeights.SemiBold,
                                          Foreground = (Brush)FindResource(highlight ? "Accent" : "Fg"), VerticalAlignment = VerticalAlignment.Center });
        body.Children.Add(head);
        if (r.Title.Length > 0)
            body.Children.Add(new TextBlock { Text = r.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });

        foreach (var s in r.Sections)
        {
            bool isTitle = s.Heading == r.Title;
            if (!isTitle)
                body.Children.Add(new TextBlock { Text = s.Heading, Foreground = (Brush)FindResource(s.Heading.StartsWith("Problemas") ? "TempWarn" : "AccentCyan"),
                                                  FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
            if (s.Text.Length > 0)
                body.Children.Add(new TextBlock { Text = s.Text, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            foreach (var item in s.Items)
                body.Children.Add(new TextBlock { Text = "•  " + item, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 1, 0, 1) });
        }
        if (r.ArticleUrl.Length > 0)
        {
            var link = new Button { Style = (Style)FindResource("OutlineButton"), Content = "Ler o artigo da NVIDIA", Padding = new Thickness(10, 4, 10, 4),
                                    FontSize = 11.5, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            link.Click += (_, _) => OpenUrl(r.ArticleUrl);
            body.Children.Add(link);
        }
        return new Border
        {
            BorderBrush = (Brush)FindResource(highlight ? "LineStrong" : "Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 12), Margin = new Thickness(0, 0, 0, 8), Child = body,
        };
    }

    private void BtnDrvOlder_Click(object sender, RoutedEventArgs e)
    {
        _drvShowOlder = !_drvShowOlder;
        if (MainDriverGpu is GpuDriverStatus g) RenderDriverNotes(g);
    }

    private void BtnDrvNotesPage_Click(object sender, RoutedEventArgs e)
    {
        if (MainDriverGpu?.Releases.FirstOrDefault() is NvRelease r && r.DetailsUrl.Length > 0) OpenUrl(r.DetailsUrl);
    }

    /// <summary>Game Ready x Studio: consulta de novo só a NVIDIA (as notas e a versão mudam).</summary>
    private async void DrvStudio_Changed(object sender, RoutedEventArgs e)
    {
        if (!_drvInit || _drvBusy || _drvGpus.FirstOrDefault(x => x.Vendor == "NVIDIA") is not GpuDriverStatus g) return;
        DrvBusy(true);
        try
        {
            DrvSet("Consultando a NVIDIA…", 0.5);
            await DriverManager.CheckNvidiaAsync(g, DrvStudio.IsChecked == true, CancellationToken.None);
            ShowDriverVersion();
            DrvSet($"Pronto às {DateTime.Now:HH:mm:ss}.", 1);
        }
        finally { DrvBusy(false); }
    }
}
