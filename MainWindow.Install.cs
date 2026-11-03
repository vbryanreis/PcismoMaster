using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>
/// FERRAMENTAS · Drivers · "Instalar driver e configurações": um cartão só, com instalação Normal (driver completo,
/// silencioso e limpo) ou Enxuta (debloat). Nas duas, as configurações marcadas no cartão são aplicadas logo depois.
/// </summary>
public partial class MainWindow
{
    private bool InstIsDebloat => InstDebloat.IsChecked == true;

    private void InstMode_Changed(object sender, RoutedEventArgs e)
    {
        if (InstDebloatPanel == null || InstModeHelp == null) return; // durante InitializeComponent
        InstDebloatPanel.Visibility = InstIsDebloat ? Visibility.Visible : Visibility.Collapsed;
        InstModeHelp.Text = InstIsDebloat
            ? "Instala só o driver, sem telemetria, apps e serviços extras. Marque o que deve sair; o que pode quebrar algo (áudio pela HDMI, PhysX, DLDSR, notebooks…) vem desmarcado. Serviços são desativados, não apagados, e podem ser desfeitos."
            : "Driver oficial com assinatura conferida, instalado sem telas e do zero (apaga as configurações antigas do driver).";
    }

    private void BtnInstLatest_Click(object sender, RoutedEventArgs e)
    {
        if (InstIsDebloat) BtnDebloatLatest_Click(sender, e);
        else _ = InstallNormalAsync(null);
    }

    private async void BtnInstFile_Click(object sender, RoutedEventArgs e)
    {
        if (InstIsDebloat) { BtnDebloatFile_Click(sender, e); return; }
        if (_gfxBusy || !RequireAdmin()) return;
        var dlg = new OpenFileDialog { Title = "Instalador do driver (NVIDIA, AMD ou Intel)", Filter = "Instalador do driver (*.exe)|*.exe" };
        if (dlg.ShowDialog(this) != true) return;
        string? vendor = await Task.Run(() => DriverDebloat.SignedVendor(dlg.FileName));
        if (vendor == null) { DebloatStatus.Text = "Esse arquivo não tem assinatura digital válida da NVIDIA, AMD ou Intel. Nada foi feito."; return; }
        if (vendor == "nvidia") { await InstallNormalAsync(dlg.FileName); return; }
        // AMD e Intel na instalação normal: o instalador oficial, com as telas dele; as configurações vêm depois, em Aplicar.
        try
        {
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            DebloatStatus.Text = "Instalador aberto. Quando terminar, clique em Aplicar abaixo para as configurações e reinicie.";
        }
        catch (Exception ex) { DebloatStatus.Text = "Não foi possível abrir o instalador: " + ex.Message; }
    }

    /// <summary>Instalação normal NVIDIA: baixa o mais recente (ou usa o arquivo escolhido), instala em silêncio e limpo e aplica as configurações.</summary>
    private async Task InstallNormalAsync(string? file)
    {
        if (_gfxBusy || !RequireAdmin()) return;
        var g = _drvGpus.FirstOrDefault(x => x.Vendor == "NVIDIA") ?? await Task.Run(() => DriverManager.ReadGpus().FirstOrDefault(x => x.Vendor == "NVIDIA"));
        if (g == null) { DebloatStatus.Text = "Nenhuma placa NVIDIA encontrada."; return; }
        if (MessageBox.Show(this,
                "Instalar o driver NVIDIA" + (file == null ? " mais recente" : "") + " em modo silencioso e limpo?\n\n" +
                (file == null ? "• Baixa do servidor oficial e confere a assinatura.\n" : "") +
                "• Instala sem telas; a imagem pisca algumas vezes.\n• \"Limpo\" apaga as configurações antigas do driver.\n" +
                "• Depois, as configurações marcadas no cartão são aplicadas.\n• Não reinicia sozinho.",
                "Driver NVIDIA", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _gfxBusy = true;
        BtnDebloatLatest.IsEnabled = BtnDebloatFile.IsEnabled = false;
        try
        {
            string version = g.Latest;
            if (file == null)
            {
                if (g.DownloadUrl.Length == 0) { DebloatStatus.Text = "Procurando o driver mais recente…"; await DriverManager.CheckNvidiaAsync(g, DrvStudio.IsChecked == true, CancellationToken.None); }
                if (g.DownloadUrl.Length == 0) { DebloatStatus.Text = g.Message; return; }
                file = await DriverManager.DownloadAsync(g, (t, _) => DebloatStatus.Text = t, CancellationToken.None);
                version = g.Latest;
            }
            else version = FileVersionInfo.GetVersionInfo(file).ProductVersion ?? "";
            DebloatStatus.Text = $"Instalando o driver {version} em silêncio… (pode levar alguns minutos; a tela vai piscar)";
            int code = await GraphicsTweaks.InstallNvidiaSilentAsync(file, CancellationToken.None);
            string settings = "";
            if (code == 0) { DebloatStatus.Text = "Driver instalado. Aplicando as configurações marcadas…"; settings = " " + await ApplyAfterInstallAsync(); }
            string panel = "";
            if (!GraphicsTweaks.HasNvidiaControlPanel())
            {
                try { Process.Start(new ProcessStartInfo(GraphicsTweaks.NvidiaControlPanelStore) { UseShellExecute = true }); } catch { }
                panel = " A página do Painel de Controle NVIDIA na Microsoft Store foi aberta: clique em Obter.";
            }
            DebloatStatus.Text = (code == 0 ? $"Driver {version} instalado." : $"O instalador terminou com o código {code}; confira se o driver aparece no Gerenciador de Dispositivos.")
                                 + settings + panel + " Reinicie para terminar. " + GpuDriverStatus.NvidiaAppTips;
            BtnGfxReboot.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { DebloatStatus.Text = "Não foi possível instalar: " + ex.Message; }
        finally { _gfxBusy = false; BtnDebloatLatest.IsEnabled = BtnDebloatFile.IsEnabled = true; }
    }
}
