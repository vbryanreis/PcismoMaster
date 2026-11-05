using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>FERRAMENTAS · Drivers: verificação das bibliotecas Visual C++ e DirectX (sem instalar nada).</summary>
public partial class MainWindow
{
    private async void InitRuntimes()
    {
        RuntimeNote.Text = "Verificando…";
        var list = await Task.Run(RuntimeCheck.Read);
        RuntimeRows.Children.Clear();
        foreach (var r in list)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = r.Name, FontWeight = FontWeights.SemiBold });
            if (r.Note.Length > 0) name.Children.Add(new TextBlock { Text = "  " + r.Note, Foreground = (Brush)FindResource("Faint"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            grid.Children.Add(name);
            var x86 = RuntimeBadge("32 bits", r.X86); Grid.SetColumn(x86, 1); grid.Children.Add(x86);
            var x64 = RuntimeBadge("64 bits", r.X64); Grid.SetColumn(x64, 2); grid.Children.Add(x64);
            RuntimeRows.Children.Add(grid);
        }
        int missing = list.Sum(r => (r.X86 ? 0 : 1) + (r.X64 ? 0 : 1));
        RuntimeNote.Text = missing == 0 ? "Tudo instalado." : $"{missing} faltando. Jogos que precisam de uma biblioteca que falta costumam avisar \"MSVCP…dll\" ou \"d3dx9_…dll não encontrado\".";
    }

    private Border RuntimeBadge(string label, bool ok)
    {
        var b = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush(ok ? Color.FromArgb(0x26, 0x4E, 0xD1, 0x7A) : Color.FromArgb(0x26, 0xFF, 0x5C, 0x5C)),
            Child = new TextBlock { Text = $"{label}: {(ok ? "instalado" : "falta")}" } };
        System.Windows.Documents.TextElement.SetForeground(b, (Brush)FindResource(ok ? "ChartGpu" : "TempCrit"));
        return b;
    }

    private void BtnRuntimeCheck_Click(object sender, RoutedEventArgs e) => InitRuntimes();

    private bool _rtBusy;

    private void BtnRtVc_Click(object sender, RoutedEventArgs e) =>
        _ = InstallRuntimeAsync("Visual C++",
            "Baixar e instalar o Visual C++ AIO (abbodi1406/vcredist, GitHub)?\n\nInstala todas as versões de 2005 a 2022, 32 e 64 bits, e atualiza as que já existem. " +
            "O arquivo é conferido pelo SHA-256 publicado no GitHub antes de abrir.", RuntimeInstaller.InstallVisualCppAsync);

    private void BtnRtDx_Click(object sender, RoutedEventArgs e) =>
        _ = InstallRuntimeAsync("DirectX",
            "Baixar e instalar o DirectX End-User Runtime (junho de 2010) do site da Microsoft (cerca de 96 MB)?\n\n" +
            "Só acrescenta as bibliotecas antigas que jogos DirectX 9, 10 e 11 pedem; o DirectX 12 do Windows não muda.", RuntimeInstaller.InstallDirectXAsync);

    private void BtnRtPage_Click(object sender, RoutedEventArgs e) =>
        OpenUrl((sender as FrameworkElement)?.Tag as string == "dx" ? RuntimeInstaller.DxPage : RuntimeInstaller.VcRepoPage);

    private async Task InstallRuntimeAsync(string what, string question, Func<Action<string, double>, CancellationToken, Task<string>> install)
    {
        if (_rtBusy || !RequireAdmin()) return;
        if (MessageBox.Show(this, question, what, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _rtBusy = true;
        BtnRtVc.IsEnabled = BtnRtDx.IsEnabled = false;
        RuntimeInstallStatus.Visibility = Visibility.Visible;
        RuntimeInstallStatus.Foreground = (Brush)FindResource("Accent");
        try
        {
            // O progresso chega de outra thread: só o texto mais recente vai para a tela.
            var progress = new Progress<(string Text, double Frac)>(p => RuntimeInstallStatus.Text = p.Text);
            IProgress<(string, double)> report = progress;
            string result = await Task.Run(() => install((t, f) => report.Report((t, f)), CancellationToken.None));
            RuntimeInstallStatus.Text = result;
        }
        catch (Exception ex)
        {
            RuntimeInstallStatus.Text = $"{what}: falhou ({ex.Message}). Use a página oficial ao lado.";
            RuntimeInstallStatus.Foreground = (Brush)FindResource("TempCrit");
        }
        finally
        {
            _rtBusy = false;
            BtnRtVc.IsEnabled = BtnRtDx.IsEnabled = true;
        }
        InitRuntimes();
    }
}
