using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>FERRAMENTAS · Drivers: limpeza completa do driver de vídeo com o DDU no modo de segurança.</summary>
public partial class MainWindow
{
    private bool _dduInit, _dduBusy;
    private string? _dduLocalSetup;
    private DispatcherTimer? _dduCountdown;

    /// <summary>Estado do cartão ao abrir a aba: GPU sugerida e bloqueio do Windows Update.</summary>
    private void InitDdu()
    {
        if (!_dduInit)
        {
            _dduInit = true;
            string vendor = _gpus.Select(g => g.Name).FirstOrDefault(n => n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) != null ? "nvidia"
                : _gpus.Any(g => g.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || g.Name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) ? "amd"
                : _gpus.Any(g => g.Name.Contains("Intel", StringComparison.OrdinalIgnoreCase)) ? "intel" : "all";
            foreach (var rb in DduGpuPanel.Children.OfType<RadioButton>()) rb.IsChecked = (string)rb.Tag == vendor;
        }
        BtnDduWuRestore.Visibility = _settings.DduWuBlocked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DduLogLine(string s)
    {
        DduLog.Text = (DduLog.Text.Length > 0 ? DduLog.Text + "\n" : "") + s;
        DriverClean.Log(s);
    }

    private void BtnDduLocal_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Instalador do DDU (*.exe)|*.exe", Title = "Instalador oficial do Display Driver Uninstaller" };
        if (dlg.ShowDialog(this) != true) return;
        _dduLocalSetup = dlg.FileName;
        DduStatus.Text = $"Vai usar {Path.GetFileName(dlg.FileName)} (a assinatura da Wagnardsoft é conferida antes).";
    }

    private async void BtnDduRun_Click(object sender, RoutedEventArgs e)
    {
        if (_dduBusy || !RequireAdmin()) return;
        var o = new DduOptions(DduAuto.IsChecked == true, CheckedTag(DduGpuPanel) ?? "all", DduAudio.IsChecked == true, DduBlockWu.IsChecked == true);
        string gpuText = o.Gpus switch { "nvidia" => "o driver NVIDIA", "amd" => "o driver AMD", "intel" => "o driver Intel", _ => "os drivers de todas as placas de vídeo" };
        string ask = $"Limpar {gpuText} com o DDU?\n\n" +
                     "1. O PcismoMaster baixa e confere o DDU.\n" +
                     "2. Cria um ponto de restauração.\n" +
                     "3. Reinicia no MODO DE SEGURANÇA (feche e salve tudo antes).\n" +
                     (o.Auto ? "4. Ao entrar com sua conta, o DDU limpa sozinho e o PC reinicia normal, sem driver de vídeo.\n"
                             : "4. Ao entrar com sua conta, o DDU abre: escolha a placa e clique em \"Clean and restart\".\n") +
                     "5. Depois, instale o driver no cartão \"Instalar driver e configurações\"." +
                     (o.Audio ? "\n\nVocê marcou remover o áudio Realtek/Sound Blaster: o PC fica sem som até reinstalar o driver de áudio." : "") +
                     "\n\nSe a limpeza não começar no modo de segurança, use o atalho \"Sair do modo de segurança (PcismoMaster)\" na Área de Trabalho.";
        if (MessageBox.Show(this, ask, "Limpeza do driver de vídeo", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

        _dduBusy = true;
        BtnDduRun.IsEnabled = false;
        DduLog.Text = "";
        var log = new Progress<string>(DduLogLine);
        IProgress<string> L = log;
        bool scheduled = false;
        try
        {
            L.Report($"Início: {(o.Auto ? "automático" : "manual")}, placas: {o.Gpus}, áudio: {o.Audio}, bloquear Windows Update: {o.BlockWu}.");
            // 1) DDU baixado, conferido, extraído e configurado.
            DduStatus.Text = "Preparando o DDU…";
            string exe = await Task.Run(() => DriverClean.PrepareAsync(o, _dduLocalSetup, s => L.Report(s), CancellationToken.None));

            // 2) Ponto de restauração.
            DduStatus.Text = "Criando o ponto de restauração (até 3 minutos)…";
            using var rpCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var ctx = new Maintenance.Ctx(log, rpCts.Token);
            string? why;
            try { why = await Maintenance.CreateRestorePoint(ctx, "Limpeza do driver de vídeo"); }
            catch (OperationCanceledException) { why = "o Windows demorou mais de 3 minutos para criar"; }
            if (why != null
                && MessageBox.Show(this, $"Não foi possível criar o ponto de restauração: {why}.\n\nContinuar mesmo assim?", "Limpeza do driver de vídeo",
                                   MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                DduStatus.Text = "Cancelado. Nada foi alterado no boot.";
                return;
            }

            // 3) Windows Update: guarda o valor atual uma vez, para o botão de desfazer.
            if (o.BlockWu)
            {
                if (!_settings.DduWuBlocked) { _settings.DduWuBackup = DriverClean.ReadWuSearchOrder(); _settings.DduWuBlocked = true; _settings.Save(); }
                DriverClean.BlockWu();
                L.Report("Windows Update não vai instalar driver de vídeo sozinho (dá para liberar depois).");
            }

            // 4) Modo de segurança + reinício com contagem regressiva.
            DduStatus.Text = "Programando o modo de segurança…";
            await Task.Run(() => DriverClean.ScheduleSafeMode(o, exe));
            scheduled = true;
            L.Report("Modo de segurança programado. Atalho de emergência criado na Área de Trabalho.");
            await Task.Run(() => DriverClean.Restart(32));
            L.Report("Reinício agendado para daqui a 30 s.");
            StartDduCountdown(30);
        }
        catch (Exception ex)
        {
            if (scheduled) try { DriverClean.Unschedule(); } catch { }
            DduStatus.Text = "Não foi possível preparar a limpeza: " + ex.Message;
            L.Report("ERRO: " + ex);
            MessageBox.Show(this, $"A limpeza não começou: {ex.Message}\n\nNada foi alterado no boot. O registro completo está em:\n{DriverClean.LogFile}",
                "Limpeza do driver de vídeo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _dduBusy = false;
            if (!scheduled) BtnDduRun.IsEnabled = true;
            InitDdu();
        }
    }

    private void StartDduCountdown(int seconds)
    {
        int left = seconds;
        BtnDduCancel.Visibility = Visibility.Visible;
        DduStatus.Text = $"Reiniciando no modo de segurança em {left} s… Salve e feche seus programas.";
        _dduCountdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _dduCountdown.Tick += (_, _) =>
        {
            left--;
            DduStatus.Text = left > 0 ? $"Reiniciando no modo de segurança em {left} s… Salve e feche seus programas." : "Reiniciando…";
            if (left <= 0) _dduCountdown?.Stop();
        };
        _dduCountdown.Start();
    }

    private void BtnDduCancel_Click(object sender, RoutedEventArgs e)
    {
        _dduCountdown?.Stop();
        try { DriverClean.AbortRestart(); } catch { }
        try { DriverClean.Unschedule(); } catch (Exception ex) { DduStatus.Text = "Atenção: " + ex.Message; return; }
        BtnDduCancel.Visibility = Visibility.Collapsed;
        BtnDduRun.IsEnabled = true;
        DduStatus.Text = "Reinício cancelado. O PC vai iniciar normalmente (o modo de segurança foi desfeito).";
    }

    private void BtnDduWuRestore_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        try
        {
            DriverClean.RestoreWu(_settings.DduWuBackup);
            _settings.DduWuBlocked = false; _settings.DduWuBackup = null; _settings.Save();
            DduStatus.Text = "Windows Update liberado para instalar drivers de novo (como estava antes).";
        }
        catch (Exception ex) { DduStatus.Text = "Não foi possível liberar: " + ex.Message; }
        InitDdu();
    }

}
