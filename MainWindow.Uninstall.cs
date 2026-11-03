using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>
/// Aba FERRAMENTAS · Desinstalar Apps: lista os programas instalados (só aplicativos de verdade, com o ícone de cada um),
/// roda o desinstalador oficial e depois faz a varredura profunda de sobras do <see cref="Uninstaller"/> (no estilo do
/// Revo/Geek Uninstaller), sempre com confirmação, Lixeira e backup do registro e das tarefas.
/// </summary>
public partial class MainWindow
{
    private List<InstalledProgram>? _programs;
    private bool _unBusy;
    private List<LeftoverRow> _leftovers = new();
    private InstalledProgram? _unApp;
    private ScanMode _unMode = ScanMode.Moderado;
    private CancellationTokenSource? _unWaitCts;

    /// <summary>Linha de sobra com a marcação do usuário.</summary>
    public sealed class LeftoverRow : INotifyPropertyChanged
    {
        public required Leftover Item { get; init; }
        private bool _selected;
        public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; OnChanged(); } }
        public string Kind => Item.Kind;
        public string Display => Item.Display;
        public string SizeText => Item.SizeText;
        public string Group => Item.Group;
        public bool Strong => Item.Strong;
        public string Info => string.Join("  ·  ", new[] { Item.Detail, Item.Reason }.Where(s => s.Length > 0));

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private void InitUninstall()
    {
        UpdateUnModeHint();
        if (_unBusy || _programs != null) return; // carrega uma vez; ATUALIZAR LISTA recarrega
        _ = LoadProgramsAsync();
    }

    private async Task LoadProgramsAsync()
    {
        UnStatus.Text = "Lendo os programas instalados…";
        try
        {
            var list = await Task.Run(ProgramInventory.Read);
            _programs = list;
            UninstallList.ItemsSource = list;
            CollectionViewSource.GetDefaultView(list).Filter = ProgramFilter;
            UnStatus.Text = $"{list.Count} programas.";
            // Ícones depois, fora da thread da UI (o DisplayIcon de cada programa)
            var icons = await Task.Run(() => list.Select(p => (p, icon: AppIcons.FromFile(p.DisplayIcon, 32))).ToList());
            if (!ReferenceEquals(_programs, list)) return; // a lista foi recarregada nesse meio-tempo
            foreach (var (p, icon) in icons) if (icon != null) p.Icon = icon;
        }
        catch (Exception ex) { UnStatus.Text = $"Falha ao ler os programas: {ex.Message}"; }
    }

    private bool ProgramFilter(object o)
    {
        string q = UnSearch.Text.Trim();
        if (q.Length == 0) return true;
        var p = (InstalledProgram)o;
        return p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || p.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void UnSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_programs != null) CollectionViewSource.GetDefaultView(_programs).Refresh();
    }

    private void UnMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse(tag, out ScanMode m)) _unMode = m;
        if (UnModeHint != null) UpdateUnModeHint();
    }

    private void UpdateUnModeHint() => UnModeHint.Text = _unMode switch
    {
        ScanMode.Seguro => "Seguro: só pastas e chaves com o nome do app (Program Files, AppData, ProgramData, Menu Iniciar) e a pasta de instalação.",
        ScanMode.Moderado => "Moderado (recomendado): o Seguro + Documentos, perfil do usuário, Temp, arquivos soltos, atalhos, App Paths, entradas de inicialização e tarefas agendadas do app.",
        _ => "Avançado: o Moderado + o que só bate com o nome do fabricante ou é parecido. Esses itens vêm desmarcados com CONFIRA: abra antes de remover.",
    };

    private void BtnUnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_unBusy) return;
        _programs = null;
        UnLeftoverCard.Visibility = Visibility.Collapsed;
        _ = LoadProgramsAsync();
    }

    private void SetUnBusy(bool busy)
    {
        _unBusy = busy;
        BtnUninstall.IsEnabled = BtnUnForce.IsEnabled = BtnUnRefresh.IsEnabled = !busy;
    }

    private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_unBusy) return;
        if (UninstallList.SelectedItem is not InstalledProgram p) { UnStatus.Text = "Selecione um programa na lista."; return; }

        var r = MessageBox.Show(
            $"Desinstalar \"{p.Name}\"?\n\nVou abrir o desinstalador oficial do programa. Conclua por ele; depois eu procuro sobras " +
            $"(varredura {ModeName(_unMode)}) para você revisar.",
            "Desinstalar Apps", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        SetUnBusy(true); UnLeftoverCard.Visibility = Visibility.Collapsed;
        try
        {
            await MaybeRestorePointAsync(p);
            UnStatus.Text = $"Desinstalando {p.Name}… conclua no desinstalador do programa.";
            bool started = await Uninstaller.RunOfficialAsync(p, CancellationToken.None);
            if (!started) UnStatus.Text = "O desinstalador não abriu. Use REMOÇÃO FORÇADA para limpar o que sobrou.";

            // Desinstaladores que se copiam para o Temp: espera a entrada sumir ou o usuário avisar que terminou.
            _unWaitCts = new CancellationTokenSource();
            BtnUnScanNow.Visibility = Visibility.Visible;
            if (Uninstaller.UninstallKeyExists(p))
                UnStatus.Text = $"Esperando o desinstalador de {p.Name} terminar… Quando acabar, clique em JÁ TERMINEI.";
            await Uninstaller.WaitUninstallFinishedAsync(p, TimeSpan.FromMinutes(10), _unWaitCts.Token);
            BtnUnScanNow.Visibility = Visibility.Collapsed;

            await ScanAndShowAsync(p, forced: false);
            await LoadProgramsAsync(); // o programa pode ter saído da lista
        }
        catch (Exception ex) { UnStatus.Text = $"Erro ao desinstalar: {ex.Message}"; }
        finally { BtnUnScanNow.Visibility = Visibility.Collapsed; _unWaitCts?.Dispose(); _unWaitCts = null; SetUnBusy(false); }
    }

    private async void BtnUnForce_Click(object sender, RoutedEventArgs e)
    {
        if (_unBusy) return;
        if (UninstallList.SelectedItem is not InstalledProgram p) { UnStatus.Text = "Selecione um programa na lista."; return; }
        var r = MessageBox.Show(
            $"Remoção forçada de \"{p.Name}\"?\n\nO desinstalador do programa NÃO será aberto. Vou procurar tudo o que for dele " +
            "(pastas, registro, inicialização, tarefas e a entrada em Programas e Recursos) para você revisar e remover.\n\n" +
            "Use quando o desinstalador sumiu ou dá erro. Feche o programa antes.",
            "Remoção forçada", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        SetUnBusy(true); UnLeftoverCard.Visibility = Visibility.Collapsed;
        try
        {
            await MaybeRestorePointAsync(p);
            await ScanAndShowAsync(p, forced: true);
        }
        catch (Exception ex) { UnStatus.Text = $"Erro na varredura: {ex.Message}"; }
        finally { SetUnBusy(false); }
    }

    private void BtnUnScanNow_Click(object sender, RoutedEventArgs e) => _unWaitCts?.Cancel();

    private async Task MaybeRestorePointAsync(InstalledProgram p)
    {
        if (UnRestorePoint.IsChecked != true) return;
        UnStatus.Text = "Criando ponto de restauração do Windows…";
        UnStatus.Text = await Uninstaller.TryRestorePointAsync(p.Name);
    }

    private async Task ScanAndShowAsync(InstalledProgram p, bool forced)
    {
        UnStatus.Text = $"Procurando sobras de {p.Name} (varredura {ModeName(_unMode)})…";
        var mode = _unMode;
        var leftovers = await Task.Run(() => Uninstaller.ScanLeftovers(p, mode, includeUninstallEntry: forced));
        _unApp = p;
        if (leftovers.Count == 0)
        {
            UnStatus.Text = $"{p.Name}: nenhuma sobra encontrada.";
            return;
        }
        // Vem marcado só o que tem ligação clara; pasta muito grande (ex.: biblioteca de jogos) vem desmarcada para conferir.
        _leftovers = leftovers.Select(l => new LeftoverRow { Item = l, Selected = l.Strong && l.SizeBytes < 5L << 30 }).ToList();
        ShowLeftovers();
        UnStatus.Text = $"{p.Name}: {_leftovers.Count} sobra(s) encontrada(s) — revise abaixo.";
    }

    private static string ModeName(ScanMode m) => m switch { ScanMode.Seguro => "segura", ScanMode.Avancado => "avançada", _ => "moderada" };

    private void ShowLeftovers()
    {
        var view = new ListCollectionView(_leftovers);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LeftoverRow.Group)));
        UnLeftoverList.ItemsSource = view;
        UnLeftoverTitle.Text = $"Sobras de {_unApp?.Name}";
        long size = _leftovers.Where(l => l.Item.Kind == "Pasta" || l.Item.Kind == "Arquivo").Sum(l => l.Item.SizeBytes);
        int Count(string g) => _leftovers.Count(l => l.Group == g);
        UnLeftoverSummary.Text = $"{Count("Pastas e arquivos")} pasta(s)/arquivo(s)" +
                                 (size > 0 ? $" ({new Leftover("", "", size, null, default, default, null).SizeText})" : "") +
                                 $" · {Count("Registro")} chave(s) de registro · {Count("Inicialização e tarefas")} de inicialização/tarefas" +
                                 $" · {_leftovers.Count(l => !l.Strong)} para conferir";
        UnLeftoverStatus.Text = "";
        UnLeftoverCard.Visibility = Visibility.Visible;
    }

    private void BtnUnMarkAll_Click(object sender, RoutedEventArgs e) { foreach (var l in _leftovers) l.Selected = true; }
    private void BtnUnMarkStrong_Click(object sender, RoutedEventArgs e) { foreach (var l in _leftovers) l.Selected = l.Strong; }
    private void BtnUnMarkNone_Click(object sender, RoutedEventArgs e) { foreach (var l in _leftovers) l.Selected = false; }

    private async void BtnUnRemoveLeftovers_Click(object sender, RoutedEventArgs e)
    {
        var sel = _leftovers.Where(l => l.Selected).ToList();
        if (sel.Count == 0) { UnLeftoverStatus.Text = "Nada marcado."; return; }

        int files = sel.Count(l => l.Group == "Pastas e arquivos");
        int weak = sel.Count(l => !l.Strong);
        var r = MessageBox.Show(
            $"Remover {sel.Count} item(ns) marcado(s)?\n\n" +
            (files > 0 ? $"• {files} pasta(s)/arquivo(s) vão para a Lixeira.\n" : "") +
            (sel.Count - files > 0 ? $"• {sel.Count - files} item(ns) de registro/inicialização/tarefa saem com backup (RESTAURAR ÚLTIMA LIMPEZA devolve).\n" : "") +
            (weak > 0 ? $"\nAtenção: {weak} item(ns) marcado(s) têm CONFIRA (ligação fraca com o app)." : ""),
            "Remover sobras", MessageBoxButton.YesNo, weak > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        BtnUnRemove.IsEnabled = false;
        UnLeftoverStatus.Text = "Removendo…";
        try
        {
            string app = _unApp?.Name ?? "app";
            var res = await Task.Run(() => Uninstaller.DeleteLeftovers(sel.Select(l => l.Item), app));
            var left = await Task.Run(() => _leftovers.Where(l => Uninstaller.StillThere(l.Item)).ToList());
            UnLeftoverStatus.Text = $"{res.Ok} removida(s)" + (res.Recycled > 0 ? $" ({res.Recycled} na Lixeira)" : "") +
                                    (res.Fail > 0 ? $" · {res.Fail} não puderam ser removidas (em uso? feche o programa e tente de novo)." : ".") +
                                    (res.BackupDir.Length > 0 ? " Backup do registro/tarefas salvo." : "");
            _leftovers = left;
            if (_leftovers.Count == 0) { UnLeftoverCard.Visibility = Visibility.Collapsed; UnStatus.Text = $"{app}: limpeza concluída. {UnLeftoverStatus.Text}"; }
            else { string status = UnLeftoverStatus.Text; ShowLeftovers(); UnLeftoverStatus.Text = status; }
        }
        catch (Exception ex) { UnLeftoverStatus.Text = $"Erro: {ex.Message}"; }
        finally { BtnUnRemove.IsEnabled = true; }
    }

    private void BtnUnCloseLeftovers_Click(object sender, RoutedEventArgs e) => UnLeftoverCard.Visibility = Visibility.Collapsed;

    /// <summary>Abre o item para conferir: Explorador (selecionado), Editor do Registro na chave ou Agendador de Tarefas.</summary>
    private void UnLeftoverOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: LeftoverRow row }) return;
        var l = row.Item;
        try
        {
            switch (l.Kind)
            {
                case "Pasta" when l.FilePath != null:
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{l.FilePath}\"") { UseShellExecute = true });
                    break;
                case "Arquivo" or "Atalho" when l.FilePath != null:
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{l.FilePath}\"") { UseShellExecute = true });
                    break;
                case "Registro" or "Inicialização":
                    // O Regedit abre na última chave vista; aponta para a do item (só a preferência do próprio Regedit).
                    using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                        k.SetValue("LastKey", Uninstaller.RegeditPath(l));
                    Process.Start(new ProcessStartInfo("regedit.exe", "/m") { UseShellExecute = true });
                    break;
                case "Tarefa":
                    Process.Start(new ProcessStartInfo("taskschd.msc") { UseShellExecute = true });
                    break;
            }
        }
        catch (Exception ex) { UnLeftoverStatus.Text = $"Não foi possível abrir: {ex.Message}"; }
    }

    private async void BtnUnRestore_Click(object sender, RoutedEventArgs e)
    {
        string? dir = Uninstaller.LatestBackup();
        if (dir == null) { UnStatus.Text = "Nenhuma limpeza com backup ainda."; return; }
        var r = MessageBox.Show(
            $"Restaurar a última limpeza ({Path.GetFileName(dir)})?\n\nDevolve as chaves de registro, as entradas de inicialização e as " +
            "tarefas agendadas removidas. Pastas e arquivos voltam pela Lixeira do Windows (botão direito → Restaurar).",
            "Restaurar limpeza", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            var (ok, fail) = await Task.Run(() => Uninstaller.RestoreBackup(dir));
            UnStatus.Text = $"{ok} item(ns) restaurado(s)" + (fail > 0 ? $", {fail} falharam." : ".") + " Pastas e arquivos: restaure pela Lixeira.";
        }
        catch (Exception ex) { UnStatus.Text = $"Falha ao restaurar: {ex.Message}"; }
    }

    private void BtnUnBackups_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Uninstaller.BackupsRoot);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Uninstaller.BackupsRoot}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { UnStatus.Text = $"Não foi possível abrir: {ex.Message}"; }
    }
}
