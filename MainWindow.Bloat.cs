using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// TWEAKING · Windows · Bloatware app por app: lista o que o "13 Bloatware" removeria e deixa escolher item a item,
/// com selo de risco, confirmação, ponto de restauração e um jeito de voltar cada um (Store, recurso ou script de reinstalar).
/// Só lê o PC quando a pessoa clica em VER O QUE ESTÁ INSTALADO.
/// </summary>
public partial class MainWindow
{
    private sealed record RemovedBloat(BloatKind Kind, string Id, string Name, string? Family, DateTime When);

    private List<BloatItem> _bloat = new();
    private readonly Dictionary<string, CheckBox> _bloatChecks = new();
    private StackPanel? _bloatList;
    private TextBox? _bloatSearch;
    private CheckBox? _bloatShowLocked;
    private Button? _bloatRemoveBtn;
    private TextBlock? _bloatStatus;

    private static string BloatRemovedFile => Path.Combine(TweakBackupDir, "bloat-removidos.json");

    private static List<RemovedBloat> LoadRemovedBloat()
    {
        try { return File.Exists(BloatRemovedFile) ? JsonSerializer.Deserialize<List<RemovedBloat>>(File.ReadAllText(BloatRemovedFile)) ?? new() : new(); }
        catch { return new(); }
    }

    private static void SaveRemovedBloat(List<RemovedBloat> list)
    {
        Directory.CreateDirectory(TweakBackupDir);
        File.WriteAllText(BloatRemovedFile, JsonSerializer.Serialize(list));
    }

    private void AddBloatSection(Panel host)
    {
        TweakCard(host, "BLOATWARE · ESCOLHER APP POR APP",
            "A mesma lista do script Bloatware, só que você escolhe o que sai. Cada item mostra o que é e um selo: SEGURO (pode tirar sem medo), " +
            "ATENÇÃO (algum app ou recurso pode depender dele) e PERIGOSO (pode quebrar login, Iniciar ou o Windows). Os que o script mantém " +
            "aparecem travados. Tudo que sair por aqui fica em REMOVIDOS POR AQUI, com o botão para voltar.",
            out var body);
        _bloatStatus = TweakStatusText();
        var load = TweakButton("VER O QUE ESTÁ INSTALADO", true, async (s, _) =>
        {
            ((Button)s).IsEnabled = false;
            await LoadBloatAsync();
            ((Button)s).Content = "LER DE NOVO";
            ((Button)s).IsEnabled = true;
        });
        body.Children.Add(load);
        body.Children.Add(_bloatStatus);

        var tools = new WrapPanel { Margin = new Thickness(0, 12, 0, 6), Visibility = Visibility.Collapsed };
        _bloatSearch = new TextBox { Width = 260, Margin = new Thickness(0, 0, 12, 8), ToolTip = "Procurar pelo nome" };
        _bloatSearch.TextChanged += (_, _) => FilterBloat();
        _bloatShowLocked = new CheckBox { Content = "Mostrar os travados", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8) };
        _bloatShowLocked.Checked += (_, _) => FilterBloat();
        _bloatShowLocked.Unchecked += (_, _) => FilterBloat();
        tools.Children.Add(new TextBlock { Text = "Procurar:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8) });
        tools.Children.Add(_bloatSearch);
        tools.Children.Add(_bloatShowLocked);
        tools.Children.Add(TweakButton("MARCAR OS SEGUROS", false, (_, _) =>
        {
            foreach (var i in _bloat.Where(i => i.Essential == null && i.Risk == ScriptRisk.Seguro && i.Kind == BloatKind.App))
                if (_bloatChecks.TryGetValue(BloatKey(i), out var c) && c.Visibility == Visibility.Visible) c.IsChecked = true;
        }));
        tools.Children.Add(TweakButton("DESMARCAR TUDO", false, (_, _) => { foreach (var c in _bloatChecks.Values) c.IsChecked = false; }));
        body.Children.Add(tools);

        _bloatList = new StackPanel();
        body.Children.Add(_bloatList);

        _bloatRemoveBtn = TweakButton("REMOVER OS MARCADOS", true, async (_, _) => await RemoveCheckedBloatAsync());
        _bloatRemoveBtn.Visibility = Visibility.Collapsed;
        body.Children.Add(_bloatRemoveBtn);

        // Saída própria (a mesma execução aparece também na seção de scripts).
        var log = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 11, Height = 130, Margin = new Thickness(0, 10, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, Text = "O que for removido aparece aqui.",
            Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Muted"), BorderThickness = new Thickness(0) };
        var stop = TweakButton("PARAR", false, (_, _) => StopScript());
        stop.IsEnabled = _scriptProc != null;
        body.Children.Add(log);
        body.Children.Add(stop);
        _scriptOutputs.Add((log, stop));

        tools.Tag = "tools";
        _bloatTools = tools;
    }

    private WrapPanel? _bloatTools;

    private static string BloatKey(BloatItem i) => i.Kind + "|" + i.Id;

    private async Task LoadBloatAsync()
    {
        if (_bloatStatus == null || _bloatList == null) return;
        _bloatStatus.Text = "Lendo apps, recursos e programas deste PC (leva uns segundos; nada é mudado)…";
        try
        {
            string json = await Task.Run(async () =>
            {
                Directory.CreateDirectory(ScriptDir);
                string file = Path.Combine(ScriptDir, "bloat-ler.ps1");
                await File.WriteAllTextAsync(file, Bloatware.ListScript, new System.Text.UTF8Encoding(true));
                try
                {
                    var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                        $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{file}\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var p = Process.Start(psi)!;
                    string output = await p.StandardOutput.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    return output.Trim();
                }
                finally { try { File.Delete(file); } catch { } }
            });
            if (!json.StartsWith('{')) throw new InvalidOperationException("o PowerShell não devolveu a lista");
            _bloat = await Task.Run(() => Bloatware.Parse(json));
        }
        catch (Exception ex) { _bloatStatus.Text = "Não deu para ler: " + ex.Message; return; }
        BuildBloatRows();
    }

    private void BuildBloatRows()
    {
        if (_bloatList == null || _bloatStatus == null) return;
        _bloatList.Children.Clear();
        _bloatChecks.Clear();
        var removed = LoadRemovedBloat();
        if (removed.Count > 0)
        {
            _bloatList.Children.Add(new TextBlock { Text = "REMOVIDOS POR AQUI", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 14, 0, 6) });
            foreach (var r in removed.OrderByDescending(r => r.When)) _bloatList.Children.Add(RemovedRow(r));
        }
        foreach (var group in _bloat.GroupBy(i => i.Kind))
        {
            int free = group.Count(i => i.Essential == null);
            _bloatList.Children.Add(new TextBlock { Text = $"{Bloatware.KindTitle(group.Key)} · {free}", Style = (Style)FindResource("Label"), Margin = new Thickness(0, 16, 0, 6), Tag = group.Key });
            foreach (var i in group) _bloatList.Children.Add(BloatRow(i));
        }
        int total = _bloat.Count(i => i.Essential == null);
        _bloatStatus.Text = $"{total} itens podem sair, {_bloat.Count - total} ficam travados (o script também mantém). Marque o que quer tirar e clique em REMOVER OS MARCADOS.";
        if (_bloatTools != null) _bloatTools.Visibility = Visibility.Visible;
        if (_bloatRemoveBtn != null) _bloatRemoveBtn.Visibility = Visibility.Visible;
        FilterBloat();
        UpdateBloatCount();
    }

    private FrameworkElement BloatRow(BloatItem i)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6), Tag = i };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var check = new CheckBox { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0), IsEnabled = i.Essential == null };
        check.Checked += (_, _) => UpdateBloatCount();
        check.Unchecked += (_, _) => UpdateBloatCount();
        _bloatChecks[BloatKey(i)] = check;
        var text = new StackPanel();
        var head = new WrapPanel();
        head.Children.Add(new TextBlock { Text = i.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        if (i.Essential == null) head.Children.Add(RiskBadge(i.Risk));
        else head.Children.Add(new TextBlock { Text = "🔒 mantido: " + i.Essential, Foreground = (Brush)FindResource("Faint"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        text.Children.Add(head);
        string sub = (i.Description.Length > 0 ? i.Description + "  ·  " : "") + i.Id.Split('~')[0];
        text.Children.Add(new TextBlock { Text = sub, Foreground = (Brush)FindResource("Muted"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        text.MouseLeftButtonUp += (_, _) => { if (check.IsEnabled) check.IsChecked = check.IsChecked != true; };
        Grid.SetColumn(text, 1);
        grid.Children.Add(check);
        grid.Children.Add(text);
        return grid;
    }

    private FrameworkElement RemovedRow(RemovedBloat r)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new TextBlock { Text = $"{r.Name}  ·  {r.When:dd/MM HH:mm}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8), MinWidth = 280 });
        string? reinstallScript = r.Kind == BloatKind.Programa ? Bloatware.ProgramReinstall(r.Id) : null;
        if (r.Kind is BloatKind.App or BloatKind.Biblioteca)
            row.Children.Add(TweakButton("REINSTALAR PELA STORE", false, (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo($"ms-windows-store://pdp/?PFN={r.Family}") { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "Não deu para abrir a Store: " + ex.Message, "Bloatware"); }
            }));
        else if (r.Kind is BloatKind.Recurso or BloatKind.RecursoAntigo)
            row.Children.Add(TweakButton("VOLTAR", false, async (_, _) => await RestoreBloatAsync(r)));
        else if (reinstallScript != null)
            row.Children.Add(TweakButton("REINSTALAR", false, (_, _) =>
            { if (RequireAdmin()) _ = RunPowerShellTextAsync("Bloatware · reinstalar " + r.Name, "bloat-reinstalar", reinstallScript, false); }));
        else row.Children.Add(new TextBlock { Text = "sem volta automática (o próprio programa reinstala quando precisar)", Foreground = (Brush)FindResource("Faint"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
        row.Children.Add(TweakButton("TIRAR DA LISTA", false, (_, _) =>
        {
            var list = LoadRemovedBloat();
            list.RemoveAll(x => x.Kind == r.Kind && x.Id == r.Id);
            SaveRemovedBloat(list);
            BuildBloatRows();
        }));
        return row;
    }

    private void FilterBloat()
    {
        if (_bloatList == null) return;
        string q = _bloatSearch?.Text.Trim() ?? "";
        bool locked = _bloatShowLocked?.IsChecked == true;
        foreach (var child in _bloatList.Children.OfType<Grid>())
        {
            if (child.Tag is not BloatItem i) continue;
            // Modo simples: os PERIGOSOS e as bibliotecas não aparecem (e são desmarcados).
            bool allowed = Advanced || (i.Risk != ScriptRisk.Perigoso && i.Kind != BloatKind.Biblioteca);
            bool match = allowed && (locked || i.Essential == null) &&
                         (q.Length == 0 || i.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || i.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
            child.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            if (_bloatChecks.TryGetValue(BloatKey(i), out var c))
            {
                c.Visibility = child.Visibility;
                if (!allowed) c.IsChecked = false;
            }
        }
    }

    private List<BloatItem> CheckedBloat() => _bloat.Where(i => i.Essential == null && _bloatChecks.TryGetValue(BloatKey(i), out var c) && c.IsChecked == true).ToList();

    private void UpdateBloatCount()
    {
        if (_bloatRemoveBtn == null) return;
        int n = CheckedBloat().Count;
        _bloatRemoveBtn.Content = n == 0 ? "REMOVER OS MARCADOS" : $"REMOVER {n} ITEM(NS)";
        _bloatRemoveBtn.IsEnabled = n > 0;
    }

    private async Task RemoveCheckedBloatAsync()
    {
        var items = CheckedBloat();
        if (items.Count == 0 || !RequireAdmin()) return;
        var risk = items.Max(i => i.Risk);
        var warnings = new List<string>();
        foreach (var i in items.Where(i => i.Risk == ScriptRisk.Perigoso)) warnings.Add($"{i.Name}: {(i.Description.Length > 0 ? i.Description : "parte do Windows")} Pode quebrar login, Iniciar ou o Windows.");
        foreach (var i in items.Where(i => i.Risk == ScriptRisk.Atencao).Take(12)) warnings.Add($"{i.Name}: {(i.Description.Length > 0 ? i.Description : "algum recurso pode depender dele.")}");
        if (items.Any(i => i.Kind is BloatKind.App or BloatKind.Biblioteca)) warnings.Add("Apps removidos voltam pela Microsoft Store (botão REINSTALAR PELA STORE em Removidos por aqui).");
        if (items.Any(i => i.Kind is BloatKind.Recurso or BloatKind.RecursoAntigo)) warnings.Add("Recursos voltam pelo botão VOLTAR; os opcionais são baixados de novo do Windows Update. Alguns só valem depois de reiniciar.");
        if (items.Any(i => i.Kind == BloatKind.Programa && !Bloatware.Programs.First(p => p.Id == i.Id).CanReinstall))
            warnings.Add("GameInput, BRLTTY e as ferramentas de atualização não têm volta automática.");

        bool? rp = ConfirmRisk($"Remover {items.Count} item(ns)", string.Join(", ", items.Take(25).Select(i => i.Name)) + (items.Count > 25 ? $" e mais {items.Count - 25}." : "."),
            risk, warnings, () => Bloatware.BatchScript(items, restore: false), "REMOVER");
        if (rp == null) return;

        _bloatRemoveBtn!.IsEnabled = false;
        await RunPowerShellTextAsync($"Bloatware · remover {items.Count}", "bloat-remover", Bloatware.BatchScript(items, restore: false), rp.Value);
        var list = LoadRemovedBloat();
        foreach (var i in items)
        {
            list.RemoveAll(x => x.Kind == i.Kind && x.Id == i.Id);
            list.Add(new RemovedBloat(i.Kind, i.Id, i.Name, i.Family, DateTime.Now));
        }
        SaveRemovedBloat(list);
        await LoadBloatAsync();
        // O que não saiu de verdade continua na lista lida agora: tira do "removidos".
        var still = _bloat.Select(BloatKey).ToHashSet();
        list.RemoveAll(x => still.Contains(x.Kind + "|" + x.Id));
        SaveRemovedBloat(list);
        BuildBloatRows();
    }

    private async Task RestoreBloatAsync(RemovedBloat r)
    {
        if (!RequireAdmin()) return;
        var item = r.Kind == BloatKind.Recurso ? Bloatware.Capability(r.Id) : Bloatware.Feature(r.Id);
        if (MessageBox.Show(this, $"Voltar \"{r.Name}\"?" + (r.Kind == BloatKind.Recurso ? " Ele é baixado de novo do Windows Update." : " Pode pedir para reiniciar."),
                "Bloatware", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        if (!await RunPowerShellTextAsync("Bloatware · voltar " + r.Name, "bloat-voltar", Bloatware.BatchScript(new[] { item }, restore: true), false)) return;
        var list = LoadRemovedBloat();
        list.RemoveAll(x => x.Kind == r.Kind && x.Id == r.Id);
        SaveRemovedBloat(list);
        await LoadBloatAsync();
    }

    /// <summary>Confirmação com selo e avisos (mesma cara da dos scripts). Devolve se cria ponto de restauração, ou null se cancelou.</summary>
    private bool? ConfirmRisk(string title, string description, ScriptRisk risk, IList<string> warnings, Func<string> script, string okText)
    {
        var root = new StackPanel { Margin = new Thickness(22) };
        var head = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        head.Children.Add(RiskBadge(risk));
        head.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(head);
        root.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 12) });
        if (warnings.Count > 0)
        {
            var box = new StackPanel();
            foreach (var w in warnings)
                box.Children.Add(new TextBlock { Text = "• " + w, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
                    Foreground = risk == ScriptRisk.Seguro ? (Brush)FindResource("Fg") : RiskBrush(risk) });
            root.Children.Add(new Border { BorderBrush = RiskBrush(risk), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 4), Margin = new Thickness(0, 0, 0, 12),
                Child = new ScrollViewer { MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = box } });
        }
        var rp = new CheckBox { Content = "Criar um ponto de restauração antes", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
        root.Children.Add(rp);
        var agree = new CheckBox { Content = "Li os avisos e assumo o risco", Margin = new Thickness(0, 0, 0, 14), FontWeight = FontWeights.SemiBold };
        if (risk == ScriptRisk.Perigoso) root.Children.Add(agree);

        var win = new Window
        {
            Title = title, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Height, Width = 580, Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Fg"), ShowInTaskbar = false,
        };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var ok = TweakButton(okText, true, (_, _) => win.DialogResult = true);
        ok.IsEnabled = risk != ScriptRisk.Perigoso;
        agree.Checked += (_, _) => ok.IsEnabled = true;
        agree.Unchecked += (_, _) => ok.IsEnabled = false;
        buttons.Children.Add(TweakButton("VER OS COMANDOS", false, (_, _) =>
            new Window { Title = "Comandos", Owner = win, Width = 820, Height = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("Bg"),
                Content = new TextBox { Text = script(), IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Fg") } }.Show()));
        buttons.Children.Add(TweakButton("CANCELAR", false, (_, _) => win.DialogResult = false));
        buttons.Children.Add(ok);
        root.Children.Add(buttons);
        win.Content = root;
        return win.ShowDialog() == true ? rp.IsChecked == true : null;
    }
}
