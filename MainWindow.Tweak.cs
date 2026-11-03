using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>TWEAKING: o "3 Setup" do Ultimate (BitLocker, compressão de memória, bloqueio de drivers/atualizações, apps em segundo plano, Edge e Store).</summary>
public partial class MainWindow
{
    private bool _tweakInit, _tweakBusy;
    private readonly Dictionary<string, CheckBox> _tweakToggles = new();
    private SetupTweak[] _setupTweaks = Array.Empty<SetupTweak>();

    private static string TweakBackupFile(string id) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups", id + ".json");

    /// <summary>Se já foi aplicado, usa as chaves gravadas no backup (desfaz exatamente o que mudou, mesmo que o PC tenha mudado).</summary>
    private static Module TweakModule(SetupTweak t) =>
        File.Exists(TweakBackupFile(t.Id)) && JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(TweakBackupFile(t.Id))) is { } saved
            ? t.Module with { Settings = saved.Settings } : t.Module;

    private static bool ServiceExists(string name)
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name);
        return k != null;
    }

    private void InitTweak()
    {
        if (_tweakInit) return;
        _tweakInit = true;
        BuildTweakRows();
        _ = RefreshMemCompressionAsync();
        _ = RefreshBitLockerAsync();
    }

    private void BuildTweakRows()
    {
        TweakRows.Children.Clear();
        _tweakToggles.Clear();
        var edge = SetupTweaks.EdgeUpdate(ServiceExists);
        _setupTweaks = edge.Settings.Length > 0 || File.Exists(TweakBackupFile(edge.Id)) ? SetupTweaks.Static.Append(edge).ToArray() : SetupTweaks.Static;
        foreach (var t in _setupTweaks)
            _tweakToggles[t.Id] = ToggleRow(t.Title, t.Advanced, t.Description, GraphicsTweaks.Engine.HasBackup(TweakModule(t)), t.DefaultOn, TweakRows);
    }

    private void BtnTweakDefaults_Click(object sender, RoutedEventArgs e)
    {
        foreach (var t in _setupTweaks) _tweakToggles[t.Id].IsChecked = t.DefaultOn;
    }

    private void BtnTweakUndoAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var sw in _tweakToggles.Values) sw.IsChecked = false;
        BtnTweakApply_Click(sender, e);
    }

    private async void BtnTweakApply_Click(object sender, RoutedEventArgs e)
    {
        if (_tweakBusy || !RequireAdmin()) return;
        var turnOn = _setupTweaks.Where(t => _tweakToggles[t.Id].IsChecked == true && _tweakToggles[t.Id].Tag is false).ToList();
        var turnOff = _setupTweaks.Where(t => _tweakToggles[t.Id].IsChecked != true && _tweakToggles[t.Id].Tag is true).ToList();
        if (turnOn.Count + turnOff.Count == 0) { TweakStatus.Text = "Nada mudou: os marcados já estão aplicados."; return; }
        if (turnOn.Any(t => t.Id == "tw-block-updates") && MessageBox.Show(this,
                "Bloquear o Windows Update deixa o PC sem atualizações de segurança até você desfazer. Continuar?",
                "Bloquear o Windows Update", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (MessageBox.Show(this, $"Aplicar {turnOn.Count} ajuste(s) e desfazer {turnOff.Count}? O valor anterior de cada um é guardado.",
                "Tweaking", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        _tweakBusy = true;
        BtnTweakApply.IsEnabled = false;
        TweakStatus.Text = "Aplicando…";
        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var t in turnOff) Try(t, () => GraphicsTweaks.Engine.Restore(TweakModule(t)));
            foreach (var t in turnOn) Try(t, () =>
            {
                var m = TweakModule(t);
                if (m.Settings.Length == 0) throw new InvalidOperationException("nada deste ajuste neste PC");
                GraphicsTweaks.Engine.Enable(m);
            });
        });
        void Try(SetupTweak t, Action a) { try { a(); } catch (Exception ex) { lock (errors) errors.Add($"{t.Title}: {ex.Message}"); } }

        _tweakBusy = false;
        BtnTweakApply.IsEnabled = true;
        BuildTweakRows();
        TweakStatus.Text = (errors.Count == 0
            ? $"Pronto: {turnOn.Count} aplicado(s)" + (turnOff.Count > 0 ? $", {turnOff.Count} desfeito(s)." : ".")
            : $"Concluído com {errors.Count} erro(s): " + string.Join(" · ", errors) + ".")
            + " Reinicie o PC (ou o Edge/a Store) para tudo valer.";
    }

    // ---------------- Compressão de memória (Get/Enable/Disable-MMAgent) ----------------

    private static async Task<string> PowerShellAsync(string command)
    {
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        string output = await p.StandardOutput.ReadToEndAsync();
        string error = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0 || error.Trim().Length > 0) throw new InvalidOperationException(error.Trim().Length > 0 ? error.Trim().Split('\n')[0] : "PowerShell saiu com erro " + p.ExitCode);
        return output.Trim();
    }

    private async Task RefreshMemCompressionAsync()
    {
        try
        {
            bool on = (await PowerShellAsync("(Get-MMAgent).MemoryCompression")).Equals("True", StringComparison.OrdinalIgnoreCase);
            TweakMemStatus.Text = on ? "Agora: LIGADA." : "Agora: DESLIGADA.";
        }
        catch (Exception ex) { TweakMemStatus.Text = "Não deu para ler: " + ex.Message; }
    }

    private void BtnTweakMemOff_Click(object sender, RoutedEventArgs e) => _ = SetMemCompressionAsync(false);
    private void BtnTweakMemOn_Click(object sender, RoutedEventArgs e) => _ = SetMemCompressionAsync(true);

    private async Task SetMemCompressionAsync(bool on)
    {
        if (!RequireAdmin()) return;
        if (MessageBox.Show(this, (on ? "Ligar" : "Desligar") + " a compressão de memória? Vale depois de reiniciar e dá para mudar de novo aqui.",
                "Compressão de memória", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        BtnTweakMemOff.IsEnabled = BtnTweakMemOn.IsEnabled = false;
        try
        {
            await PowerShellAsync((on ? "Enable" : "Disable") + "-MMAgent -MemoryCompression");
            await RefreshMemCompressionAsync();
            TweakMemStatus.Text += " Reinicie o PC para valer.";
        }
        catch (Exception ex) { TweakMemStatus.Text = "Não deu: " + ex.Message; }
        finally { BtnTweakMemOff.IsEnabled = BtnTweakMemOn.IsEnabled = true; }
    }

    // ---------------- BitLocker (Win32_EncryptableVolume) ----------------

    private sealed record BitVolume(ManagementObject Wmi, string Drive, bool Protected, int Conversion, int Percent);

    private static List<BitVolume> BitLockerVolumes()
    {
        var list = new List<BitVolume>();
        var scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftVolumeEncryption");
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));
        foreach (ManagementObject v in searcher.Get())
        {
            var conv = v.InvokeMethod("GetConversionStatus", null, null);
            int status = Convert.ToInt32(conv?["ConversionStatus"] ?? 0), pct = Convert.ToInt32(conv?["EncryptionPercentage"] ?? 0);
            list.Add(new(v, (v["DriveLetter"] as string) ?? "(sem letra)", Convert.ToInt32(v["ProtectionStatus"] ?? 0) == 1, status, pct));
        }
        return list;
    }

    private static string BitDescribe(BitVolume v) => v.Conversion switch
    {
        0 => $"{v.Drive} sem criptografia",
        1 => $"{v.Drive} criptografado" + (v.Protected ? " (protegido)" : " (proteção suspensa)"),
        2 => $"{v.Drive} criptografando ({v.Percent}%)",
        3 => $"{v.Drive} descriptografando ({v.Percent}% ainda criptografado)",
        _ => $"{v.Drive} em pausa ({v.Percent}%)",
    };

    private async Task RefreshBitLockerAsync()
    {
        try
        {
            var vols = await Task.Run(BitLockerVolumes);
            bool any = vols.Any(v => v.Protected || v.Conversion != 0);
            TweakBitStatus.Text = vols.Count == 0 ? "Nenhum disco com BitLocker neste PC."
                : "Agora: " + string.Join(" · ", vols.Select(BitDescribe)) + ".";
            BtnTweakBitOff.IsEnabled = any;
        }
        catch (ManagementException) { TweakBitStatus.Text = "Esta edição do Windows não tem BitLocker (ou a criptografia de dispositivo está indisponível)."; BtnTweakBitOff.IsEnabled = false; }
        catch (Exception ex) { TweakBitStatus.Text = "Não deu para ler: " + ex.Message; BtnTweakBitOff.IsEnabled = false; }
    }

    private async void BtnTweakBitOff_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        if (MessageBox.Show(this,
                "Desligar o BitLocker em todos os discos? Eles serão descriptografados (pode levar de minutos a horas) e os dados " +
                "ficam legíveis se o disco ou o PC for roubado. Para ligar de novo, use as configurações do BitLocker.",
                "Desligar o BitLocker", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        BtnTweakBitOff.IsEnabled = false;
        TweakBitStatus.Text = "Desligando…";
        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var v in BitLockerVolumes().Where(v => v.Protected || v.Conversion is 1 or 2 or 4 or 5))
            {
                try
                {
                    var r = v.Wmi.InvokeMethod("Decrypt", null, null);
                    uint code = Convert.ToUInt32(r?["ReturnValue"] ?? 0u);
                    if (code != 0) errors.Add($"{v.Drive}: código 0x{code:X8}");
                }
                catch (Exception ex) { errors.Add($"{v.Drive}: {ex.Message}"); }
            }
        });
        await RefreshBitLockerAsync();
        if (errors.Count > 0) TweakBitStatus.Text += " Erros: " + string.Join(" · ", errors) + ".";
        else TweakBitStatus.Text += " A descriptografia continua sozinha em segundo plano.";
    }

    // ---------------- Atalhos ----------------

    private void BtnTweakOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tag) return;
        var parts = tag.Split('|', 2);
        try { Process.Start(new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Tweaking"); }
    }
}
