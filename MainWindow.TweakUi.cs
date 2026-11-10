using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// Peças de tela comuns do TWEAKING (cartão, botão, selo de risco) e o executor de PowerShell com a saída na tela:
/// um script por vez, com botão PARAR. Usado pelo Bloatware e pelas páginas de otimização.
/// </summary>
public partial class MainWindow
{
    private Process? _scriptProc;
    private readonly List<(TextBox Log, Button Stop)> _scriptOutputs = new();

    private static string ScriptDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Scripts");
    private static string TweakBackupDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "ModuleBackups");

    private Border TweakCard(Panel host, string icon, string title, string? intro, out StackPanel body)
    {
        body = new StackPanel();
        body.Children.Add(CardHeader(icon, title));
        if (intro != null)
            body.Children.Add(new TextBlock { Text = intro, Foreground = (Brush)FindResource("Faint"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var card = new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 22), Child = body };
        host.Children.Add(card);
        return card;
    }

    private Button TweakButton(string text, bool primary, RoutedEventHandler click, string? tag = null)
    {
        var b = new Button { Content = text, Style = (Style)FindResource(primary ? "PrimaryButton" : "OutlineButton"), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 8), Tag = tag,
            HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += click;
        return b;
    }

    private TextBlock TweakStatusText(string text = "") =>
        new() { Text = text, Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    private Brush RiskBrush(ScriptRisk r) => (Brush)FindResource(r switch { ScriptRisk.Perigoso => "TempCrit", ScriptRisk.Atencao => "TempWarn", _ => "ChartGpu" });
    private static string RiskText(ScriptRisk r) => r switch { ScriptRisk.Perigoso => "PERIGOSO", ScriptRisk.Atencao => "ATENÇÃO", _ => "SEGURO" };

    private Border RiskBadge(ScriptRisk r) => Badge(RiskText(r), RiskBrush(r));

    /// <summary>Etiqueta de status (RECOMENDADO, REINICIA, ATENÇÃO…): contorno fino e texto mono na cor do status, sem fundo.</summary>
    private Border Badge(string text, Brush brush)
    {
        var b = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 6, 0),
            BorderBrush = brush, Child = new TextBlock { Text = text.ToUpperInvariant() } };
        if (ThemeManager.Legacy) // tema Legacy: selo com fundo suave na cor do status, como na 0.12.0
        {
            var c = brush is SolidColorBrush s ? s.Color : Colors.Gray;
            b.Padding = new Thickness(7, 2, 7, 2);
            b.Background = new SolidColorBrush(Color.FromArgb(0x26, c.R, c.G, c.B));
            ((TextBlock)b.Child).Text = text;
        }
        TextElement.SetForeground(b, brush);
        return b;
    }

    /// <summary>Caixa de saída + PARAR, ligada ao executor (a mesma execução aparece em todas as caixas abertas).</summary>
    private TextBox AddScriptOutput(Panel host, string hint)
    {
        var log = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, Height = 160, Margin = new Thickness(0, 10, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, Text = hint,
            Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Muted"), BorderThickness = new Thickness(0) };
        var stop = TweakButton("PARAR", false, (_, _) => StopScript());
        stop.IsEnabled = _scriptProc != null;
        // Escondida até algo rodar: uma caixa vazia no meio da página só empurrava os ajustes para baixo.
        log.Visibility = stop.Visibility = _scriptShown ? Visibility.Visible : Visibility.Collapsed;
        host.Children.Add(log);
        host.Children.Add(stop);
        _scriptOutputs.Add((log, stop));
        return log;
    }

    private void ScriptLog(string line)
    {
        // Pode chegar de uma tarefa em segundo plano: a caixa de texto só pode ser mexida pela tela.
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ScriptLog(line)); return; }
        ShowScriptOutputs();
        foreach (var (log, _) in _scriptOutputs)
        {
            if (log.Text.Length > 60000) log.Text = log.Text[^40000..];
            log.AppendText(Environment.NewLine + line);
            log.ScrollToEnd();
        }
    }

    private bool _scriptShown;

    /// <summary>Mostra as caixas de andamento (e o cartão "Andamento" que as guarda) na primeira vez que algo roda.</summary>
    private void ShowScriptOutputs()
    {
        if (_scriptShown) return;
        _scriptShown = true;
        foreach (var (log, stop) in _scriptOutputs)
        {
            log.Visibility = stop.Visibility = Visibility.Visible;
            for (DependencyObject? p = log; p != null; p = LogicalTreeHelper.GetParent(p))
                if (p is Border { Tag: "output-card" } card) { card.Visibility = Visibility.Visible; break; }
        }
    }

    private void ScriptRunning(bool on) { foreach (var (_, stop) in _scriptOutputs) stop.IsEnabled = on; }

    private void StopScript()
    {
        try { _scriptProc?.Kill(entireProcessTree: true); ScriptLog("— parado pelo usuário."); } catch { }
    }

    /// <summary>Roda um texto de PowerShell (já confirmado pelo usuário) mostrando a saída nos logs; um por vez.</summary>
    private async Task<bool> RunPowerShellTextAsync(string title, string fileId, string body, bool restorePoint, bool clearLog = true)
    {
        if (_scriptProc != null) { MessageBox.Show(this, "Já tem um script rodando. Espere terminar ou clique em PARAR.", "Tweaking"); return false; }
        if (clearLog) foreach (var (log, _) in _scriptOutputs) log.Text = $"▶ {title}  ({DateTime.Now:HH:mm:ss})";
        else ScriptLog($"▶ {title}");
        ShowScriptOutputs();
        if (restorePoint && !await EnsureRestorePointAsync(title)) { ScriptLog("Cancelado."); return false; }

        Directory.CreateDirectory(ScriptDir);
        string file = Path.Combine(ScriptDir, fileId + ".ps1");
        await File.WriteAllTextAsync(file, "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n$ProgressPreference = 'SilentlyContinue'\r\n" + body, new UTF8Encoding(true));
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
          StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = ScriptDir };

        bool ok = false;
        try
        {
            var p = Process.Start(psi)!;
            _scriptProc = p;
            ScriptRunning(true);
            p.StandardInput.Close();
            var progress = new Progress<string>(ScriptLog);
            var t1 = Pump(p.StandardOutput, progress);
            var t2 = Pump(p.StandardError, progress);
            await p.WaitForExitAsync();
            await Task.WhenAll(t1, t2);
            ok = p.ExitCode == 0;
            ScriptLog(ok ? "✔ Terminou." : $"Terminou com código {p.ExitCode}.");
        }
        catch (Exception ex) { ScriptLog("Não deu para rodar: " + ex.Message); }
        finally
        {
            _scriptProc?.Dispose();
            _scriptProc = null;
            ScriptRunning(false);
            try { File.Delete(file); } catch { }
        }
        return ok;

        static async Task Pump(StreamReader r, IProgress<string> to)
        {
            string? line;
            while ((line = await r.ReadLineAsync()) != null) if (line.Trim().Length > 0) to.Report(line.TrimEnd());
        }
    }

    private bool _restorePointDone;

    /// <summary>Um ponto de restauração por sessão do app, antes da primeira mudança. false = a pessoa desistiu.</summary>
    private async Task<bool> EnsureRestorePointAsync(string title)
    {
        if (_restorePointDone) return true;
        ScriptLog("Criando ponto de restauração…");
        string? why = await Maintenance.CreateRestorePoint(new Maintenance.Ctx(new Progress<string>(ScriptLog), CancellationToken.None), "PcismoMaster · " + title);
        if (why != null && MessageBox.Show(this, "O ponto de restauração não foi criado: " + why + "\n\nContinuar mesmo assim?", "Ponto de restauração",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
        _restorePointDone = why == null;
        return true;
    }
}

public partial class MainWindow
{
    /// <summary>
    /// Páginas com pouco conteúdo (Sistema, Discos, Energia, Rede, Utilidades) ficam com no máximo 1400 px de largura,
    /// para não esticar linhas e cartões pela tela inteira em monitor largo; a lista do Desinstalar apps cresce com a janela.
    /// </summary>
    private void ApplyPageLayout()
    {
        foreach (FrameworkElement page in new FrameworkElement[] { PageSystem, PageDisks, PageEnergy, PageNet, PageUtil })
        {
            var content = page is ScrollViewer { Content: FrameworkElement c } ? c : page;
            content.MaxWidth = 1400;
            content.HorizontalAlignment = HorizontalAlignment.Left;
        }
        SizeChanged += (_, _) => UninstallList.MaxHeight = Math.Max(430, ActualHeight - 470);
        // Manutenção tem uma página só: abrir a categoria já abre a página.
        GrpMaint.Checked += (_, _) => NavMaint.IsChecked = true;
    }
}
