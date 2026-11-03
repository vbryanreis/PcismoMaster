using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Globalization;
using ResetExplode.Modular;

namespace ResetExplode;
public partial class MainWindow
{
    // ---------------- FERRAMENTAS · Inicialização do Windows ----------------

    private readonly ModuleEngine updateEngine = new(new RegistryStore(), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups"));
    private Module UpdateModule()
    {
        var now = DateTime.UtcNow;
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", "updates-pause.json");
        if (File.Exists(file))
        {
            var saved = System.Text.Json.JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file))!;
            return new("updates-pause", "Pausar atualizações", "Windows", "", "12 Updates Pause.ps1", saved.Settings);
        }
        return new("updates-pause", "Pausar atualizações", "Windows", "", "12 Updates Pause.ps1",
            new[] { "PauseUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseFeatureUpdatesStartTime", "PauseQualityUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseUpdatesStartTime" }
            .Select(n => new Setting("HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", n, 0, (n.Contains("Start") ? now : now.AddDays(365)).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).ToArray());
    }

    private void InitUltimate() => RefreshWindowsUpdate();

    private void RefreshWindowsUpdate()
    {
        try { WuStatus.Text = updateEngine.HasBackup(UpdateModule()) ? "Backup disponível. Você pode restaurar a configuração anterior." : "Nenhuma pausa aplicada pelo PcismoMaster."; }
        catch (Exception ex) { WuStatus.Text = ex.Message; }
    }

    private void ChangeWindowsUpdate(bool restore)
    {
        try
        {
            var m = UpdateModule();
            if (restore && !updateEngine.HasBackup(m))
            {
                MessageBox.Show(this, "Não há nada para restaurar: o PcismoMaster ainda não pausou o Windows Update neste PC.", "Windows Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var label = restore ? "Restaurar configuração anterior" : "Pausar por 365 dias";
            if (MessageBox.Show(this, label + "? A pausa inclui correções de segurança. Restaurar recupera os valores anteriores, que também podem conter uma pausa.", "Windows Update", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            if (restore) updateEngine.Restore(m); else updateEngine.Enable(m);
            RefreshWindowsUpdate();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Operação não concluída"); }
    }

    private void BtnWuPause_Click(object sender, RoutedEventArgs e) => ChangeWindowsUpdate(false);
    private void BtnWuRestore_Click(object sender, RoutedEventArgs e) => ChangeWindowsUpdate(true);
    private void BtnWuOpen_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Windows Update"); }
    }
}
