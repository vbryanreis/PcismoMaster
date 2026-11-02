using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ResetExplode;

/// <summary>
/// Aba OTIMIZAÇÃO · Desinstalar Apps: lista os programas instalados (só aplicativos de verdade), roda o desinstalador
/// oficial e depois oferece remover sobras (pastas e chaves de registro) ligadas ao app, sempre com confirmação e com
/// as guardas de segurança do <see cref="Uninstaller"/>.
/// </summary>
public partial class MainWindow
{
    private List<InstalledProgram>? _programs;
    private bool _unBusy;
    private List<LeftoverRow> _leftovers = new();

    /// <summary>Linha de sobra com a marcação do usuário.</summary>
    public sealed class LeftoverRow
    {
        public required Leftover Item { get; init; }
        public bool Selected { get; set; } = true;
        public string Kind => Item.Kind;
        public string Display => Item.Display;
        public string SizeText => Item.SizeText;
    }

    private void InitUninstall()
    {
        if (_unBusy || _programs != null) return; // carrega uma vez; ATUALIZAR LISTA recarrega
        _ = LoadProgramsAsync();
    }

    private async Task LoadProgramsAsync()
    {
        UnStatus.Text = "Lendo os programas instalados…";
        try
        {
            _programs = await Task.Run(ProgramInventory.Read);
            UninstallList.ItemsSource = _programs;
            CollectionViewSource.GetDefaultView(_programs).Filter = ProgramFilter;
            UnStatus.Text = $"{_programs.Count} programas.";
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

    private void BtnUnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_unBusy) return;
        _programs = null;
        UnLeftoverCard.Visibility = Visibility.Collapsed;
        _ = LoadProgramsAsync();
    }

    private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_unBusy) return;
        if (UninstallList.SelectedItem is not InstalledProgram p) { UnStatus.Text = "Selecione um programa na lista."; return; }

        var r = MessageBox.Show(
            $"Desinstalar \"{p.Name}\"?\n\nVou abrir o desinstalador oficial do programa. Conclua por ele; depois eu procuro sobras para você revisar.",
            "Desinstalar Apps", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        _unBusy = true; BtnUninstall.IsEnabled = false; UnLeftoverCard.Visibility = Visibility.Collapsed;
        UnStatus.Text = $"Desinstalando {p.Name}… conclua no desinstalador do programa.";
        try
        {
            await Uninstaller.RunOfficialAsync(p, CancellationToken.None);
            var leftovers = await Task.Run(() => Uninstaller.ScanLeftovers(p));
            await LoadProgramsAsync(); // o programa pode ter saído da lista

            if (leftovers.Count == 0)
            {
                UnStatus.Text = $"{p.Name}: desinstalado. Nenhuma sobra encontrada.";
            }
            else
            {
                _leftovers = leftovers.Select(l => new LeftoverRow { Item = l }).ToList();
                UnLeftoverList.ItemsSource = _leftovers;
                UnLeftoverTitle.Text = $"Sobras de {p.Name}  ({_leftovers.Count})";
                UnLeftoverStatus.Text = "";
                UnLeftoverCard.Visibility = Visibility.Visible;
                UnStatus.Text = $"{p.Name}: desinstalado. {_leftovers.Count} sobra(s) encontrada(s) — revise abaixo.";
            }
        }
        catch (Exception ex) { UnStatus.Text = $"Erro ao desinstalar: {ex.Message}"; }
        finally { _unBusy = false; BtnUninstall.IsEnabled = true; }
    }

    private async void BtnUnRemoveLeftovers_Click(object sender, RoutedEventArgs e)
    {
        var sel = _leftovers.Where(l => l.Selected).ToList();
        if (sel.Count == 0) { UnLeftoverStatus.Text = "Nada marcado."; return; }

        var r = MessageBox.Show($"Remover {sel.Count} item(ns) marcado(s)? Esta ação não tem desfazer.",
                                "Remover sobras", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        UnLeftoverStatus.Text = "Removendo…";
        var (ok, fail) = await Task.Run(() => Uninstaller.DeleteLeftovers(sel.Select(l => l.Item)));
        UnLeftoverStatus.Text = $"{ok} removida(s)" + (fail > 0 ? $" · {fail} não puderam ser removidas." : ".");

        _leftovers = _leftovers.Where(l => !l.Selected).ToList();
        UnLeftoverList.ItemsSource = null;
        UnLeftoverList.ItemsSource = _leftovers;
        if (_leftovers.Count == 0) UnLeftoverCard.Visibility = Visibility.Collapsed;
    }

    private void BtnUnCloseLeftovers_Click(object sender, RoutedEventArgs e) => UnLeftoverCard.Visibility = Visibility.Collapsed;
}
