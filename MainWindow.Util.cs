using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ResetExplode;

/// <summary>
/// Aba FERRAMENTAS · Utilidades: duas ferramentas do dia a dia em sub-sub-abas —
/// "Baixar vídeos" (yt-dlp + ffmpeg, instalados pela aba Instalar apps) e "Finder" (busca instantânea estilo
/// Everything lendo a MFT dos discos NTFS).
/// </summary>
public partial class MainWindow
{
    private bool _utilInit;
    private string _dlFolder = MediaDownloader.DefaultFolder;

    // baixador
    private CancellationTokenSource? _dlCts;
    private bool _dlBusy;

    // finder
    private readonly MftIndex _mft = new();
    private bool _mftBuilding;
    private string _findKind = "all";
    private DispatcherTimer? _findDebounce;

    /// <summary>Item da lista de resultados do Finder.</summary>
    public sealed record FindRow(string Icon, string Name, string FullPath, bool IsDir);

    private void InitUtil()
    {
        if (!_utilInit)
        {
            _utilInit = true;
            DlFolder.Text = _dlFolder;
            _findDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _findDebounce.Tick += (_, _) => { _findDebounce!.Stop(); RunSearch(); };
        }
        RefreshDlTools();
    }

    // ---------------- sub-sub-abas ----------------

    private async void UtilTab_Checked(object sender, RoutedEventArgs e)
    {
        if (UtilDownloadPanel == null) return; // durante InitializeComponent
        bool finder = UtilTabFinder.IsChecked == true;
        UtilDownloadPanel.Visibility = finder ? Visibility.Collapsed : Visibility.Visible;
        UtilFinderPanel.Visibility = finder ? Visibility.Visible : Visibility.Collapsed;
        if (finder)
        {
            await EnsureIndexAsync();
            RunSearch();
        }
        else RefreshDlTools();
    }

    // ---------------- baixar vídeos ----------------

    private void RefreshDlTools()
    {
        bool ready = MediaDownloader.Ready;
        DlToolsWarn.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        if (!_dlBusy) BtnDownload.IsEnabled = ready;
    }

    private void BtnDlInstallTools_Click(object sender, RoutedEventArgs e) => NavApps.IsChecked = true;

    private void BtnDlFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Pasta de destino" };
        try { if (Directory.Exists(_dlFolder)) dlg.InitialDirectory = _dlFolder; } catch { }
        if (dlg.ShowDialog() == true)
        {
            _dlFolder = dlg.FolderName;
            DlFolder.Text = _dlFolder;
        }
    }

    private void BtnDlOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_dlFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_dlFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { DlStatus.Text = $"Não foi possível abrir a pasta: {ex.Message}"; }
    }

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_dlBusy) { _dlCts?.Cancel(); BtnDownload.IsEnabled = false; DlStatus.Text = "Cancelando…"; return; }

        if (!MediaDownloader.Ready) { RefreshDlTools(); DlStatus.Text = "Instale o yt-dlp e o ffmpeg na aba Instalar apps."; return; }
        string url = DlUrl.Text.Trim();
        if (url.Length == 0) { DlStatus.Text = "Cole um link primeiro."; return; }
        string fmt = DlFmtMp3.IsChecked == true ? "mp3" : "mp4";

        _dlBusy = true; _dlCts = new CancellationTokenSource();
        BtnDownload.Content = "PARAR";
        DlStatus.Text = "Baixando…";
        try
        {
            var (ok, msg) = await MediaDownloader.DownloadAsync(url, fmt, _dlFolder,
                line => Dispatcher.BeginInvoke((Action)(() => DlStatus.Text = line)), _dlCts.Token);
            DlStatus.Text = ok ? $"✓ {msg} Salvo em {_dlFolder}" : $"✗ {msg}";
        }
        catch (OperationCanceledException) { DlStatus.Text = "Cancelado."; }
        catch (Exception ex) { DlStatus.Text = $"✗ Erro: {ex.Message}"; }
        finally
        {
            _dlBusy = false; _dlCts?.Dispose(); _dlCts = null;
            BtnDownload.Content = "BAIXAR"; BtnDownload.IsEnabled = true;
        }
    }

    // ---------------- finder (MFT) ----------------

    private async Task EnsureIndexAsync()
    {
        if (_mft.Ready || _mftBuilding) return;
        if (!MftIndex.NtfsDrives().Any()) { FindStatus.Text = "Nenhum disco NTFS encontrado para indexar."; return; }
        _mftBuilding = true;
        FindStatus.Text = "Indexando os discos… (só na primeira busca)";
        try
        {
            await Task.Run(() => _mft.Build());
            FindStatus.Text = $"{_mft.Count:N0} itens indexados.";
        }
        catch (Exception ex) { FindStatus.Text = $"Falha ao indexar: {ex.Message}"; }
        finally { _mftBuilding = false; }
    }

    private void FindQuery_TextChanged(object sender, TextChangedEventArgs e)
    {
        _findDebounce?.Stop();
        _findDebounce?.Start();
    }

    private void FindKind_Checked(object sender, RoutedEventArgs e)
    {
        if (FindResults == null) return; // durante InitializeComponent
        if (sender is RadioButton { Tag: string tag }) _findKind = tag;
        RunSearch();
    }

    private async void RunSearch()
    {
        if (!_mft.Ready)
        {
            await EnsureIndexAsync();
            if (!_mft.Ready) return;
        }
        string q = FindQuery.Text.Trim();
        if (q.Length == 0)
        {
            FindResults.ItemsSource = null;
            FindStatus.Text = $"{_mft.Count:N0} itens indexados.";
            return;
        }

        bool files = _findKind == "files", folders = _findKind == "folders";
        var hits = await Task.Run(() => _mft.Search(q, files, folders, 500));
        if (q != FindQuery.Text.Trim()) return; // busca antiga: chegou outra tecla

        FindResults.ItemsSource = hits
            .Select(h => new FindRow(h.IsDir ? "" : "", h.Name, h.FullPath, h.IsDir))
            .ToList();
        FindStatus.Text = hits.Count >= 500 ? "500+ resultados · refine a busca" : $"{hits.Count} resultado(s)";
    }

    private void FindResults_Open(object sender, MouseButtonEventArgs e)
    {
        if (FindResults.SelectedItem is not FindRow row) return;
        try
        {
            var psi = row.IsDir && Directory.Exists(row.FullPath)
                ? new ProcessStartInfo("explorer.exe", $"\"{row.FullPath}\"")
                : new ProcessStartInfo("explorer.exe", $"/select,\"{row.FullPath}\"");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex) { FindStatus.Text = $"Não foi possível abrir: {ex.Message}"; }
    }
}
