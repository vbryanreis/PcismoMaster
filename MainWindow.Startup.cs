using System.Windows;
using System.Windows.Controls;

namespace ResetExplode;

/// <summary>Aba FERRAMENTAS · Inicialização: programas que abrem junto com o Windows.</summary>
public partial class MainWindow
{
    private int _startupScan;

    /// <summary>Lê as entradas fora da thread da tela (registro, pastas de inicialização e tarefas); a última leitura pedida é a que vale.</summary>
    private async void ScanStartup()
    {
        int id = ++_startupScan;
        List<StartupEntry> list;
        try { list = await Task.Run(StartupManager.List); }
        catch (Exception ex) { StartupResult.Text = $"Não foi possível listar: {ex.Message}"; return; }
        if (id != _startupScan) return;

        StartupList.ItemsSource = list;
        int on = list.Count(e => e.Enabled);
        StartupSummary.Subtitle = list.Count == 0 ? "Nenhum programa abre junto com o Windows"
                            : $"{list.Count} programas · {on} ativados · {list.Count - on} desativados";
        BtnStartupRestore.IsEnabled = _settings.StartupRemoved.Count > 0;
        BtnStartupRestore.Content = _settings.StartupRemoved.Count > 0
            ? $"RESTAURAR REMOVIDOS ({_settings.StartupRemoved.Count})" : "RESTAURAR REMOVIDOS";
    }

    private void BtnStartupScan_Click(object sender, RoutedEventArgs e)
    {
        StartupResult.Text = "";
        ScanStartup();
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: StartupEntry item } box) return;
        bool turnOn = box.IsChecked == true;
        if (item.Machine && !RequireAdmin()) { box.IsChecked = item.Enabled; return; }
        try
        {
            StartupManager.SetEnabled(item, turnOn);
            StartupResult.Text = $"{item.Name}: {(turnOn ? "ativado" : "desativado")}. Vale na próxima inicialização do Windows.";
        }
        catch (Exception ex) { StartupResult.Text = $"{item.Name}: falhou ({ex.Message})."; }
        ScanStartup();
    }

    private void StartupRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: StartupEntry item }) return;
        if (item.Machine && !RequireAdmin()) return;
        var r = MessageBox.Show($"Remover \"{item.Name}\" da inicialização do Windows?\n\n{item.Command}\n\n" +
                                "O programa continua instalado, só deixa de abrir sozinho. " +
                                "Dá para desfazer em \"Restaurar removidos\".\n\n" +
                                "Se só quiser testar, prefira desligar o interruptor.",
                                "Inicialização", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            _settings.StartupRemoved.Add(StartupManager.Remove(item));
            _settings.Save();
            StartupResult.Text = $"{item.Name} removido da inicialização.";
        }
        catch (Exception ex) { StartupResult.Text = $"{item.Name}: não foi possível remover ({ex.Message})."; }
        ScanStartup();
    }

    private void StartupOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: StartupEntry item }) return;
        string? path = StartupFile(item);
        if (path == null) { StartupResult.Text = $"{item.Name}: o arquivo não existe mais, não há local para abrir."; return; }
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); }
        catch (Exception ex) { StartupResult.Text = $"{item.Name}: não deu para abrir o local ({ex.Message})."; }
    }

    /// <summary>O arquivo de um item: o atalho/arquivo da pasta Inicializar, ou o executável do comando (com ou sem aspas).</summary>
    private static string? StartupFile(StartupEntry item)
    {
        if (item.FilePath != null && System.IO.File.Exists(item.FilePath)) return item.FilePath;
        string cmd = Environment.ExpandEnvironmentVariables(item.Command.Trim());
        if (cmd.StartsWith('"')) { int end = cmd.IndexOf('"', 1); return end > 1 && System.IO.File.Exists(cmd[1..end]) ? cmd[1..end] : null; }
        for (int i = cmd.Length; i > 0; i = cmd.LastIndexOf(' ', i - 1))
        {
            string c = cmd[..i];
            if (System.IO.File.Exists(c)) return c;
            if (!c.Contains(' ')) break;
        }
        return null;
    }

    private void BtnStartupRestore_Click(object sender, RoutedEventArgs e)
    {
        var removed = _settings.StartupRemoved;
        if (removed.Count == 0) return;
        if (removed.Any(b => b.Machine) && !RequireAdmin()) return;
        string names = string.Join("\n", removed.Take(12).Select(b => $"  • {b.Name}"))
                       + (removed.Count > 12 ? $"\n  … e mais {removed.Count - 12}" : "");
        var r = MessageBox.Show($"Devolver {removed.Count} programas à inicialização do Windows?\n\n{names}",
                                "Inicialização", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        var failed = new List<string>();
        foreach (var b in removed.ToList())
        {
            try { StartupManager.Restore(b); removed.Remove(b); }
            catch (Exception ex) { failed.Add($"{b.Name} ({ex.Message})"); }
        }
        _settings.Save();
        StartupResult.Text = failed.Count == 0 ? "Programas restaurados." : $"Não restaurados: {string.Join("; ", failed)}";
        ScanStartup();
    }
}
