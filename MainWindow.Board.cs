using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>Aba Drivers · Placa-mãe: BIOS instalada x mais recente e todos os drivers do modelo no suporte oficial.</summary>
public partial class MainWindow
{
    private List<DriverRow> _drvInventory = new();
    private BoardCheck? _board;

    private async Task CheckBoardAsync(CancellationToken ct)
    {
        BoardMessage.Text = "Consultando o suporte do fabricante…";
        BoardRows.Children.Clear();
        try
        {
            var info = _drvBoardInfo;
            var inv = _drvInventory;
            _board = await Task.Run(() => BoardSupport.CheckAsync(info, inv, ct), ct);
            ShowBoardCheck();
        }
        catch (OperationCanceledException) { BoardMessage.Text = ""; }
        catch (Exception ex) { BoardMessage.Text = "Não foi possível conferir a placa-mãe: " + ex.Message; }
    }

    private void ShowBoardCheck()
    {
        var c = _board;
        if (c == null) return;
        var b = _drvBoardInfo;
        string installedBios = BoardSupport.BiosVersion(b.Bios);
        BtnBoardBiosPage.IsEnabled = c.BiosPage.Length > 0;
        BtnBoardDrivers.IsEnabled = c.DriversPage.Length > 0;

        if (c.LatestBios is BoardFile bios)
        {
            BoardBiosLine.Text = $"Instalada: {(b.Bios.Length > 0 ? b.Bios : "—")}   ·   Mais recente no site: {bios.Version}" +
                                 (bios.DateText.Length > 0 ? $" ({bios.DateText})" : "");
            BoardBiosLine.Foreground = (Brush)FindResource(c.BiosUpToDate == false ? "TempWarn" : "Fg");
            BoardBiosHint.Text = c.BiosUpToDate switch
            {
                true => "✔ Você já está na BIOS mais recente.",
                false => $"Há uma BIOS mais nova ({bios.Version}). Leia as notas da versão no site antes de atualizar; não desligue o PC durante a gravação.",
                _ => "Compare as duas versões acima (o formato da versão instalada é diferente do site).",
            };
            BtnBoardBiosGet.Visibility = c.BiosUpToDate == true ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            BoardBiosLine.Text = $"Instalada: {(b.Bios.Length > 0 ? b.Bios : "—")}";
            BoardBiosLine.Foreground = (Brush)FindResource("Fg");
            BoardBiosHint.Text = c.BiosPage.Length > 0
                ? $"Para ver se há uma mais nova, abra a página de BIOS do modelo e compare com a versão {(installedBios.Length > 0 ? installedBios : "instalada")}."
                : "Fabricante não reconhecido: procure o modelo acima no site do fabricante.";
            BtnBoardBiosGet.Visibility = Visibility.Collapsed;
        }

        BoardMessage.Text = c.Message;
        int newer = c.Rows.Count(r => r.Status == BoardStatus.Newer), missing = c.Rows.Count(r => r.Status == BoardStatus.NotInstalled);
        BoardSummary.Visibility = c.Online || c.AmdChipset != null ? Visibility.Visible : Visibility.Collapsed;
        BoardSummary.Text = newer == 0 && missing == 0 ? "✔ Todos os drivers da placa estão em dia."
                          : $"{newer} com versão mais nova no site" + (missing > 0 ? $" · {missing} não instalado(s)" : "") + ".";
        BoardSummary.Foreground = (Brush)FindResource(newer + missing == 0 ? "ChartGpu" : "TempWarn");

        ShowBoardTiles();
        BoardRows.Children.Clear();
        foreach (var group in c.Rows.GroupBy(r => r.Category))
        {
            BoardRows.Children.Add(new TextBlock { Text = group.Key.ToUpperInvariant(), Style = (Style)FindResource("Label"), Margin = new Thickness(0, 10, 0, 4) });
            foreach (var r in group) BoardRows.Children.Add(BoardRow(r));
        }
    }

    private FrameworkElement BoardRow(BoardDriverRow r)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Margin = new Thickness(0, 6, 12, 6) };
        left.Children.Add(new TextBlock { Text = r.Title, TextWrapping = TextWrapping.Wrap });
        string detail = r.Status == BoardStatus.LocalOnly
            ? $"Instalado: {r.Installed}" + (r.InstalledDate is { Length: > 0 } and not "—" ? $" de {r.InstalledDate}" : "")
            : (r.Installed.Length > 0 ? $"Instalado: {r.Installed}" + (r.Device.Length > 0 && r.Device != r.Title ? $" ({r.Device})" : "") : "Não encontrado neste PC") +
              $"   ·   Site: {r.SiteVersion}" + (r.SiteDate.Length > 0 ? $" de {r.SiteDate}" : "");
        left.Children.Add(new TextBlock { Text = detail, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        grid.Children.Add(left);

        var (label, brush) = r.Status switch
        {
            BoardStatus.UpToDate => ("✔ Atualizado", "ChartGpu"),
            BoardStatus.Newer => ("Mais novo no site", "TempWarn"),
            BoardStatus.NotInstalled => ("Não instalado", "TempWarn"),
            BoardStatus.Optional => ("Opcional", "Faint"),
            BoardStatus.Check => ("Compare as versões", "Muted"),
            _ => ("Confira no site", "Muted"),
        };
        var status = new TextBlock { Text = label, Foreground = (Brush)FindResource(brush), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(status, 1);
        grid.Children.Add(status);

        if (r.Url.Length > 0 && r.Status != BoardStatus.UpToDate)
        {
            var btn = new Button { Style = (Style)FindResource(r.Status == BoardStatus.Newer ? "PrimaryButton" : "OutlineButton"), Content = "Baixar",
                                   Padding = new Thickness(12, 4, 12, 4), FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                                   ToolTip = "Baixa do site oficial do fabricante (abre no navegador)" };
            btn.Click += (_, _) => OpenUrl(r.Url);
            Grid.SetColumn(btn, 2);
            grid.Children.Add(btn);
        }
        return new Border { BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Child = grid };
    }

    private void BtnBoardBiosGet_Click(object sender, RoutedEventArgs e)
    {
        if (_board?.LatestBios is not BoardFile bios) return;
        if (MessageBox.Show(this, $"Baixar a BIOS {bios.Version} do site oficial?\n\nO app não grava a BIOS: depois de baixar, siga o manual da placa " +
                "(normalmente pelo utilitário de atualização dentro da própria BIOS, com o arquivo num pendrive). Não desligue o PC durante a gravação.",
                "BIOS", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            OpenUrl(bios.Url);
    }

    private void BtnBoardBiosPage_Click(object sender, RoutedEventArgs e) { if (_board?.BiosPage.Length > 0) OpenUrl(_board.BiosPage); }
    private void BtnBoardDrivers_Click(object sender, RoutedEventArgs e) { if (_board?.DriversPage.Length > 0) OpenUrl(_board.DriversPage); }

    private async void BtnBoardCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_drvBusy) return;
        DrvBusy(true);
        try
        {
            if (_drvInventory.Count == 0) _drvInventory = await Task.Run(DriverManager.Inventory);
            _drvCts = new CancellationTokenSource();
            await CheckBoardAsync(_drvCts.Token);
        }
        finally { DrvBusy(false); }
    }
}
