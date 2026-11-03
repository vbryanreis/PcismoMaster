using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ResetExplode;

/// <summary>Aba RESET FERRAMENTAS · Instalar apps: essenciais pós-formatação pelo winget, em cartões com o ícone de cada app.</summary>
public partial class MainWindow
{
    private List<InstallableApp>? _installApps;
    private bool _appsBusy, _appsStop, _appsIconsLoading;
    private string _appsCat = ""; // sub-aba de categoria selecionada ("" = Todos)

    /// <summary>Apps visíveis agora (categoria, busca e "esconder instalados").</summary>
    private IEnumerable<InstallableApp> VisibleApps() =>
        _installApps == null ? Enumerable.Empty<InstallableApp>() : _installApps.Where(AppVisible);

    private bool AppVisible(InstallableApp a)
    {
        if (_appsCat.Length > 0 && a.Category != _appsCat) return false;
        if (AppsHideInstalled.IsChecked == true && a.Installed) return false;
        string q = AppsSearch.Text.Trim();
        return q.Length == 0 || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || a.Description.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void InitApps()
    {
        if (_appsBusy) return;
        if (_installApps == null)
        {
            _installApps = AppInstaller.Catalog();
            // Agrupado por categoria, na ordem das sub-abas (estilo Ninite).
            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(_installApps);
            view.CustomSort = Comparer<object>.Create((x, y) =>
            {
                var a = (InstallableApp)x; var b = (InstallableApp)y;
                int c = Array.IndexOf(AppInstaller.Categories, a.Category).CompareTo(Array.IndexOf(AppInstaller.Categories, b.Category));
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InstallableApp.Category)));
            view.Filter = o => AppVisible((InstallableApp)o);
            AppsList.ItemsSource = view;
            AppInstaller.RefreshInstalled(_installApps);
            foreach (var a in _installApps)
            {
                a.Selected = a.PreSelect && !a.Installed;
                a.PropertyChanged += App_PropertyChanged;
            }
        }
        else AppInstaller.RefreshInstalled(_installApps);
        _ = LoadAppIconsAsync();
        UpdateAppsSelCount();

        bool winget = AppInstaller.WingetAvailable;
        BtnAppsStore.Visibility = winget ? Visibility.Collapsed : Visibility.Visible;
        BtnAppsInstall.IsEnabled = winget;
        int installed = _installApps.Count(a => a.Installed);
        AppsStatus.Text = winget
            ? $"{installed} de {_installApps.Count} já instalados. Clique nos cartões do que quer e em INSTALAR SELECIONADOS."
            : "O winget não foi encontrado. Em PC recém-formatado ele aparece depois que a Microsoft Store atualiza o " +
              "\"Instalador de Aplicativo\": use o botão abaixo, espere atualizar e abra esta página de novo.";
    }

    /// <summary>Ícones em segundo plano, poucos de cada vez: do cache (instantâneo), do site do app ou do app instalado.</summary>
    private async Task LoadAppIconsAsync()
    {
        if (_installApps == null || _appsIconsLoading) return;
        _appsIconsLoading = true;
        try
        {
            var pending = _installApps.Where(a => a.Icon == null).ToList();
            using var gate = new SemaphoreSlim(4);
            await Task.WhenAll(pending.Select(async a =>
            {
                await gate.WaitAsync();
                try
                {
                    var icon = await AppIcons.FromWebAsync(a.WingetId, a.Site, AppInstaller.IconUrls.GetValueOrDefault(a.WingetId, ""));
                    if (icon == null && a.LocalIcon.Length > 0) icon = await Task.Run(() => AppIcons.FromFile(a.LocalIcon));
                    if (icon != null) a.Icon = icon; // ImageSource congelado: pode vir de outra thread
                }
                finally { gate.Release(); }
            }));
        }
        catch { }
        finally { _appsIconsLoading = false; }
    }

    private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallableApp.Selected)) UpdateAppsSelCount();
    }

    private void UpdateAppsSelCount()
    {
        int n = _installApps?.Count(a => a.Selected && !a.Installed) ?? 0;
        AppsSelCount.Text = n == 0 ? "Nenhum selecionado" : n == 1 ? "1 selecionado" : $"{n} selecionados";
    }

    private void RefreshAppsView()
    {
        if (_installApps != null) CollectionViewSource.GetDefaultView(_installApps).Refresh();
    }

    private void AppCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (_appsBusy || sender is not FrameworkElement { DataContext: InstallableApp a } || !a.CanSelect) return;
        a.Selected = !a.Selected;
    }

    private void BtnAppsAll_Click(object sender, RoutedEventArgs e)
    {
        if (_installApps == null || _appsBusy) return;
        foreach (var a in VisibleApps()) a.Selected = !a.Installed;
    }

    private void BtnAppsNone_Click(object sender, RoutedEventArgs e)
    {
        if (_installApps == null || _appsBusy) return;
        foreach (var a in VisibleApps()) a.Selected = false;
    }

    private void AppsCat_Checked(object sender, RoutedEventArgs e)
    {
        if (_installApps == null) return; // disparado durante InitializeComponent
        if (sender is RadioButton { Tag: string cat }) _appsCat = cat;
        RefreshAppsView();
    }

    private void AppsSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshAppsView();

    private void AppsHideInstalled_Changed(object sender, RoutedEventArgs e) => RefreshAppsView();

    private void BtnAppsStore_Click(object sender, RoutedEventArgs e)
    {
        // Página do "Instalador de Aplicativo" (App Installer), que traz o winget.
        try { Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?productid=9NBLGGH4NNS1") { UseShellExecute = true }); }
        catch (Exception ex) { AppsStatus.Text = $"Não foi possível abrir a Microsoft Store: {ex.Message}"; }
    }

    private async void BtnAppsInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_installApps == null) return;
        if (_appsBusy) { _appsStop = true; BtnAppsInstall.IsEnabled = false; AppsStatus.Text = "Parando depois do app atual…"; return; }

        var sel = _installApps.Where(a => a.Selected && !a.Installed).ToList();
        if (sel.Count == 0) { AppsStatus.Text = "Nenhum app selecionado."; return; }
        if (sel.Any(a => a.WingetId == "RARLab.WinRAR") && sel.Any(a => a.WingetId == "7zip.7zip"))
        {
            var r = MessageBox.Show("WinRAR e 7-Zip fazem a mesma coisa. Instalar os dois mesmo assim?", "Instalar apps",
                                    MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        _appsBusy = true; _appsStop = false;
        BtnAppsInstall.Content = "PARAR";
        foreach (var a in sel) a.Status = "Na fila.";
        int ok = 0, done = 0;
        var failed = new List<string>();
        AppsProgress.ScaleX = 0;

        foreach (var app in sel)
        {
            if (_appsStop) { app.Status = "Não instalado (parado)."; continue; }
            AppsStatus.Text = $"Instalando {app.Name} ({done + 1} de {sel.Count})… baixando do site oficial, pode levar alguns minutos.";
            app.Status = "Instalando…";
            try
            {
                var (success, msg) = await AppInstaller.InstallAsync(app, CancellationToken.None);
                app.Status = msg;
                if (success) { ok++; app.Selected = false; }
                else failed.Add(app.Name);
            }
            catch (Exception ex) { app.Status = $"Falhou: {ex.Message}"; failed.Add(app.Name); }
            done++;
            AppsProgress.ScaleX = (double)done / sel.Count;
        }

        AppInstaller.RefreshInstalled(_installApps);
        RefreshAppsView();
        _appsBusy = false;
        BtnAppsInstall.Content = "INSTALAR SELECIONADOS";
        BtnAppsInstall.IsEnabled = true;
        AppsStatus.Text = $"{ok} de {sel.Count} instalados." +
                          (failed.Count > 0 ? $" Falharam: {string.Join(", ", failed)} (veja o motivo no cartão)." : "") +
                          (_appsStop ? " Instalação interrompida." : "");
    }
}
