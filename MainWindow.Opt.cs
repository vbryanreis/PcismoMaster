using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// TWEAKING: Formatação, Debloat, Windows, Mouse e teclado, GPU, Perfis e Limpeza, montados a partir do OptCatalog.
/// Cada página só é montada quando aberta e o estado dos ajustes é lido em segundo plano (um PowerShell para tudo que
/// não é registro). Nada muda no PC sem clique; antes da primeira mudança é criado um ponto de restauração.
/// </summary>
public partial class MainWindow
{
    private sealed record SavedOptProfile(string Name, string[] Ids);

    private OptEngine? _optEngine;
    private OptEngine Opt => _optEngine ??= new OptEngine(new RegistryStore(), OptDir, new PowerShellRunner(), new DisplayRefresh(), ServiceExists);
    private static string OptDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "Otimizar");
    private static string OptProfilesFile => Path.Combine(OptDir, "perfis.json");

    private readonly HashSet<string> _optBuilt = new();
    private readonly HashSet<OptPage> _optPagesShown = new();
    private readonly Dictionary<string, OptState> _optStates = new();
    private readonly Dictionary<string, List<Action>> _optViews = new();
    private readonly List<Action> _optCounters = new();
    private bool _optBusy, _optReading;
    private StackPanel? _optProfilesList;

    private static bool ServiceExists(string name)
    {
        try { using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name); return k != null; } catch { return false; }
    }

    private void InitOpt(string page)
    {
        if (!_optBuilt.Add(page)) return;
        switch (page)
        {
            case "PageOptFormat": BuildOptFormat(OptFormatHost); break;
            case "PageOptDebloat": BuildOptDebloat(OptDebloatHost); _optPagesShown.Add(OptPage.Debloat); break;
            case "PageOptWindows": BuildOptWindows(OptWindowsHost); _optPagesShown.Add(OptPage.Windows); break;
            case "PageOptInput": BuildOptInput(OptInputHost); _optPagesShown.Add(OptPage.Mouse); _optPagesShown.Add(OptPage.Teclado); break;
            case "PageOptGpu": BuildOptGpu(OptGpuHost); _optPagesShown.Add(OptPage.Gpu); break;
            case "PageOptProfiles": BuildOptProfiles(OptProfilesHost); break;
            case "PageOptClean": BuildOptClean(OptCleanHost); return;
        }
        _ = RefreshOptStatesAsync();
    }

    // ---------------- Estado ----------------

    /// <summary>Lê o estado das páginas abertas (ou só de alguns ajustes, depois de aplicar: bem mais rápido).</summary>
    private async Task RefreshOptStatesAsync(IEnumerable<OptTweak>? only = null)
    {
        if (only == null && _optReading) return;
        if (only == null) _optReading = true;
        try
        {
            var list = only?.ToList() ?? OptCatalog.All.Where(t => _optPagesShown.Contains(t.Page) || t.Id == "new_users").ToList();
            if (list.Count == 0) list = OptCatalog.All.ToList(); // perfis e formatação precisam saber o que já está ativo
            var states = await Task.Run(() => Opt.StatesAsync(list));
            foreach (var kv in states) _optStates[kv.Key] = kv.Value;
        }
        catch (Exception ex) { ScriptLog("Não deu para ler o estado dos ajustes: " + ex.Message); }
        finally { if (only == null) _optReading = false; }
        foreach (var views in _optViews.Values) foreach (var v in views) v();
        foreach (var c in _optCounters) c();
    }

    private void OptView(string id, Action refresh)
    {
        if (!_optViews.TryGetValue(id, out var list)) _optViews[id] = list = new();
        list.Add(refresh);
        refresh();
    }

    private string OptStatusText(OptTweak t)
    {
        if (!_optStates.TryGetValue(t.Id, out var s)) return "lendo…";
        return s.On switch
        {
            null => "não se aplica neste PC",
            true => s.Ours ? "ativo" : "ativo (já estava assim)",
            false => s.Ours ? "o Windows mudou de volta" : "inativo",
        };
    }

    // ---------------- Aplicar e desfazer ----------------

    private async Task<bool> ApplyOptAsync(IList<OptTweak> list, string title, bool undo = false)
    {
        if (list.Count == 0) { MessageBox.Show(this, "Nada para fazer: os itens escolhidos já estão assim.", "Tweaking"); return false; }
        if (_optBusy || _scriptProc != null) { MessageBox.Show(this, "Espere a operação atual terminar.", "Tweaking"); return false; }
        if (!RequireAdmin()) return false;
        _optBusy = true;
        foreach (var views in _optViews.Values) foreach (var v in views) v();
        foreach (var (log, _) in _scriptOutputs) log.Text = $"▶ {title}  ({DateTime.Now:HH:mm:ss})";
        ShowScriptOutputs();
        try
        {
            if (!undo && !await EnsureRestorePointAsync(title)) { ScriptLog("Cancelado."); return false; }
            var progress = new Progress<string>(ScriptLog);
            var failed = await Task.Run(() => undo ? Opt.UndoAsync(list, progress) : Opt.ApplyAsync(list, progress));
            var ok = list.Where(t => !failed.ContainsKey(t.Id)).ToList();
            if (ok.Any(t => t.Explorer)) await Maintenance.RestartExplorer(new Maintenance.Ctx(progress, CancellationToken.None), null);
            ScriptLog(failed.Count == 0 ? $"✔ Pronto: {ok.Count} item(ns)." : $"Terminou: {ok.Count} ok, {failed.Count} com problema (veja acima).");
            if (ok.Any(t => t.Restart)) ScriptLog("⟳ Alguns itens só valem depois de reiniciar o PC.");
            foreach (var t in ok.Where(t => undo && t.UndoNote != null)) ScriptLog("ℹ " + t.UndoNote);
            return failed.Count == 0;
        }
        catch (Exception ex) { ScriptLog("Erro: " + ex.Message); return false; }
        finally
        {
            _optBusy = false;
            // Só o que mudou (e os outros do mesmo grupo, que podem ter sido desfeitos).
            var groups = list.Select(t => t.Group).OfType<string>().ToHashSet();
            await RefreshOptStatesAsync(list.Concat(OptCatalog.All.Where(t => t.Group != null && groups.Contains(t.Group))).Distinct());
        }
    }

    private async Task ToggleOptAsync(OptTweak t, bool on)
    {
        if (on)
        {
            if (t.Warning != null && MessageBox.Show(this, $"{t.Title}\n\n⚠ {t.Warning}\n\nAplicar mesmo assim?", "Tweaking",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await ApplyOptAsync(new[] { t }, t.Title);
            return;
        }
        if (_optStates.TryGetValue(t.Id, out var s) && !s.Ours)
        {
            MessageBox.Show(this, "Este ajuste já estava assim no Windows antes do PcismoMaster, então não há valor anterior guardado para voltar.", "Tweaking");
            return;
        }
        await ApplyOptAsync(new[] { t }, "Desfazer · " + t.Title, undo: true);
    }

    // ---------------- Peças ----------------

    private TextBlock OptText(string s, string brush = "Muted", double size = 12, double bottom = 0) =>
        new() { Text = s, Foreground = (Brush)FindResource(brush), FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, bottom) };

    private WrapPanel OptBadges(OptTweak t)
    {
        var head = new WrapPanel();
        head.Children.Add(new TextBlock { Text = t.Title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 2), TextWrapping = TextWrapping.Wrap });
        if (t.Recommended || t.Level is OptLevel.Essencial or OptLevel.Recomendado) head.Children.Add(Badge("RECOMENDADO", (Brush)FindResource("ChartGpu")));
        if (t.Restart) head.Children.Add(Badge("REINICIA", (Brush)FindResource("TempWarn")));
        head.Children.Add(Badge(t.Category.ToUpperInvariant(), (Brush)FindResource("Faint")));
        return head;
    }

    private StackPanel OptTexts(OptTweak t)
    {
        var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        text.Children.Add(OptBadges(t));
        text.Children.Add(OptText(t.Description, "Muted", 12));
        if (t.Warning != null) text.Children.Add(new TextBlock { Text = "⚠ " + t.Warning, Foreground = (Brush)FindResource("TempWarn"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        return text;
    }

    /// <summary>Cartão de um ajuste com interruptor (liga = aplica; desliga = desfaz o que o PcismoMaster aplicou).</summary>
    private Border OptCard(OptTweak t)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(OptTexts(t));
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var sw = new CheckBox { HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = false };
        var status = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Faint"), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(sw);
        right.Children.Add(status);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        bool updating = false;
        void Refresh()
        {
            updating = true;
            var st = _optStates.GetValueOrDefault(t.Id);
            sw.IsChecked = st?.On == true;
            sw.IsEnabled = st?.On != null && !_optBusy;
            status.Text = OptStatusText(t);
            status.Foreground = (Brush)FindResource(st?.On == true ? "ChartGpu" : "Faint");
            updating = false;
        }
        sw.Click += async (_, _) =>
        {
            if (updating) return;
            bool want = sw.IsChecked == true;
            Refresh(); // volta ao estado real até terminar
            await ToggleOptAsync(t, want);
        };
        OptView(t.Id, Refresh);
        return new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(18, 14, 18, 14), Child = grid, Tag = t };
    }

    private Border OptOutputCard(Panel host)
    {
        var card = TweakCard(host, "ANDAMENTO", null, out var body);
        AddScriptOutput(body, "O que for aplicado aparece aqui.");
        card.Tag = "output-card";
        if (!_scriptShown) card.Visibility = Visibility.Collapsed;
        return card;
    }

    // ---------------- Debloat ----------------

    private void BuildOptDebloat(Panel host)
    {
        TweakCard(host, "COMO FUNCIONA",
            "Marque o que quer tirar e clique em APLICAR SELECIONADOS. Essencial e Recomendado já vêm marcados; Avançado fica escondido porque " +
            "tem efeitos colaterais (cada item diz qual). Tudo guarda o estado de antes: o botão DESFAZER de cada item volta exatamente como estava. " +
            "Antes da primeira mudança o PcismoMaster cria um ponto de restauração.", out _);

        var checks = new Dictionary<string, CheckBox>();
        Button apply = null!;
        void Count() { int n = checks.Count(kv => kv.Value.IsChecked == true && kv.Value.IsEnabled); apply.Content = n == 0 ? "APLICAR SELECIONADOS" : $"APLICAR {n} SELECIONADO(S)"; apply.IsEnabled = n > 0 && !_optBusy; }
        apply = TweakButton("APLICAR SELECIONADOS", true, async (_, _) =>
        {
            var list = OptCatalog.For(OptPage.Debloat).Where(t => checks[t.Id].IsChecked == true && checks[t.Id].IsEnabled).ToList();
            var warn = list.Where(t => t.Warning != null).Select(t => $"• {t.Title}: {t.Warning}").ToList();
            if (warn.Count > 0 && MessageBox.Show(this, "Atenção a estes itens:\n\n" + string.Join("\n", warn.Take(12)) + "\n\nAplicar?", "Debloat",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await ApplyOptAsync(list, $"Debloat · {list.Count} item(ns)");
        });

        foreach (var level in new[] { OptLevel.Essencial, OptLevel.Recomendado, OptLevel.Avancado })
        {
            var items = OptCatalog.For(OptPage.Debloat).Where(t => t.Level == level).ToList();
            string intro = level switch
            {
                OptLevel.Essencial => "Telemetria, propaganda, IA e serviços que rodam escondidos. Seguro para qualquer PC.",
                OptLevel.Recomendado => "Mais desempenho e privacidade. Pode remover apps de IA (voltam pela Store) e mudar serviços.",
                _ => "Ganhos menores e com efeito colateral: leia o aviso de cada um. Nada aqui vem marcado.",
            };
            var card = TweakCard(host, $"{OptCatalog.LevelText(level).ToUpperInvariant()} · {items.Count} ITENS", intro, out var body);
            var rows = new StackPanel();
            var tools = new WrapPanel();
            tools.Children.Add(TweakButton("MARCAR TODOS", false, (_, _) => { foreach (var t in items) if (checks[t.Id].IsEnabled) checks[t.Id].IsChecked = true; Count(); }));
            tools.Children.Add(TweakButton("DESMARCAR TODOS", false, (_, _) => { foreach (var t in items) checks[t.Id].IsChecked = false; Count(); }));
            if (level == OptLevel.Avancado)
            {
                rows.Visibility = Visibility.Collapsed;
                tools.Visibility = Visibility.Collapsed;
                Button show = null!;
                show = TweakButton("MOSTRAR OS AVANÇADOS", false, (_, _) =>
                {
                    if (MessageBox.Show(this, "Os itens avançados liberam mais recursos, mas cada um desliga algo que alguém pode usar (impressão, pesquisa rápida, " +
                            "hibernação, acesso remoto, caneta, Windows Hello…). Leia o aviso de cada item antes de marcar.\n\nMostrar?", "Debloat avançado",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    rows.Visibility = tools.Visibility = Visibility.Visible;
                    show.Visibility = Visibility.Collapsed;
                });
                body.Children.Add(show);
            }
            body.Children.Add(tools);
            body.Children.Add(rows);
            foreach (var t in items) rows.Children.Add(DebloatRow(t, checks, Count));
        }

        var actions = TweakCard(host, "APLICAR", null, out var ab);
        var bar = new WrapPanel();
        bar.Children.Add(apply);
        bar.Children.Add(TweakButton("DESFAZER TUDO DO DEBLOAT", false, async (_, _) =>
        {
            var ours = OptCatalog.For(OptPage.Debloat).Where(t => _optStates.GetValueOrDefault(t.Id)?.Ours == true).ToList();
            if (ours.Count == 0) { MessageBox.Show(this, "Nada do Debloat foi aplicado pelo PcismoMaster.", "Debloat"); return; }
            if (MessageBox.Show(this, $"Voltar {ours.Count} item(ns) ao estado de antes?", "Debloat", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await ApplyOptAsync(ours, "Desfazer Debloat", undo: true);
        }));
        ab.Children.Add(bar);
        AddScriptOutput(ab, "O que for aplicado aparece aqui.");
        _optCounters.Add(Count);
        Count();

        AddBloatSection(host);
    }

    private FrameworkElement DebloatRow(OptTweak t, Dictionary<string, CheckBox> checks, Action count)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var box = new CheckBox { IsChecked = t.DefaultOn, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) };
        box.Click += (_, _) => count();
        checks[t.Id] = box;
        grid.Children.Add(box);
        var text = OptTexts(t);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        var status = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Faint"), HorizontalAlignment = HorizontalAlignment.Right };
        var undo = TweakButton("DESFAZER", false, async (_, _) => await ApplyOptAsync(new[] { t }, "Desfazer · " + t.Title, undo: true));
        undo.Margin = new Thickness(0, 6, 0, 0);
        undo.Padding = new Thickness(10, 4, 10, 4);
        right.Children.Add(status);
        right.Children.Add(undo);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        OptView(t.Id, () =>
        {
            var st = _optStates.GetValueOrDefault(t.Id);
            status.Text = OptStatusText(t);
            status.Foreground = (Brush)FindResource(st?.On == true ? "ChartGpu" : "Faint");
            undo.Visibility = st?.Ours == true ? Visibility.Visible : Visibility.Collapsed;
            undo.IsEnabled = !_optBusy;
            bool done = st != null && st.On != false;
            if (done) box.IsChecked = false;
            box.IsEnabled = !done;
        });
        return grid;
    }

    // ---------------- Windows ----------------

    private void BuildOptWindows(Panel host)
    {
        var items = OptCatalog.For(OptPage.Windows).ToList();
        var tools = TweakCard(host, "AJUSTES DO WINDOWS",
            "Ligue um ajuste para aplicar; desligue para voltar exatamente como estava. O selo RECOMENDADO marca o que serve para quase todo mundo; " +
            "o ⚠ diz quando evitar. Ajustes do plano de energia, de jogos (Game DVR, otimização de janela) e de aceleração do mouse ficam em Ferramentas › Otimização.", out var tb);
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 10), ToolTip = "Pesquisar ajustes" };
        tb.Children.Add(OptText("Pesquisar", "Faint", 11, 4));
        tb.Children.Add(search);
        var cards = new StackPanel();
        var onlyRec = new CheckBox { Content = "Só os recomendados", Margin = new Thickness(0, 0, 0, 10) };
        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        string cat = "";
        foreach (var c in new[] { "" }.Concat(OptCatalog.Categories))
        {
            if (c.Length > 0 && !items.Any(t => t.Category == c)) continue;
            var chip = new RadioButton { Style = (Style)FindResource("Chip"), Content = c.Length == 0 ? "Todas" : c, GroupName = "OptWinCat", IsChecked = c.Length == 0, Margin = new Thickness(0, 0, 6, 6) };
            chip.Checked += (_, _) => { cat = c; Filter(); };
            chips.Children.Add(chip);
        }
        tb.Children.Add(chips);
        onlyRec.Click += (_, _) => Filter();
        tb.Children.Add(onlyRec);
        var counter = OptText("", "Muted", 12, 8);
        tb.Children.Add(counter);
        var bar = new WrapPanel();
        bar.Children.Add(TweakButton("APLICAR RECOMENDADOS", true, async (_, _) =>
        {
            var list = items.Where(t => t.Recommended && _optStates.GetValueOrDefault(t.Id)?.On == false).ToList();
            if (list.Count > 0 && MessageBox.Show(this, $"Aplicar {list.Count} ajuste(s) recomendado(s) que ainda não estão ativos?", "Windows", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await ApplyOptAsync(list, "Windows · recomendados");
        }));
        bar.Children.Add(TweakButton("SALVAR COMO PERFIL", false, (_, _) => SaveActiveAsProfile()));
        tb.Children.Add(bar);
        AddScriptOutput(tb, "O que for aplicado aparece aqui.");

        foreach (var t in items) cards.Children.Add(OptCard(t));
        host.Children.Add(cards);

        void Filter()
        {
            string q = search.Text.Trim();
            foreach (var card in cards.Children.OfType<Border>())
            {
                var t = (OptTweak)card.Tag;
                bool ok = (cat.Length == 0 || t.Category == cat) && (onlyRec.IsChecked != true || t.Recommended) &&
                          (q.Length == 0 || t.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) || t.Description.Contains(q, StringComparison.CurrentCultureIgnoreCase));
                card.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        search.TextChanged += (_, _) => Filter();
        void Count() => counter.Text = $"{items.Count(t => _optStates.GetValueOrDefault(t.Id)?.On == true)} de {items.Count} ativos neste PC";
        _optCounters.Add(Count);
        Count();
    }

    private void BuildOptInput(Panel host)
    {
        OptOutputCard(host);
        foreach (var (page, title, intro) in new[]
                 {
                     (OptPage.Mouse, "MOUSE", "A aceleração do mouse sozinha também está em Ferramentas › Otimização; aqui é o pacote completo de resposta do mouse."),
                     (OptPage.Teclado, "TECLADO", "Atalhos de acessibilidade e de idioma que costumam disparar no meio do jogo, e a resposta das teclas."),
                 })
        {
            TweakCard(host, title, intro, out _);
            foreach (var t in OptCatalog.For(page)) host.Children.Add(OptCard(t));
        }
    }

    private void BuildOptGpu(Panel host)
    {
        TweakCard(host, "GPU E MONITOR", "Os ajustes do driver da placa de vídeo (perfil NVIDIA, MPO e afins) ficam em Ferramentas › Drivers.", out _);
        OptOutputCard(host);
        foreach (var t in OptCatalog.For(OptPage.Gpu)) host.Children.Add(OptCard(t));
    }

    // ---------------- Perfis ----------------

    private void BuildOptProfiles(Panel host)
    {
        OptOutputCard(host);
        foreach (var p in OptCatalog.Profiles)
        {
            TweakCard(host, p.Title.ToUpperInvariant(), null, out var body);
            foreach (var line in p.Lines) body.Children.Add(OptText("• " + line, "Muted", 12, 2));
            body.Children.Add(OptText($"{p.Tweaks.Length} ajustes" + (p.Cleanup ? " + limpeza de temporários" : ""), "Faint", 11, 10));
            body.Children.Add(TweakButton("APLICAR", true, async (_, _) => await ApplyProfileAsync(p.Title, p.Tweaks, p.Cleanup)));
        }
        TweakCard(host, "PADRÃO DO WINDOWS", "Desfaz tudo o que o PcismoMaster aplicou nas páginas do TWEAKING, voltando cada valor ao que era antes.", out var def);
        def.Children.Add(TweakButton("VOLTAR AO PADRÃO", false, async (_, _) =>
        {
            var ours = OptCatalog.All.Where(Opt.Ours).ToList();
            if (ours.Count == 0) { MessageBox.Show(this, "Nada foi aplicado pelo PcismoMaster.", "Perfis"); return; }
            if (MessageBox.Show(this, $"Desfazer {ours.Count} ajuste(s) aplicados pelo PcismoMaster?", "Perfis", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await ApplyOptAsync(ours, "Voltar ao padrão do Windows", undo: true);
        }));

        TweakCard(host, "MEUS PERFIS", "Salve o que está aplicado agora como um perfil para repetir em outro PC ou depois de formatar. Dá para exportar e importar o arquivo.", out var mine);
        var bar = new WrapPanel();
        bar.Children.Add(TweakButton("CRIAR A PARTIR DO QUE ESTÁ ATIVO", true, (_, _) => SaveActiveAsProfile()));
        bar.Children.Add(TweakButton("IMPORTAR", false, (_, _) => ImportProfile()));
        mine.Children.Add(bar);
        _optProfilesList = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        mine.Children.Add(_optProfilesList);
        BuildProfileRows();
    }

    private static List<SavedOptProfile> LoadProfiles()
    {
        try { return File.Exists(OptProfilesFile) ? JsonSerializer.Deserialize<List<SavedOptProfile>>(File.ReadAllText(OptProfilesFile)) ?? new() : new(); }
        catch { return new(); }
    }

    private static void SaveProfiles(List<SavedOptProfile> list)
    {
        Directory.CreateDirectory(OptDir);
        File.WriteAllText(OptProfilesFile, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void BuildProfileRows()
    {
        if (_optProfilesList == null) return;
        _optProfilesList.Children.Clear();
        var list = LoadProfiles();
        if (list.Count == 0) { _optProfilesList.Children.Add(OptText("Nenhum perfil salvo ainda.", "Faint", 12)); return; }
        foreach (var p in list)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock { Text = $"{p.Name}  ·  {p.Ids.Length} ajustes", VerticalAlignment = VerticalAlignment.Center, MinWidth = 260, Margin = new Thickness(0, 0, 12, 8) });
            row.Children.Add(TweakButton("APLICAR", true, async (_, _) => await ApplyProfileAsync(p.Name, p.Ids, false)));
            row.Children.Add(TweakButton("EXPORTAR", false, (_, _) =>
            {
                var dlg = new SaveFileDialog { FileName = p.Name + ".pcismo.json", Filter = "Perfil do PcismoMaster|*.json" };
                if (dlg.ShowDialog(this) == true) File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            }));
            row.Children.Add(TweakButton("EXCLUIR", false, (_, _) =>
            {
                if (MessageBox.Show(this, $"Excluir o perfil \"{p.Name}\"? Os ajustes aplicados continuam no PC.", "Perfis", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                var all = LoadProfiles();
                all.RemoveAll(x => x.Name == p.Name);
                SaveProfiles(all);
                BuildProfileRows();
            }));
            _optProfilesList.Children.Add(row);
        }
    }

    private void SaveActiveAsProfile()
    {
        var ids = OptCatalog.All.Where(Opt.Ours).Select(t => t.Id).ToArray();
        if (ids.Length == 0) { MessageBox.Show(this, "Nada foi aplicado pelo PcismoMaster ainda: aplique alguns ajustes antes de salvar um perfil.", "Perfis"); return; }
        string? name = AskText("Salvar perfil", $"Nome do perfil ({ids.Length} ajustes ativos):", "Meu perfil");
        if (string.IsNullOrWhiteSpace(name)) return;
        var all = LoadProfiles();
        all.RemoveAll(x => x.Name == name.Trim());
        all.Add(new SavedOptProfile(name.Trim(), ids));
        SaveProfiles(all);
        BuildProfileRows();
        MessageBox.Show(this, $"Perfil \"{name.Trim()}\" salvo em Perfis.", "Perfis");
    }

    private void ImportProfile()
    {
        var dlg = new OpenFileDialog { Filter = "Perfil do PcismoMaster|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var p = JsonSerializer.Deserialize<SavedOptProfile>(File.ReadAllText(dlg.FileName)) ?? throw new InvalidDataException();
            var ids = p.Ids.Where(id => OptCatalog.ById(id) != null).Distinct().ToArray();
            if (string.IsNullOrWhiteSpace(p.Name) || ids.Length == 0) throw new InvalidDataException();
            var all = LoadProfiles();
            all.RemoveAll(x => x.Name == p.Name);
            all.Add(new SavedOptProfile(p.Name, ids));
            SaveProfiles(all);
            BuildProfileRows();
        }
        catch { MessageBox.Show(this, "Este arquivo não é um perfil do PcismoMaster.", "Perfis"); }
    }

    private async Task ApplyProfileAsync(string name, string[] ids, bool cleanup)
    {
        var list = ids.Select(OptCatalog.ById).OfType<OptTweak>().Where(t => _optStates.GetValueOrDefault(t.Id)?.On != true).ToList();
        var warn = list.Where(t => t.Warning != null).Select(t => "• " + t.Title).ToList();
        if (MessageBox.Show(this, $"Aplicar o perfil \"{name}\": {list.Count} ajuste(s) ainda não ativos" + (cleanup ? " e limpeza de temporários" : "") + "." +
                (warn.Count > 0 ? "\n\nCom aviso (leia em cada página):\n" + string.Join("\n", warn.Take(10)) : ""), "Perfis",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (list.Count > 0 && !await ApplyOptAsync(list, "Perfil · " + name) && !cleanup) return;
        if (cleanup) await CleanDefaultAsync();
    }

    private string? AskText(string title, string prompt, string value)
    {
        var box = new TextBox { Text = value, Margin = new Thickness(0, 6, 0, 14) };
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(box);
        var win = new Window { Title = title, Owner = this, Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("Bg"), Foreground = (Brush)FindResource("Fg"), ShowInTaskbar = false, Content = root };
        var bar = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        bar.Children.Add(TweakButton("CANCELAR", false, (_, _) => win.DialogResult = false));
        bar.Children.Add(TweakButton("SALVAR", true, (_, _) => win.DialogResult = true));
        root.Children.Add(bar);
        win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return win.ShowDialog() == true ? box.Text : null;
    }

    // ---------------- Formatação ----------------

    /// <summary>O pacote completo: perfil Básico + Debloat Essencial e Recomendado + os recomendados das outras páginas.</summary>
    private static List<OptTweak> FullBoost() =>
        OptCatalog.Profiles[0].Tweaks.Select(OptCatalog.ById).OfType<OptTweak>()
            .Concat(OptCatalog.All.Where(t => t.Id != "new_users" && (t.Level is OptLevel.Essencial or OptLevel.Recomendado || (t.Level == OptLevel.Normal && t.Recommended))))
            .Distinct().ToList();

    private void BuildOptFormat(Panel host)
    {
        var boost = FullBoost();
        TweakCard(host, "WINDOWS RECÉM-INSTALADO",
            "Para rodar logo depois de formatar. Aplica de uma vez o pacote completo de otimização (o mesmo das outras páginas, então cada item " +
            "pode ser desfeito depois em Debloat, Windows, Mouse e teclado ou GPU). Programas e jogos para instalar ficam em Instalar apps.", out var intro);
        intro.Children.Add(OptText($"Pacote completo: {boost.Count} ajustes · {boost.Count(t => t.Restart)} pedem reiniciar · {boost.Count(t => t.Warning != null)} com aviso", "Muted", 12));

        TweakCard(host, "O QUE INCLUIR", null, out var body);
        var cBoost = new CheckBox { Content = $"Pacote completo de otimização ({boost.Count} ajustes)", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
        var cPhoto = new CheckBox { Content = "Visualizador de Fotos clássico do Windows", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
        var cOne = new CheckBox { Content = "Remover o OneDrive", IsChecked = false, Margin = new Thickness(0, 0, 0, 8) };
        var cUsers = new CheckBox { Content = "Aplicar também para contas criadas depois", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
        var cClean = new CheckBox { Content = "Limpar temporários e cache de atualização no final", IsChecked = true, Margin = new Thickness(0, 0, 0, 14) };
        foreach (var c in new[] { cBoost, cPhoto, cOne, cUsers, cClean }) body.Children.Add(c);
        body.Children.Add(TweakButton("INICIAR CONFIGURAÇÃO", true, async (_, _) =>
        {
            var list = new List<OptTweak>();
            if (cBoost.IsChecked == true) list.AddRange(boost);
            if (cPhoto.IsChecked == true) list.Add(OptCatalog.ById("photo_viewer_classic")!);
            if (cOne.IsChecked == true) list.Add(OptCatalog.ById("onedrive_remove")!);
            if (cUsers.IsChecked == true) list.Add(OptCatalog.ById("new_users")!); // por último: copia o que foi aplicado
            list = list.Distinct().ToList();
            if (list.Count == 0 && cClean.IsChecked != true) return;
            if (MessageBox.Show(this, $"Aplicar {list.Count} item(ns)" + (cClean.IsChecked == true ? " e limpar temporários" : "") +
                    "?\n\nUm ponto de restauração é criado antes. Leva alguns minutos; o Explorer pisca no final.", "Formatação",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (list.Count > 0) await ApplyOptAsync(list, "Formatação · configuração completa");
            if (cClean.IsChecked == true) await CleanDefaultAsync();
            if (MessageBox.Show(this, "Configuração terminada. Reiniciar o PC agora para tudo valer?", "Formatação", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5") { CreateNoWindow = true, UseShellExecute = false });
        }));
        OptOutputCard(host);
    }

    // ---------------- Limpeza ----------------

    private async Task CleanDefaultAsync()
    {
        var cats = Cleanup.Categories.Where(c => c.DefaultOn && !c.RecycleBin).ToList();
        ScriptLog("Limpando temporários…");
        var progress = new Progress<string>(ScriptLog); // criado na tela: as linhas voltam para a tela
        long freed = await Task.Run(() => CleanCategories(cats, progress));
        ScriptLog($"✔ Limpeza: {Cleanup.Size(freed)} liberados.");
    }

    private static long CleanCategories(IEnumerable<CleanCategory> cats, IProgress<string> log)
    {
        long total = 0;
        foreach (var c in cats)
        {
            foreach (var s in c.StopServices ?? Array.Empty<string>()) Net("stop", s);
            try
            {
                var (freed, skipped) = Cleanup.Clean(c);
                total += freed;
                log.Report($"{c.Title}: {Cleanup.Size(freed)}" + (skipped > 0 ? $" ({skipped} em uso ficaram)" : ""));
            }
            catch (Exception ex) { log.Report($"{c.Title}: {ex.Message}"); }
            finally { foreach (var s in (c.StopServices ?? Array.Empty<string>()).Reverse()) Net("start", s); }
        }
        return total;

        static void Net(string verb, string svc)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("net.exe", $"{verb} {svc} /y") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
                p?.WaitForExit(30000);
            }
            catch { }
        }
    }

    private void BuildOptClean(Panel host)
    {
        TweakCard(host, "LIMPEZA",
            "Clique em ANALISAR para ver quanto cada tipo ocupa. Só arquivos que o Windows e os programas recriam sozinhos; nada pessoal. " +
            "Arquivos em uso são pulados. Limpar a memória RAM e o cache de DNS fica em Ferramentas › Otimização e Reparos do Windows.", out var body);
        var checks = new Dictionary<string, (CheckBox Box, TextBlock Size)>();
        foreach (var c in Cleanup.Categories)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = new CheckBox { IsChecked = c.DefaultOn, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) };
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = c.Title, FontWeight = FontWeights.SemiBold });
            text.Children.Add(OptText(c.Description, "Muted", 12));
            var size = new TextBlock { Text = "—", Foreground = (Brush)FindResource("Muted"), FontFamily = (FontFamily)FindResource("Mono"), Margin = new Thickness(16, 2, 0, 0) };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(size, 2);
            grid.Children.Add(box);
            grid.Children.Add(text);
            grid.Children.Add(size);
            body.Children.Add(grid);
            checks[c.Id] = (box, size);
        }
        var total = OptText("", "Fg", 13, 8);
        body.Children.Add(total);
        var bar = new WrapPanel();
        Button analyze = null!, clean = null!;
        async Task Analyze()
        {
            analyze.IsEnabled = clean.IsEnabled = false;
            total.Text = "Analisando…";
            long sum = 0;
            foreach (var c in Cleanup.Categories)
            {
                checks[c.Id].Size.Text = "…";
                var (bytes, n) = await Task.Run(() => Cleanup.Measure(c));
                checks[c.Id].Size.Text = n == 0 && bytes == 0 ? "vazio" : Cleanup.Size(bytes);
                if (checks[c.Id].Box.IsChecked == true) sum += bytes;
            }
            total.Text = $"Marcados: {Cleanup.Size(sum)}";
            analyze.IsEnabled = clean.IsEnabled = true;
        }
        analyze = TweakButton("ANALISAR", false, async (_, _) => await Analyze());
        clean = TweakButton("LIMPAR SELECIONADOS", true, async (_, _) =>
        {
            var cats = Cleanup.Categories.Where(c => checks[c.Id].Box.IsChecked == true).ToList();
            if (cats.Count == 0 || !RequireAdmin()) return;
            if (MessageBox.Show(this, "Apagar: " + string.Join(", ", cats.Select(c => c.Title)) + "?\n\nNão dá para desfazer (a Lixeira é esvaziada de vez).", "Limpeza",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            analyze.IsEnabled = clean.IsEnabled = false;
            foreach (var (log, _) in _scriptOutputs) log.Text = $"▶ Limpeza  ({DateTime.Now:HH:mm:ss})";
            ShowScriptOutputs();
            var progress = new Progress<string>(ScriptLog); // criado na tela: as linhas voltam para a tela
            long freed = await Task.Run(() => CleanCategories(cats, progress));
            ScriptLog($"✔ {Cleanup.Size(freed)} liberados.");
            await Analyze();
        });
        bar.Children.Add(analyze);
        bar.Children.Add(clean);
        body.Children.Add(bar);
        AddScriptOutput(body, "O que for limpo aparece aqui.");
    }
}
