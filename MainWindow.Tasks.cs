using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>Ferramentas › Tarefas agendadas: lista as tarefas do Agendador e deixa executar, parar, ligar, desligar e apagar.</summary>
public partial class MainWindow
{
    private sealed record SchedTask(string Path, string Name, string State, string Description, string Action, string Author)
    {
        public string Full => Path + Name;
        public bool Microsoft => Path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
    }

    private List<SchedTask> _tasks = new();
    private bool _tasksBusy, _tasksBuilt;
    private StackPanel? _tasksList;
    private TextBlock? _tasksStatus;
    private TextBox? _tasksSearch;
    private CheckBox? _tasksShowMs;

    private void InitTasks()
    {
        if (!_tasksBuilt)
        {
            _tasksBuilt = true;
            var host = TasksHost;
            TweakCard(host, "", "Tarefas agendadas", "Programas e scripts que o Windows roda sozinho em horários ou eventos (ao ligar, ao entrar, a cada hora). " +
                "Muitos atualizadores e telemetria ficam aqui. Desligar é seguro e dá para ligar de novo; apagar não tem volta.", out var body);
            var bar = new WrapPanel();
            _tasksSearch = new TextBox { Width = 240, Margin = new Thickness(0, 0, 10, 8), Style = TryFindResource("SearchBox") as Style, ToolTip = "Pesquisar pelo nome, pasta ou comando" };
            _tasksSearch.TextChanged += (_, _) => RenderTasks();
            _tasksShowMs = new CheckBox { Content = "Mostrar tarefas do Windows (\\Microsoft)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 8) };
            _tasksShowMs.Click += (_, _) => RenderTasks();
            bar.Children.Add(_tasksSearch);
            bar.Children.Add(_tasksShowMs);
            bar.Children.Add(TweakButton("ATUALIZAR", false, async (s, _) => await LoadTasksAsync()));
            body.Children.Add(bar);
            _tasksStatus = OptText("", "Faint", 11.5, 8);
            body.Children.Add(_tasksStatus);
            _tasksList = new StackPanel();
            host.Children.Add(_tasksList);
        }
        if (_tasks.Count == 0) _ = LoadTasksAsync();
    }

    private static async Task<(int Code, string Out)> RunPsAsync(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8;" + script)))
        { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        string err = (await e).Trim();
        return (p.ExitCode, (await o).Trim() + (err.Length > 0 ? "\n" + err : ""));
    }

    private static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";

    private async Task LoadTasksAsync()
    {
        if (_tasksBusy) return;
        _tasksBusy = true;
        if (_tasksStatus != null) _tasksStatus.Text = "Lendo as tarefas…";
        try
        {
            var (_, json) = await RunPsAsync("ConvertTo-Json -Compress -InputObject @(Get-ScheduledTask | ForEach-Object { [pscustomobject]@{ P=$_.TaskPath; N=$_.TaskName; S=[string]$_.State; " +
                "D=[string]$_.Description; A=(@($_.Actions | ForEach-Object { (($_.Execute, $_.Arguments) -join ' ').Trim() }) -join ' ; '); U=[string]$_.Author } })");
            int start = json.IndexOf('[');
            var list = new List<SchedTask>();
            if (start >= 0)
                foreach (var el in JsonDocument.Parse(json[start..(json.LastIndexOf(']') + 1)]).RootElement.EnumerateArray())
                    list.Add(new SchedTask(Str(el, "P"), Str(el, "N"), Str(el, "S"), Str(el, "D"), Str(el, "A"), Str(el, "U")));
            _tasks = list.OrderBy(t => t.Microsoft).ThenBy(t => t.Full, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) { if (_tasksStatus != null) _tasksStatus.Text = "Não deu para ler as tarefas: " + ex.Message; }
        finally { _tasksBusy = false; }
        RenderTasks();

        static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }

    private static string TaskStateText(string s) => s switch
    {
        "Ready" => "PRONTA", "Running" => "RODANDO", "Disabled" => "DESLIGADA", "Queued" => "NA FILA", _ => s.ToUpperInvariant(),
    };

    private void RenderTasks()
    {
        if (_tasksList == null) return;
        _tasksList.Children.Clear();
        string q = _tasksSearch?.Text.Trim() ?? "";
        bool ms = _tasksShowMs?.IsChecked == true;
        var shown = _tasks.Where(t => (ms || !t.Microsoft) && (q.Length == 0 || t.Full.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Action.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Description.Contains(q, StringComparison.OrdinalIgnoreCase))).Take(300).ToList();
        if (_tasksStatus != null && !_tasksBusy)
            _tasksStatus.Text = $"{_tasks.Count} tarefas no total · mostrando {shown.Count}" + (ms ? "" : " (as do Windows estão escondidas)") + (shown.Count == 300 ? " · refine a pesquisa para ver mais" : "");
        foreach (var t in shown)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel();
            var head = new DockPanel();
            var state = OptText(TaskStateText(t.State), t.State == "Disabled" ? "Faint" : "Muted", 9.5);
            state.FontWeight = FontWeights.Bold; state.Margin = new Thickness(10, 2, 0, 0);
            DockPanel.SetDock(state, Dock.Right);
            head.Children.Add(state);
            head.Children.Add(new TextBlock { Text = t.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            info.Children.Add(head);
            info.Children.Add(OptText(t.Path + (t.Author.Length > 0 ? "  ·  " + t.Author : ""), "Faint", 10.5));
            if (t.Description.Length > 0) { var d = OptText(t.Description, "Muted", 12); d.TextTrimming = TextTrimming.CharacterEllipsis; d.TextWrapping = TextWrapping.NoWrap; d.ToolTip = t.Description; info.Children.Add(d); }
            if (t.Action.Length > 0) { var a = OptText(t.Action, "Faint", 10.5); a.TextTrimming = TextTrimming.CharacterEllipsis; a.TextWrapping = TextWrapping.NoWrap; a.ToolTip = t.Action; info.Children.Add(a); }
            g.Children.Add(info);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            Button B(string text, string op) { var b = TweakButton(text, false, async (_, _) => await TaskActionAsync(t, op)); b.Padding = new Thickness(10, 5, 10, 5); b.FontSize = 11.5; b.Margin = new Thickness(6, 0, 0, 0); return b; }
            if (t.State == "Running") btns.Children.Add(B("PARAR", "stop"));
            else if (t.State != "Disabled") btns.Children.Add(B("EXECUTAR", "run"));
            btns.Children.Add(t.State == "Disabled" ? B("LIGAR", "enable") : B("DESLIGAR", "disable"));
            btns.Children.Add(B("APAGAR", "delete"));
            Grid.SetColumn(btns, 1);
            g.Children.Add(btns);
            _tasksList.Children.Add(new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16, 10, 16, 10), Child = g });
        }
    }

    private async Task TaskActionAsync(SchedTask t, string op)
    {
        if (_tasksBusy) return;
        if (!RequireAdmin()) return;
        string warnMs = t.Microsoft ? "\n\nATENÇÃO: é uma tarefa do próprio Windows. Desligar algumas (atualização, manutenção, Defender) pode atrapalhar o sistema." : "";
        string? ask = op switch
        {
            "disable" => $"Desligar a tarefa \"{t.Name}\"?\n\nEla deixa de rodar sozinha. Dá para ligar de novo aqui mesmo.{warnMs}",
            "delete" => $"APAGAR a tarefa \"{t.Name}\" ({t.Path})?\n\nIsso não tem volta: a tarefa some do Agendador. Se só quer que ela pare de rodar, prefira DESLIGAR.{warnMs}",
            "run" => $"Executar agora a tarefa \"{t.Name}\"?\n\n{t.Action}",
            "stop" => $"Parar a tarefa \"{t.Name}\" que está rodando?",
            _ => null,
        };
        if (ask != null && MessageBox.Show(ask, "Tarefas agendadas", MessageBoxButton.YesNo, op == "delete" ? MessageBoxImage.Warning : MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string args = $"-TaskPath {PsQuote(t.Path)} -TaskName {PsQuote(t.Name)}";
        string script = op switch
        {
            "run" => $"Start-ScheduledTask {args} -ErrorAction Stop",
            "stop" => $"Stop-ScheduledTask {args} -ErrorAction Stop",
            "enable" => $"Enable-ScheduledTask {args} -ErrorAction Stop | Out-Null",
            "disable" => $"Disable-ScheduledTask {args} -ErrorAction Stop | Out-Null",
            _ => $"Unregister-ScheduledTask {args} -Confirm:$false -ErrorAction Stop",
        };
        _tasksBusy = true;
        if (_tasksStatus != null) _tasksStatus.Text = $"{t.Name}: aplicando…";
        string result;
        try
        {
            var (code, outp) = await RunPsAsync(script);
            result = code == 0 ? $"{t.Name}: " + op switch { "run" => "iniciada.", "stop" => "parada.", "enable" => "ligada.", "disable" => "desligada.", _ => "apagada." }
                               : $"{t.Name}: não deu certo ({outp.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim()}).";
        }
        catch (Exception ex) { result = $"{t.Name}: falhou ({ex.Message})."; }
        finally { _tasksBusy = false; }
        ScriptLog("Tarefas agendadas · " + result);
        await LoadTasksAsync();
        if (_tasksStatus != null) _tasksStatus.Text = result + "  ·  " + _tasksStatus.Text;
    }
}
