using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>FERRAMENTAS · Drivers: instalação enxuta do driver (componentes escolhidos) e limpezas com desfazer.</summary>
public partial class MainWindow
{
    private readonly Dictionary<string, CheckBox> _debloatToggles = new();
    private List<string> _debloatVendors = new();

    /// <summary>Monta as opções dos fabricantes presentes neste PC (chamado pelo cartão de configurações, que já leu as placas).</summary>
    private async void InitDebloat(List<string> vendors)
    {
        _debloatVendors = vendors;
        DebloatRows.Children.Clear();
        _debloatToggles.Clear();
        var items = DriverDebloat.Nvidia.Concat(DriverDebloat.Amd).Concat(DriverDebloat.Intel).Where(i => vendors.Contains(i.Vendor))
            .Select(i => GpuRules.ForCard(i, _gfxCard)).ToList();
        var (applied, has7z) = await Task.Run(() => (items.Where(i => i.Vendor != "nvidia").ToDictionary(i => i.Id, DriverDebloat.IsApplied), DriverDebloat.SevenZip() != null));

        foreach (var vendor in new[] { "nvidia", "amd", "intel" }.Where(vendors.Contains))
        {
            DebloatRows.Children.Add(new TextBlock { Text = vendor == "nvidia" ? "NVIDIA · TIRAR DO PACOTE" : vendor.ToUpperInvariant() + " · LIMPAR DEPOIS DE INSTALAR",
                Style = (Style)FindResource("Label"), Margin = new Thickness(0, 10, 0, 8) });
            foreach (var i in items.Where(i => i.Vendor == vendor))
            {
                bool on = applied.GetValueOrDefault(i.Id);
                GfxRow(i.Id, i.Title, i.Warn, i.Description, on, i.Default, DebloatRows);
                _debloatToggles[i.Id] = _gfxToggles[i.Id];
                _gfxToggles.Remove(i.Id);
            }
        }
        DebloatNo7z.Visibility = has7z ? Visibility.Collapsed : Visibility.Visible;
        BtnDebloatLatest.Visibility = vendors.Contains("nvidia") ? Visibility.Visible : Visibility.Collapsed;
        BtnDebloatUndo.Visibility = applied.Values.Any(x => x) ? Visibility.Visible : Visibility.Collapsed;
        DebloatStatus.Text = vendors.Count == 0 ? "Nenhuma placa NVIDIA, AMD ou Intel encontrada."
            : "";
    }

    private async void BtnDebloat7z_Click(object sender, RoutedEventArgs e)
    {
        if (!AppInstaller.WingetAvailable) { DebloatStatus.Text = "O winget não está disponível neste PC. Instale o 7-Zip pelo site 7-zip.org."; return; }
        if (MessageBox.Show(this, "Instalar o 7-Zip pelo winget (versão oficial)?", "7-Zip", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        DebloatStatus.Text = "Instalando o 7-Zip…";
        var app = AppInstaller.Catalog().First(a => a.WingetId == "7zip.7zip");
        var (ok, msg) = await AppInstaller.InstallAsync(app, CancellationToken.None);
        DebloatStatus.Text = "7-Zip: " + msg;
        if (ok && DriverDebloat.SevenZip() != null) DebloatNo7z.Visibility = Visibility.Collapsed;
    }

    private async void BtnDebloatLatest_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin() || !Need7z()) return;
        var g = _drvGpus.FirstOrDefault(x => x.Vendor == "NVIDIA") ?? DriverManager.ReadGpus().FirstOrDefault(x => x.Vendor == "NVIDIA");
        if (g == null) { DebloatStatus.Text = "Nenhuma placa NVIDIA encontrada."; return; }
        if (!ConfirmDebloat("nvidia")) return;
        await RunDebloat(async () =>
        {
            if (g.DownloadUrl.Length == 0) { DebloatStatus.Text = "Procurando o driver mais recente…"; await DriverManager.CheckNvidiaAsync(g, DrvStudio.IsChecked == true, CancellationToken.None); }
            if (g.DownloadUrl.Length == 0) throw new InvalidOperationException(g.Message);
            return await DriverManager.DownloadAsync(g, (t, _) => DebloatStatus.Text = t, CancellationToken.None);
        });
    }

    private async void BtnDebloatFile_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin() || !Need7z()) return;
        var dlg = new OpenFileDialog { Title = "Instalador do driver (NVIDIA, AMD ou Intel)", Filter = "Instalador do driver (*.exe)|*.exe" };
        if (dlg.ShowDialog(this) != true) return;
        string? vendor = await Task.Run(() => DriverDebloat.SignedVendor(dlg.FileName));
        if (vendor == null) { DebloatStatus.Text = "Esse arquivo não tem assinatura digital válida da NVIDIA, AMD ou Intel. Nada foi feito."; return; }
        if (!ConfirmDebloat(vendor)) return;
        await RunDebloat(() => Task.FromResult(dlg.FileName));
    }

    private bool Need7z()
    {
        if (DriverDebloat.SevenZip() != null) return true;
        DebloatNo7z.Visibility = Visibility.Visible;
        DebloatStatus.Text = "A instalação enxuta precisa do 7-Zip: use o link \"Instalar o 7-Zip\" acima.";
        return false;
    }

    private bool ConfirmDebloat(string vendor)
    {
        var picked = Picked(vendor).Select(i => "• " + i.Title).ToList();
        string what = vendor == "nvidia" ? "Sai do pacote:" : "Depois de instalar:";
        return MessageBox.Show(this,
            $"Instalar o driver {vendor.ToUpperInvariant()} do jeito enxuto, em silêncio e limpo?\n\n{what}\n{(picked.Count == 0 ? "• nada (instalação completa)" : string.Join("\n", picked))}\n\n" +
            "As configurações antigas do driver são apagadas; depois, as configurações marcadas no cartão são aplicadas. Não reinicia sozinho.",
            "Instalação enxuta", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
    }

    private List<DebloatItem> Picked(string vendor)
        => DriverDebloat.Nvidia.Concat(DriverDebloat.Amd).Concat(DriverDebloat.Intel)
            .Where(i => i.Vendor == vendor && _debloatToggles.TryGetValue(i.Id, out var sw) && sw.IsChecked == true).ToList();

    /// <summary>Pega o instalador, instala enxuto e aplica as limpezas marcadas do fabricante (com backup).</summary>
    private async Task RunDebloat(Func<Task<string>> getPackage)
    {
        _gfxBusy = true;
        BtnDebloatLatest.IsEnabled = BtnDebloatFile.IsEnabled = false;
        void Log(string t) => Dispatcher.BeginInvoke(() => DebloatStatus.Text = t);
        try
        {
            string package = await getPackage();
            var nvRemove = Picked("nvidia").Select(i => i.Id).ToHashSet();
            var (vendor, code) = await Task.Run(() => DriverDebloat.InstallAsync(package, nvRemove, Log, CancellationToken.None));
            var errors = new List<string>();
            var cleanups = vendor == "nvidia" ? new List<DebloatItem>() : Picked(vendor);
            await Task.Run(() =>
            {
                foreach (var i in cleanups)
                {
                    try
                    {
                        DriverDebloat.ExtraCleanup(i.Id, Log);
                        var m = DriverDebloat.ModuleFor(i);
                        if (m.Settings.Length > 0 && !GraphicsTweaks.Engine.HasBackup(m)) GraphicsTweaks.Engine.Enable(m);
                    }
                    catch (Exception ex) { lock (errors) errors.Add($"{i.Title}: {ex.Message}"); }
                }
            });
            string panel = "";
            if (vendor == "nvidia" && !GraphicsTweaks.HasNvidiaControlPanel())
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GraphicsTweaks.NvidiaControlPanelStore) { UseShellExecute = true }); } catch { }
                panel = " A página do Painel de Controle NVIDIA na Microsoft Store foi aberta: clique em Obter.";
            }
            InitDebloat(_debloatVendors);
            string settings = "";
            if (code == 0) { DebloatStatus.Text = "Driver instalado. Aplicando as configurações marcadas no cartão acima…"; settings = " " + await ApplyAfterInstallAsync(); }
            DebloatStatus.Text = (code == 0 ? "Driver instalado do jeito enxuto." : $"O instalador terminou com o código {code}; confira o driver no Gerenciador de Dispositivos.")
                                 + (errors.Count > 0 ? $" {errors.Count} limpeza(s) falharam: " + string.Join(" · ", errors) + "." : "")
                                 + settings + panel + " Reinicie para terminar.";
            BtnGfxReboot.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { DebloatStatus.Text = "Não foi possível instalar: " + ex.Message; }
        finally { _gfxBusy = false; BtnDebloatLatest.IsEnabled = BtnDebloatFile.IsEnabled = true; }
    }

    private async void BtnDebloatUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin()) return;
        if (MessageBox.Show(this, "Reativar os serviços e as entradas de inicialização que a instalação enxuta desligou?\n(Componentes tirados do pacote só voltam reinstalando o driver completo.)",
                "Desfazer as limpezas", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var i in DriverDebloat.Amd.Concat(DriverDebloat.Intel))
            {
                try { var m = DriverDebloat.ModuleFor(i); if (GraphicsTweaks.Engine.HasBackup(m)) GraphicsTweaks.Engine.Restore(m); }
                catch (Exception ex) { lock (errors) errors.Add($"{i.Title}: {ex.Message}"); }
            }
        });
        InitDebloat(_debloatVendors);
        await Task.Delay(300);
        DebloatStatus.Text = errors.Count == 0 ? "Limpezas desfeitas. Reinicie para os serviços voltarem." : "Concluído com erros: " + string.Join(" · ", errors);
    }
}
