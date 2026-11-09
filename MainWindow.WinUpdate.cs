using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// Começar aqui › Windows Update: os dois bloqueios (drivers e Update inteiro) e o leitor de atualizações, que mostra o que
/// falta instalar e se a Microsoft já reconheceu problema em cada uma. Na abertura do app checa sozinho (no máximo a cada 12 h)
/// e avisa no canto da tela quando sai uma versão nova do Windows.
/// </summary>
public partial class MainWindow
{
    private StackPanel? _wuBody;
    private bool _wuBusy;

    private void BuildWinUpdateCard(Panel host)
    {
        TweakCard(host, "", "Windows Update",
            "A Microsoft às vezes solta uma atualização que quebra alguma coisa e só corrige semanas depois. Aqui você vê o que falta instalar e se ela tem problema " +
            "conhecido antes de instalar, e pode segurar os drivers ou o Update inteiro enquanto isso.", out var body);
        foreach (var id in new[] { "wu_drivers_block", "wu_block_all" })
            if (OptCatalog.ById(id) is { } t) body.Children.Add(OptCard(t));

        var bar = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        bar.Children.Add(TweakButton("VERIFICAR ATUALIZAÇÕES", true, async (_, _) => await CheckWinUpdatesAsync(withWu: false)));
        bar.Children.Add(TweakButton("PERGUNTAR AO WINDOWS UPDATE", false, async (_, _) => await CheckWinUpdatesAsync(withWu: true)));
        bar.Children.Add(TweakButton("ABRIR O WINDOWS UPDATE", false, (_, _) => OpenUrl("ms-settings:windowsupdate")));
        body.Children.Add(bar);
        body.Children.Add(OptText("VERIFICAR lê o histórico oficial de versões e a lista de problemas conhecidos da Microsoft (funciona mesmo com o Update bloqueado). " +
            "PERGUNTAR AO WINDOWS UPDATE também lista drivers, Defender e .NET que ele oferece; demora um pouco e nada é baixado nem instalado.", "Faint", 11.5, 8));
        _wuBody = new StackPanel();
        body.Children.Add(_wuBody);
        _ = CheckWinUpdatesAsync(withWu: false);
    }

    private sealed record WuReport(WinUpdates.LocalInfo Local, List<WinRelease> History, List<WinIssue> Issues, string? Error);

    private static async Task<WuReport> ReadWinUpdateReportAsync()
    {
        var local = WinUpdates.ReadLocal();
        try
        {
            var hist = Task.Run(() => WinUpdates.GetAsync(WinUpdates.HistoryUrl(local)));
            var iss = Task.Run(() => WinUpdates.GetAsync(WinUpdates.IssuesUrl(local)));
            var history = WinUpdates.ParseHistory(await hist, local.Build);
            List<WinIssue> issues;
            try { issues = WinUpdates.ParseIssues(await iss); } catch { issues = new(); }
            return new WuReport(local, history, issues, history.Count == 0 ? "a página da Microsoft não trouxe o histórico desta versão" : null);
        }
        catch (Exception ex) { return new WuReport(local, new(), new(), ex.Message); }
    }

    private Brush VerdictBrush(string level) => (Brush)FindResource(level switch { "problema" => "TempCrit", "atencao" => "TempWarn", _ => "ChartGpu" });

    private async Task CheckWinUpdatesAsync(bool withWu)
    {
        if (_wuBody == null || _wuBusy) return;
        _wuBusy = true;
        _wuBody.Children.Clear();
        _wuBody.Children.Add(OptText(withWu ? "Perguntando ao Windows Update… (pode levar até 2 minutos)" : "Lendo as páginas oficiais da Microsoft…", "Muted", 12));
        try
        {
            var repTask = ReadWinUpdateReportAsync();
            var wuTask = withWu ? WinUpdates.SearchWindowsUpdateAsync() : Task.FromResult((new List<WuItem>(), (string?)null));
            var rep = await repTask;
            var (wuItems, wuError) = await wuTask;
            _wuBody.Children.Clear();
            RenderWinUpdates(rep, withWu, wuItems, wuError);
        }
        catch (Exception ex) { _wuBody.Children.Clear(); _wuBody.Children.Add(OptText("Não deu para verificar: " + ex.Message, "TempWarn", 12)); }
        finally { _wuBusy = false; }
    }

    private void RenderWinUpdates(WuReport rep, bool withWu, List<WuItem> wuItems, string? wuError)
    {
        var host = _wuBody!;
        var l = rep.Local;
        host.Children.Add(new TextBlock { Text = $"Seu Windows: {l.Product} {l.DisplayVersion} · build {l.BuildText}", FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Fg"), Margin = new Thickness(0, 6, 0, 8) });
        if (rep.Error != null) host.Children.Add(OptText("Não deu para ler a página da Microsoft: " + rep.Error + ". Confira a internet e tente de novo.", "TempWarn", 12, 8));

        var missing = rep.History.Where(r => r.Revision > l.Revision).OrderByDescending(r => r.Revision).ToList();
        if (rep.History.Count > 0)
        {
            if (missing.Count == 0)
                host.Children.Add(OptText("✔ Você está na versão mais recente publicada pela Microsoft para o " + l.DisplayVersion + ".", "ChartGpu", 12.5, 8));
            else
            {
                host.Children.Add(OptText($"VERSÕES QUE VOCÊ AINDA NÃO TEM · {missing.Count}", "Faint", 11, 6));
                host.Children.Add(OptText("As atualizações do Windows são cumulativas: instalar a mais nova já traz as anteriores.", "Faint", 11.5, 6));
                foreach (var r in missing) host.Children.Add(ReleaseCard(r, WinUpdates.Judge(r, rep.Issues, rep.History, l.Revision)));
            }
        }

        // Problemas em aberto que já valem para a versão instalada.
        var current = new WinRelease(l.Build, l.Revision, rep.History.FirstOrDefault(h => h.Revision == l.Revision)?.Kb ?? "", "", "");
        var nowAffects = rep.Issues.Count == 0 ? new List<WinIssue>() : WinUpdates.Judge(current, rep.Issues, rep.History, -1).Issues.Where(i => i.Open).ToList();
        if (nowAffects.Count > 0)
        {
            host.Children.Add(OptText($"PROBLEMAS EM ABERTO QUE JÁ VALEM PARA A SUA VERSÃO · {nowAffects.Count}", "Faint", 11, 6));
            foreach (var i in nowAffects) host.Children.Add(IssueLine(i));
        }

        if (withWu)
        {
            host.Children.Add(OptText("O QUE O WINDOWS UPDATE OFERECE", "Faint", 11, 6));
            if (wuError != null)
                host.Children.Add(OptText("O Windows Update não respondeu (" + wuError + "). Se você bloqueou o Update aqui, isso é esperado: a lista acima continua valendo.", "Muted", 12, 6));
            else if (wuItems.Count == 0)
                host.Children.Add(OptText("Nada pendente no Windows Update.", "ChartGpu", 12, 6));
            foreach (var w in wuItems)
            {
                var kbs = w.Kb.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var rel = rep.History.FirstOrDefault(h => kbs.Contains(h.Kb, StringComparer.OrdinalIgnoreCase));
                var hits = rep.Issues.Where(i => kbs.Contains(i.OriginKb, StringComparer.OrdinalIgnoreCase) && i.Open).ToList();
                string verdict = rel != null ? WinUpdates.Judge(rel, rep.Issues, rep.History, l.Revision).Text
                    : hits.Count > 0 ? $"A Microsoft listou {hits.Count} problema(s) em aberto vindos desta atualização."
                    : "Sem problema conhecido publicado pela Microsoft.";
                string level = rel != null ? WinUpdates.Judge(rel, rep.Issues, rep.History, l.Revision).Level : hits.Count > 0 ? "atencao" : "ok";
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = w.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Fg") });
                sp.Children.Add(OptText(string.Join("  ·  ", new[] { w.Kind, w.Size > 0 ? Cleanup.Size(w.Size) : "" }.Where(s => s.Length > 0)), "Faint", 11));
                sp.Children.Add(new TextBlock { Text = verdict, Foreground = VerdictBrush(level), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                foreach (var i in hits) sp.Children.Add(IssueLine(i));
                if (kbs.Length > 0) sp.Children.Add(KbButtons(kbs[0]));
                host.Children.Add(new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16, 10, 16, 10), Child = sp });
            }
        }

        host.Children.Add(OptText("Fonte: Windows release health (Microsoft), que lista só os problemas que a própria Microsoft reconheceu. Relatos em fóruns e notícias " +
            "aparecem antes disso: use PROCURAR RELATOS para ver o que estão dizendo de uma atualização.", "Faint", 11, 0));
    }

    private Border ReleaseCard(WinRelease r, WinVerdict v)
    {
        var sp = new StackPanel();
        var head = new WrapPanel();
        head.Children.Add(new TextBlock { Text = $"{r.Kb} · build {r.BuildText}", FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Fg"), Margin = new Thickness(0, 0, 8, 2) });
        head.Children.Add(Badge(r.TypeText.ToUpperInvariant(), (Brush)FindResource("Faint")));
        head.Children.Add(Badge(v.Level switch { "problema" => "TEM PROBLEMA", "atencao" => "ATENÇÃO", _ => "OK" }, VerdictBrush(v.Level)));
        sp.Children.Add(head);
        sp.Children.Add(OptText("Publicada em " + r.Date, "Faint", 11));
        sp.Children.Add(new TextBlock { Text = v.Text, Foreground = VerdictBrush(v.Level), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2) });
        foreach (var i in v.Issues.Where(i => i.Open)) sp.Children.Add(IssueLine(i));
        if (v.Fixes.Count > 0)
        {
            sp.Children.Add(OptText($"Corrige {v.Fixes.Count} problema(s) que a sua versão tem:", "ChartGpu", 12, 0));
            foreach (var i in v.Fixes) sp.Children.Add(OptText("  ✔ " + i.Title, "Muted", 11.5));
        }
        if (r.Kb.StartsWith("KB")) sp.Children.Add(KbButtons(r.Kb));
        return new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16, 10, 16, 10), Child = sp };
    }

    private TextBlock IssueLine(WinIssue i)
    {
        string since = i.OriginKb.Length > 0 ? $"desde a {i.OriginKb}" : "a Microsoft não informou qual atualização causou";
        var t = OptText($"  ⚠ {i.Title} — {i.StatusText} ({since})",i.Open ? "TempWarn" : "Muted", 11.5);
        t.ToolTip = i.Summary + "\n\nAtualizado: " + i.Updated;
        return t;
    }

    private WrapPanel KbButtons(string kb)
    {
        var w = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        string num = kb.Replace("KB", "", StringComparison.OrdinalIgnoreCase);
        foreach (var (text, url) in new[] { ("NOTAS DA MICROSOFT", "https://support.microsoft.com/help/" + num),
                                            ("PROCURAR RELATOS", "https://www.bing.com/search?q=" + Uri.EscapeDataString(kb + " problema bug Windows 11")) })
        {
            var b = TweakButton(text, false, (_, _) => OpenUrl(url));
            b.Padding = new Thickness(10, 4, 10, 4); b.FontSize = 11; b.Margin = new Thickness(0, 0, 6, 0);
            w.Children.Add(b);
        }
        return w;
    }

    /// <summary>Na abertura: no máximo a cada 12 h, vê se saiu versão nova do Windows e avisa uma vez por versão.</summary>
    private async Task WinUpdateStartupCheckAsync()
    {
        try
        {
            if (DateTime.UtcNow - _settings.WinUpdLastCheck < TimeSpan.FromHours(12)) return;
            await Task.Delay(TimeSpan.FromSeconds(60));
            var rep = await ReadWinUpdateReportAsync();
            if (rep.Error != null) return;
            _settings.WinUpdLastCheck = DateTime.UtcNow;
            var newest = rep.History.Where(r => r.Revision > rep.Local.Revision).OrderByDescending(r => r.Revision).FirstOrDefault();
            if (newest == null || newest.Kb == _settings.WinUpdLastNotified) { _settings.Save(); return; }
            _settings.WinUpdLastNotified = newest.Kb;
            _settings.Save();
            var v = WinUpdates.Judge(newest, rep.Issues, rep.History, rep.Local.Revision);
            string state = v.Level switch { "problema" => "a Microsoft confirmou problema nela", "atencao" => "tem problema conhecido com contorno", _ => "sem problemas conhecidos" };
            Toast("Atualização do Windows disponível", $"{newest.Kb} ({newest.TypeText.ToLowerInvariant()}): {state}.\nVeja em TWEAKING › Começar aqui.", "");
        }
        catch { }
    }
}
