using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Aba Drivers: a parte visual — logos das marcas (desenhados aqui, sem imagens baixadas), o resumo do topo
/// (placa de vídeo, placa-mãe/BIOS e chipset), os botões de fabricante e as ferramentas NVIDIA (Painel e Profile Inspector).
/// </summary>
public partial class MainWindow
{
    private bool _drvLookInit;

    private void InitDriverLook()
    {
        if (_drvLookInit) return;
        _drvLookInit = true;
        DrvRtLogo.Content = BrandLogo("Microsoft", 44);
        DrvWuLogo.Content = BrandLogo("Microsoft", 44);
        DrvGpuLogo.Content = BrandLogo("", 44);
        DrvBoardLogo.Content = BrandLogo("", 44);
        DrvTileGpuLogo.Content = BrandLogo("", 40);
        DrvTileBoardLogo.Content = BrandLogo("", 40);
        DrvTileChipLogo.Content = BrandLogo(CpuVendor(), 40);
        DrvTileChipName.Text = CpuVendor() == "AMD" ? "AMD Chipset Software" : "Intel Chipset Device Software";
        foreach (var v in new[] { "NVIDIA", "AMD", "Intel" })
        {
            var b = new Button { Style = (Style)FindResource("OutlineButton"), Tag = v, Padding = new Thickness(6, 5, 12, 5), Margin = new Thickness(0, 0, 8, 6),
                                 ToolTip = v switch
                                 {
                                     "AMD" => "Procura o instalador na página oficial do seu modelo (RX 4000 a 9000) e confere a assinatura antes de abrir",
                                     "Intel" => "Abre a lista de drivers gráficos da Intel para Windows 11",
                                     _ => "Baixa o driver mais recente do servidor oficial e confere a assinatura",
                                 } };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(BrandLogo(v, 22));
            sp.Children.Add(new TextBlock { Text = v, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            b.Content = sp;
            b.Click += BtnDrvVendor_Click;
            DrvVendorRow.Children.Add(b);
        }
        UpdateNvToolsInfo();
    }

    private static string CpuVendor()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (k?.GetValue("VendorIdentifier") as string ?? "").Contains("AMD", StringComparison.OrdinalIgnoreCase) ? "AMD" : "Intel";
        }
        catch { return "Intel"; }
    }

    /// <summary>Logotipo simplificado da marca: as cores e o nome, num selo arredondado do tamanho pedido.</summary>
    private FrameworkElement BrandLogo(string brand, double size)
    {
        string b = brand.ToUpperInvariant();
        if (b is "MICROSOFT")
        {
            var g = new UniformGrid { Rows = 2, Columns = 2, Width = size * 0.56, Height = size * 0.56 };
            foreach (var c in new[] { "#F25022", "#7FBA00", "#00A4EF", "#FFB900" })
                g.Children.Add(new Border { Background = Hex(c), Margin = new Thickness(size * 0.02) });
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.26), Background = Hex("#1B1B22"), Child = g };
        }
        (string bg, string fg, string? stripe, string text) = b switch
        {
            "NVIDIA" => ("#76B900", "#FFFFFF", null, "NVIDIA"),
            "AMD" => ("#000000", "#FFFFFF", "#ED1C24", "AMD"),
            "INTEL" => ("#0068B5", "#FFFFFF", null, "intel"),
            "ASUS" => ("#000000", "#FFFFFF", "#00A3E0", "ASUS"),
            "GIGABYTE" => ("#0E0E0E", "#FFFFFF", "#FF7A00", "GIGABYTE"),
            "MSI" => ("#000000", "#FFFFFF", "#E60012", "msi"),
            "ASROCK" => ("#0B2C5F", "#FFFFFF", null, "ASRock"),
            _ => ("", "", null, ""),
        };
        if (text.Length == 0)
        {
            return new Border
            {
                Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.26), Background = (Brush)FindResource("AccentGradient"),
                Child = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = size * 0.4, Foreground = (Brush)FindResource("OnAccent"),
                                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
        }
        double font = size * (text.Length > 5 ? 0.22 : 0.3);
        return new Border
        {
            Height = size, MinWidth = size, CornerRadius = new CornerRadius(size * 0.26), Background = Hex(bg), Padding = new Thickness(size * 0.18, 0, size * 0.18, 0),
            BorderBrush = stripe != null ? Hex(stripe) : null, BorderThickness = stripe != null ? new Thickness(0, 0, 0, Math.Max(2, size * 0.07)) : new Thickness(0),
            Child = new TextBlock { Text = text, Foreground = Hex(fg), FontWeight = FontWeights.Black, FontSize = font, HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center },
        };
    }

    private static SolidColorBrush Hex(string c)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c));
        b.Freeze();
        return b;
    }

    /// <summary>Topo e cartão da placa de vídeo, depois da consulta do driver.</summary>
    private void ShowGpuTile()
    {
        var g = MainDriverGpu;
        string vendor = g?.Vendor ?? "";
        DrvGpuLogo.Content = BrandLogo(vendor, 44);
        DrvTileGpuLogo.Content = BrandLogo(vendor, 40);
        DrvTileGpuName.Text = g?.GpuName ?? "Não encontrada";
        if (g == null) { SetTile(DrvTileGpuState, "Escolha o fabricante no cartão abaixo", "Muted"); return; }
        if (g.NoDriver) SetTile(DrvTileGpuState, "Sem driver instalado", "TempWarn");
        else if (vendor == "NVIDIA" && g.Latest.Length > 0)
            SetTile(DrvTileGpuState, g.UpdateAvailable ? $"{g.Installed} → {g.Latest} disponível" : $"✔ {g.Installed}, o mais recente", g.UpdateAvailable ? "TempWarn" : "ChartGpu");
        else SetTile(DrvTileGpuState, $"Instalado: {g.Installed}", "Muted");
    }

    /// <summary>Topo (placa-mãe e chipset) e os quadros de BIOS e chipset do cartão da placa-mãe.</summary>
    private void ShowBoardTiles()
    {
        var b = _drvBoardInfo;
        DrvBoardLogo.Content = BrandLogo(b.Brand, 44);
        DrvTileBoardLogo.Content = BrandLogo(b.Brand, 40);
        DrvTileBoardName.Text = b.Model.Length > 0 ? b.Model : "Modelo não reconhecido";
        var c = _board;
        if (c?.LatestBios is BoardFile bios)
            SetTile(DrvTileBoardState, c.BiosUpToDate switch
            {
                true => $"✔ BIOS {BoardSupport.BiosVersion(b.Bios)}, a mais recente",
                false => $"BIOS {BoardSupport.BiosVersion(b.Bios)} → {bios.Version} disponível",
                _ => $"BIOS {BoardSupport.BiosVersion(b.Bios)} · site: {bios.Version}",
            }, c.BiosUpToDate == false ? "TempWarn" : c.BiosUpToDate == true ? "ChartGpu" : "Muted");
        else SetTile(DrvTileBoardState, b.Bios.Length > 0 ? $"BIOS {b.Bios} · confira no site" : "BIOS não lida", "Muted");

        var chip = c?.Rows.FirstOrDefault(r => r.Category == "Chipset");
        BtnBoardChipPage.IsEnabled = (c?.ChipsetPage.Length ?? 0) > 0;
        BtnBoardChipPage.Content = CpuVendor() == "AMD" ? "Página do chipset na AMD" : "Página do chipset";
        if (c?.AmdChipset is AmdChipsetInfo amd)
        {
            string inst = chip?.Installed is { Length: > 0 } i ? i : "não instalado";
            BoardChipLine.Text = $"Instalado: {inst}   ·   AMD: {amd.Version}" + (amd.DateText.Length > 0 ? $" ({amd.DateText})" : "");
            (string hint, string tile, string brush) = chip?.Status switch
            {
                BoardStatus.UpToDate => ("✔ Você já tem o pacote de chipset mais recente da AMD.", $"✔ {inst}, o mais recente", "ChartGpu"),
                BoardStatus.Newer => ($"A AMD já publicou a versão {amd.Version}. Baixe na página da AMD e instale; reinicie depois.", $"{inst} → {amd.Version} disponível", "TempWarn"),
                BoardStatus.NotInstalled => ("O pacote de chipset da AMD não está instalado: ele traz os planos de energia e drivers que o Windows não tem.", "Não instalado", "TempWarn"),
                _ => ("Compare as duas versões acima.", $"{inst} · AMD: {amd.Version}", "Muted"),
            };
            BoardChipHint.Text = hint;
            BoardChipLine.Foreground = (Brush)FindResource(brush == "ChartGpu" ? "Fg" : brush);
            SetTile(DrvTileChipState, tile, brush);
        }
        else
        {
            // Sem o pacote em "Aplicativos instalados", a versão vem do driver dos dispositivos de chipset (não é a do pacote).
            bool fromDevices = chip?.Device.StartsWith("Drivers de chipset", StringComparison.Ordinal) == true;
            BoardChipLine.Text = chip?.Installed is { Length: > 0 } i
                ? $"Instalado: {i}" + (fromDevices ? " (versão do driver, não a do pacote da Intel)" : "")
                : "Pacote de chipset não encontrado";
            BoardChipLine.Foreground = (Brush)FindResource("Fg");
            BoardChipHint.Text = c?.ChipsetMessage is { Length: > 0 } m ? m
                : CpuVendor() == "AMD" ? "Não deu para ler a versão no site da AMD agora; abra a página do chipset para comparar."
                : "A Intel não tem consulta pública de versões: compare na página de drivers do modelo.";
            SetTile(DrvTileChipState, chip?.Installed is { Length: > 0 } j ? $"Instalado: {j}" : "Não encontrado", "Muted");
        }
    }

    private void SetTile(TextBlock t, string text, string brush)
    {
        t.Text = text;
        t.Foreground = (Brush)FindResource(brush);
    }

    private void BtnBoardChipPage_Click(object sender, RoutedEventArgs e) { if (_board?.ChipsetPage.Length > 0) OpenUrl(_board.ChipsetPage); }

    // ------------------------------------------------------------------ ferramentas NVIDIA

    private void UpdateNvToolsInfo()
    {
        string npi = NvidiaTools.HasProfileInspector ? $"Profile Inspector {NvidiaTools.ProfileInspectorVersion} baixado" : "Profile Inspector ainda não baixado";
        NvToolsInfo.Text = "Toda vez que o driver NVIDIA é instalado por esta página, o Painel de Controle NVIDIA (Microsoft Store) é instalado se faltar e o " +
                           $"Profile Inspector mais novo é baixado do GitHub oficial (Orbmu2k), com atalhos na Área de Trabalho. Agora: {npi}.";
        BtnNpiOpen.IsEnabled = NvidiaTools.HasProfileInspector;
    }

    private void BtnNpiOpen_Click(object sender, RoutedEventArgs e)
    {
        try { NvidiaTools.OpenProfileInspector(); }
        catch (Exception ex) { GfxStatus.Text = "Não foi possível abrir o Profile Inspector: " + ex.Message; }
    }

    private async void BtnNvTools_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin()) return;
        _gfxBusy = true;
        ((Button)sender).IsEnabled = false;
        GfxStatus.Text = "Instalando o Painel de Controle NVIDIA e baixando o Profile Inspector mais recente…";
        try { GfxStatus.Text = await NvidiaTools.AfterDriverInstallAsync(CancellationToken.None); }
        finally { _gfxBusy = false; ((Button)sender).IsEnabled = true; UpdateNvToolsInfo(); }
    }
}
