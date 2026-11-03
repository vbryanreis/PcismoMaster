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
/// TWEAKING · "Scripts completos do Ultimate": cada opção de cada script num botão, com selo de risco e uma confirmação
/// que explica o que vai mudar (reinício, Modo de Segurança, downloads, sem desfazer). Nada roda sem confirmar; o
/// ponto de restauração vem marcado nos de risco. Um script por vez, com a saída na tela e botão PARAR.
/// </summary>
public partial class MainWindow
{
    private Process? _scriptProc;
    private readonly List<(TextBox Log, Button Stop)> _scriptOutputs = new();

    private static string ScriptDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Scripts");

    private Brush RiskBrush(ScriptRisk r) => (Brush)FindResource(r switch { ScriptRisk.Perigoso => "TempCrit", ScriptRisk.Atencao => "TempWarn", _ => "ChartGpu" });
    private static string RiskText(ScriptRisk r) => r switch { ScriptRisk.Perigoso => "PERIGOSO", ScriptRisk.Atencao => "ATENÇÃO", _ => "SEGURO" };

    private Border RiskBadge(ScriptRisk r)
    {
        var c = ((SolidColorBrush)RiskBrush(r)).Color;
        var b = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x26, c.R, c.G, c.B)), Child = new TextBlock { Text = RiskText(r) } };
        TextElement.SetForeground(b, RiskBrush(r));
        return b;
    }

    /// <summary>Cartão que abre (sob demanda, para a página continuar leve) os scripts completos desta sub-aba.</summary>
    private void AddScriptSection(Panel host, string page)
    {
        var cards = ScriptCatalog.For(page).ToList();
        var sectionCard = TweakCard(host, "SCRIPTS COMPLETOS DO ULTIMATE",
            "Cada script da pasta, do jeito que o autor fez, com todas as opções do menu dele. Diferente dos ajustes acima, a maioria NÃO tem " +
            "desfazer pelo PcismoMaster: para voltar, use o botão de padrão do próprio cartão ou o ponto de restauração. Cada botão mostra " +
            "antes o que vai acontecer. Selos: SEGURO, ATENÇÃO (muda bastante coisa ou baixa programas) e PERIGOSO (tira proteções, apaga coisas ou reinicia o PC).",
            out var body);
        var list = new StackPanel();
        Button toggle = null!;
        toggle = TweakButton($"MOSTRAR OS {cards.Count} SCRIPTS", true, (_, _) =>
        {
            if (list.Children.Count == 0) foreach (var c in cards) list.Children.Add(ScriptCardView(c));
            list.Visibility = list.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            toggle.Content = list.Visibility == Visibility.Visible ? "ESCONDER OS SCRIPTS" : $"MOSTRAR OS {cards.Count} SCRIPTS";
        });
        list.Visibility = Visibility.Collapsed;
        body.Children.Add(toggle);
        body.Children.Add(list);

        // Saída do script (uma por página; a mesma execução aparece em todas).
        var log = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 11, Height = 180, Margin = new Thickness(0, 10, 0, 6),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, Text = "A saída do script aparece aqui.",
            Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Muted"), BorderThickness = new Thickness(0) };
        var stop = TweakButton("PARAR O SCRIPT", false, (_, _) => StopScript());
        stop.IsEnabled = _scriptProc != null;
        body.Children.Add(log);
        body.Children.Add(stop);
        _scriptOutputs.Add((log, stop));
        AdvancedOnly(sectionCard); // scripts inteiros só no modo avançado
    }

    private Border ScriptCardView(ScriptCard c)
    {
        var sp = new StackPanel();
        var head = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        head.Children.Add(new TextBlock { Text = c.Title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        head.Children.Add(RiskBadge(c.MaxRisk));
        sp.Children.Add(head);
        sp.Children.Add(new TextBlock { Text = c.Description, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Text = c.Source, Foreground = (Brush)FindResource("Faint"), FontSize = 10, Margin = new Thickness(0, 2, 0, 8) });
        var wrap = new WrapPanel();
        foreach (var o in c.Options)
        {
            var b = TweakButton(o.Label.ToUpperInvariant() + (o.Risk == ScriptRisk.Perigoso ? "  ⚠" : ""), false, (_, _) => _ = RunScriptOptionAsync(c, o));
            if (o.Risk != ScriptRisk.Seguro) b.Foreground = RiskBrush(o.Risk);
            b.ToolTip = o.Warning ?? c.Description;
            wrap.Children.Add(b);
        }
        sp.Children.Add(wrap);
        return new Border { BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 4), Margin = new Thickness(0, 10, 0, 0), Child = sp };
    }

    // ---------------- Confirmação ----------------

    private sealed record ScriptChoice(bool RestorePoint, string? Usb);

    private ScriptChoice? ConfirmScript(ScriptCard c, ScriptOption o)
    {
        var warnings = ScriptCatalog.Warnings(o, c);
        var root = new StackPanel { Margin = new Thickness(22) };
        var head = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        head.Children.Add(RiskBadge(o.Risk));
        head.Children.Add(new TextBlock { Text = $"{c.Title} · {o.Label}", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(head);
        root.Children.Add(new TextBlock { Text = c.Description, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 12) });
        if (warnings.Count > 0)
        {
            var box = new StackPanel();
            foreach (var w in warnings)
                box.Children.Add(new TextBlock { Text = "• " + w, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
                    Foreground = o.Risk == ScriptRisk.Seguro ? (Brush)FindResource("Fg") : RiskBrush(o.Risk) });
            root.Children.Add(new Border { BorderBrush = RiskBrush(o.Risk), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 4), Margin = new Thickness(0, 0, 0, 12), Child = box });
        }
        ComboBox? usb = null;
        if (o.NeedsUsb)
        {
            usb = new ComboBox { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable))
                usb.Items.Add($"{d.Name[0]}: {d.VolumeLabel} ({d.TotalSize / 1_000_000_000.0:0.#} GB)");
            if (usb.Items.Count > 0) usb.SelectedIndex = 0;
            root.Children.Add(new TextBlock { Text = usb.Items.Count > 0 ? "Pendrive de instalação do Windows:" : "Nenhum pendrive encontrado: conecte o pendrive de instalação e tente de novo.", Margin = new Thickness(0, 0, 0, 4) });
            root.Children.Add(usb);
        }
        var rp = new CheckBox { Content = "Criar um ponto de restauração antes", IsChecked = o.Risk != ScriptRisk.Seguro && !o.NeedsUsb, Margin = new Thickness(0, 0, 0, 8) };
        if (o.Risk != ScriptRisk.Seguro && !o.NeedsUsb) root.Children.Add(rp);
        var agree = new CheckBox { Content = "Li os avisos e assumo o risco", Margin = new Thickness(0, 0, 0, 14), FontWeight = FontWeights.SemiBold };
        if (o.Risk == ScriptRisk.Perigoso) root.Children.Add(agree);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var win = new Window
        {
            Title = "Confirmar · " + c.Title, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Height, Width = 560, Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Fg"), ShowInTaskbar = false,
        };
        var view = TweakButton("VER O SCRIPT", false, (_, _) => ShowScriptText(o));
        var ok = TweakButton(o.Restarts ? "RODAR E REINICIAR" : "RODAR", true, (_, _) => win.DialogResult = true);
        var cancel = TweakButton("CANCELAR", false, (_, _) => win.DialogResult = false);
        ok.IsEnabled = o.Risk != ScriptRisk.Perigoso && (usb == null || usb.Items.Count > 0);
        agree.Checked += (_, _) => ok.IsEnabled = usb == null || usb.Items.Count > 0;
        agree.Unchecked += (_, _) => ok.IsEnabled = false;
        buttons.Children.Add(view);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);
        win.Content = root;
        if (win.ShowDialog() != true) return null;
        return new ScriptChoice(rp.IsChecked == true && root.Children.Contains(rp), usb?.SelectedItem is string s ? s[..1] : null);
    }

    private void ShowScriptText(ScriptOption o)
    {
        var box = new TextBox { Text = ScriptCatalog.Read(o.Id), IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Fg") };
        new Window { Title = "Script · " + o.Id, Owner = this, Width = 900, Height = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"), Content = box }.Show();
    }

    // ---------------- Execução ----------------

    private void ScriptLog(string line)
    {
        foreach (var (log, _) in _scriptOutputs)
        {
            if (log.Text.Length > 60000) log.Text = log.Text[^40000..];
            log.AppendText(Environment.NewLine + line);
            log.ScrollToEnd();
        }
    }

    private void ScriptRunning(bool on) { foreach (var (_, stop) in _scriptOutputs) stop.IsEnabled = on; }

    private void StopScript()
    {
        try { _scriptProc?.Kill(entireProcessTree: true); ScriptLog("— parado pelo usuário."); } catch { }
    }

    private async Task RunScriptOptionAsync(ScriptCard c, ScriptOption o)
    {
        if (!RequireAdmin()) return;
        if (_scriptProc != null) { MessageBox.Show(this, "Já tem um script rodando. Espere terminar ou clique em PARAR O SCRIPT.", "Tweaking"); return; }
        var choice = ConfirmScript(c, o);
        if (choice == null) return;
        await RunPowerShellTextAsync($"{c.Title} · {o.Label}", o.Id, ScriptCatalog.Read(o.Id), choice.RestorePoint, choice.Usb);
        if (o.Restarts) ScriptLog("O PC deve reiniciar agora.");
    }

    /// <summary>Roda um texto de PowerShell (já confirmado pelo usuário) mostrando a saída nos logs; um por vez.</summary>
    private async Task<bool> RunPowerShellTextAsync(string title, string fileId, string body, bool restorePoint, string? usb = null)
    {
        if (_scriptProc != null) { MessageBox.Show(this, "Já tem um script rodando. Espere terminar ou clique em PARAR.", "Tweaking"); return false; }
        foreach (var (log, _) in _scriptOutputs) log.Text = $"▶ {title}  ({DateTime.Now:HH:mm:ss})";
        if (restorePoint)
        {
            string? why = await Maintenance.CreateRestorePoint(new Maintenance.Ctx(new Progress<string>(ScriptLog), CancellationToken.None), title);
            if (why != null && MessageBox.Show(this, "O ponto de restauração não foi criado: " + why + "\n\nContinuar mesmo assim?", "Ponto de restauração",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { ScriptLog("Cancelado."); return false; }
        }

        Directory.CreateDirectory(ScriptDir);
        string file = Path.Combine(ScriptDir, fileId + ".ps1");
        // Saída em UTF-8 para os acentos chegarem certos na tela.
        await File.WriteAllTextAsync(file, "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n" + body, new UTF8Encoding(true));
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
          StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = ScriptDir };
        if (usb != null) psi.Environment["PM_USB"] = usb;

        bool ok = false;
        try
        {
            var p = Process.Start(psi)!;
            _scriptProc = p;
            ScriptRunning(true);
            p.StandardInput.Close(); // nada espera digitação
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
}
