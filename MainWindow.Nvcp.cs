using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>Item da lista de uma linha: a opção (null = usar global/padrão), "Outro valor…" do limite de FPS ou o valor atual desconhecido.</summary>
public sealed record NvcpItem(string Label, PanelChoice? Choice, bool Other = false, bool Keep = false)
{
    public override string ToString() => Label;
}

/// <summary>FERRAMENTAS · Drivers · Painel NVIDIA: "Gerenciar as configurações em 3D" do Painel de Controle NVIDIA.</summary>
public partial class MainWindow
{
    private bool _nvcpInit, _nvcpBusy;
    private string _nvcpBase = "Base Profile", _nvcpProfile = "";
    private List<NpiProfileVm> _nvcpProfiles = new();
    private readonly List<(PanelSetting Setting, ComboBox Box, int Original)> _nvcpRows = new();
    private bool _nvcpHybrid;
    private List<(int DeviceId, string Name)> _nvcpGpus = new();

    private async void InitNvcp()
    {
        if (_nvcpInit) return;
        _nvcpInit = true;
        if (!NvDrs.Available)
        {
            NvcpStatus.Text = "O Painel NVIDIA precisa de uma placa de vídeo NVIDIA com o driver oficial instalado.";
            return;
        }
        NvcpStatus.Text = "Lendo os perfis do driver…";
        try
        {
            var (baseName, list, hybrid) = await Task.Run(() =>
            {
                var (b, l) = ProfileInspector.LoadProfiles();
                _nvcpGpus = DriverManager.ReadGpus().Select(g => (g.DeviceId, g.GpuName)).ToList();
                return (b, l, GpuDetect.DetectNvidia()?.Hybrid == true);
            });
            _nvcpBase = baseName;
            _nvcpHybrid = hybrid;
            // Como no Painel: primeiro as configurações globais, depois os programas.
            _nvcpProfiles = new List<NpiProfileVm> { new(baseName, "Configurações globais", "valem para todos os jogos e programas", true) };
            _nvcpProfiles.AddRange(list.Select(p => new NpiProfileVm(p.Name, p.Name, p.Apps > 0 ? $"{p.Apps} executável(is)" : "sem executáveis", false)));
            NvcpStatus.Text = "";
            ApplyNvcpFilter();
            NvcpProfiles.SelectedItem = _nvcpProfiles[0];
        }
        catch (Exception ex) { NvcpStatus.Text = "Não foi possível ler os perfis: " + ex.Message; }
    }

    private void ApplyNvcpFilter()
    {
        string q = NvcpProfileSearch.Text.Trim();
        var keep = NvcpProfiles.SelectedItem;
        var view = q.Length == 0 ? _nvcpProfiles : _nvcpProfiles.Where(p => p.IsGlobal || p.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        NvcpProfiles.ItemsSource = view;
        NvcpProfileCount.Text = $"{view.Count - 1:N0} de {_nvcpProfiles.Count - 1:N0} programas";
        if (keep != null && view.Contains(keep)) NvcpProfiles.SelectedItem = keep;
    }

    private void NvcpProfileSearch_Changed(object sender, TextChangedEventArgs e) { if (_nvcpInit) ApplyNvcpFilter(); }

    private bool NvcpPending => _nvcpRows.Any(r => r.Box.SelectedIndex != r.Original);

    private async void NvcpProfiles_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NvcpProfiles.SelectedItem is not NpiProfileVm p || p.Name == _nvcpProfile) return;
        if (NvcpPending && MessageBox.Show(this, "Há alterações não aplicadas. Descartar?", "Painel NVIDIA",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            NvcpProfiles.SelectedItem = _nvcpProfiles.FirstOrDefault(x => x.Name == _nvcpProfile);
            return;
        }
        await LoadNvcpAsync(p.Name);
    }

    private async Task LoadNvcpAsync(string name)
    {
        if (_nvcpBusy) return;
        _nvcpBusy = true;
        bool global = name == _nvcpBase;
        try
        {
            var settings = NvidiaPanel.All.Where(s => !s.HybridOnly || _nvcpHybrid).ToList();
            string baseName = _nvcpBase;
            var states = await Task.Run(() => NvidiaPanel.Read(name, baseName, settings));
            _nvcpProfile = name;
            NvcpTitle.Text = global ? "Configurações globais" : name;
            NvcpSubtitle.Text = global
                ? "Valem para todos os jogos, menos o que um programa tiver diferente. \"Padrão do driver\" tira a sua alteração."
                : "Só para este programa. \"Usar a configuração global\" tira a alteração e ele volta a seguir as globais.";
            BtnNvcpRestore.Content = global ? "Restaurar padrão do driver" : "Restaurar este programa";
            NvcpRows.Children.Clear();
            _nvcpRows.Clear();
            foreach (var st in states) NvcpRow(st, global);
            NvcpStatus.Text = "";
        }
        catch (Exception ex) { NvcpStatus.Text = "Não foi possível ler as configurações: " + ex.Message; }
        finally { _nvcpBusy = false; }
    }

    /// <summary>Linha: nome (com a explicação embaixo), lista de opções e de onde vem o valor.</summary>
    private void NvcpRow(PanelState st, bool global)
    {
        if (st.Setting.Info) { NvcpInfoRow(st.Setting); return; }
        var items = new List<NvcpItem>();
        string? textNow = st.Setting.Text ? NvidiaPanel.Describe(st.Setting, st.TextValue, _nvcpGpus) : null;
        string inherited = st.SetHere ? "" : st.Current is { } c ? $" ({c.Label})" : textNow is { Length: > 0 } t && t != st.Setting.AutoLabel ? $" ({t})" : "";
        string auto = global ? st.Setting.AutoLabel ?? "Padrão do driver" : st.Predefined ? "Padrão da NVIDIA para este jogo" : "Usar a configuração global";
        items.Add(new NvcpItem(auto + inherited, null));
        if (st.Setting.Text && st.SetHere) items.Add(new NvcpItem(textNow!, null, Keep: true));
        IEnumerable<PanelChoice> choices = st.Setting.Fps
            ? new[] { 0u }.Concat(NvidiaPanel.FpsPresets).Concat(st.SetHere && st.Raw is uint f && f != 0 && !NvidiaPanel.FpsPresets.Contains(f) ? new[] { f } : Array.Empty<uint>())
                .OrderBy(x => x).Select(x => NvidiaPanel.FpsChoice(st.Setting.Ids[0], x))
            : st.Setting.Choices;
        items.AddRange(choices.Select(ch => new NvcpItem(ch.Label, ch)));
        if (st.Setting.Fps) items.Add(new NvcpItem("Outro valor…", null, Other: true));

        int selected = 0;
        if (st.SetHere)
        {
            int i = st.Setting.Text ? 1 : st.Current == null ? -1 : items.FindIndex(x => x.Choice is { } ch && ch.Writes.SequenceEqual(st.Current.Writes));
            if (i < 0) { items.Insert(1, new NvcpItem($"Personalizado ({(st.Raw is uint r ? $"0x{r:X8}" : "?")})", null, Keep: true)); i = 1; }
            selected = i;
        }

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        var text = new StackPanel { Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = st.Setting.Title, FontWeight = st.SetHere ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = st.Setting.Help, Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(text);
        var box = new ComboBox { ItemsSource = items, SelectedIndex = selected, VerticalAlignment = VerticalAlignment.Center };
        var source = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        void Mark()
        {
            bool dirty = box.SelectedIndex != selected;
            source.Text = dirty ? "alterada" : st.SetHere ? "você" : st.Predefined ? "NVIDIA" : global ? "padrão" : "global";
            source.Foreground = (Brush)FindResource(dirty ? "TempWarn" : st.SetHere ? "Accent" : st.Predefined ? "AccentCyan" : "Faint");
        }
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is NvcpItem { Other: true })
            {
                string? typed = Prompt(st.Setting.Title, "Limite de FPS (5 a 1000; 0 = desligado):");
                if (uint.TryParse(typed?.Trim(), out uint fps) && (fps == 0 || fps is >= 5 and <= 1000))
                {
                    var ch = NvidiaPanel.FpsChoice(st.Setting.Ids[0], fps);
                    int at = items.FindIndex(x => x.Choice is { } c2 && c2.Writes.SequenceEqual(ch.Writes));
                    if (at < 0) { items.Insert(items.Count - 1, new NvcpItem(ch.Label, ch)); at = items.Count - 2; box.Items.Refresh(); }
                    box.SelectedIndex = at;
                }
                else box.SelectedIndex = selected;
                return;
            }
            Mark();
        };
        Mark();
        Grid.SetColumn(box, 1);
        Grid.SetColumn(source, 2);
        grid.Children.Add(box);
        grid.Children.Add(source);
        NvcpRows.Children.Add(grid);
        _nvcpRows.Add((st.Setting, box, selected));
    }

    /// <summary>Linha só de informação (ex.: DSR, que fica na configuração do monitor) com atalho para o Painel de Controle.</summary>
    private void NvcpInfoRow(PanelSetting st)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        var text = new StackPanel { Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = st.Title, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = st.Help, Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(text);
        var open = new Button { Content = "Abrir no Painel de Controle NVIDIA", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        open.Click += (_, _) =>
        {
            try { if (!GraphicsTweaks.OpenNvidiaControlPanel()) NvcpStatus.Text = "O Painel de Controle NVIDIA não está instalado: a página dele na Microsoft Store foi aberta."; }
            catch (Exception ex) { NvcpStatus.Text = "Não foi possível abrir o Painel de Controle: " + ex.Message; }
        };
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);
        NvcpRows.Children.Add(grid);
    }

    private async void BtnNvcpApply_Click(object sender, RoutedEventArgs e)
    {
        if (_nvcpBusy || !RequireAdmin() || _nvcpProfile.Length == 0) return;
        var changed = _nvcpRows.Where(r => r.Box.SelectedIndex != r.Original && r.Box.SelectedItem is NvcpItem { Keep: false, Other: false }).ToList();
        if (changed.Count == 0) { NvcpStatus.Text = "Nada mudou."; return; }
        var changes = changed.SelectMany(r => NvidiaPanel.Changes(r.Setting, ((NvcpItem)r.Box.SelectedItem).Choice)).ToList();
        string name = _nvcpProfile, baseName = _nvcpBase;
        _nvcpBusy = true;
        BtnNvcpApply.IsEnabled = false;
        try
        {
            await Task.Run(() => ProfileInspector.Apply(name, baseName, changes));
            _nvcpBusy = false;
            await LoadNvcpAsync(name);
            NvcpStatus.Text = $"{changed.Count} configuração(ões) aplicada(s). Vale na próxima vez que o jogo abrir.";
        }
        catch (Exception ex) { NvcpStatus.Text = "Não foi possível gravar: " + ex.Message; }
        finally { _nvcpBusy = false; BtnNvcpApply.IsEnabled = true; }
    }

    private void BtnNvcpDiscard_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _nvcpRows) r.Box.SelectedIndex = r.Original;
        NvcpStatus.Text = "";
    }

    private async void BtnNvcpRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_nvcpBusy || !RequireAdmin() || _nvcpProfile.Length == 0) return;
        bool global = _nvcpProfile == _nvcpBase;
        if (MessageBox.Show(this, global
                ? "Voltar todas as configurações globais ao padrão do driver?\nSai tudo o que foi mudado no perfil global, por aqui, pelo Painel de Controle ou pelo Profile Inspector. Não tem desfazer."
                : $"Voltar \"{_nvcpProfile}\" ao padrão da NVIDIA?\nSai tudo o que foi mudado neste programa. Não tem desfazer.",
                "Restaurar", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        string name = _nvcpProfile, baseName = _nvcpBase;
        _nvcpBusy = true;
        try
        {
            await Task.Run(() => { if (global) GraphicsTweaks.ResetGlobalDrs(DrsNip, DrsGsync); else ProfileInspector.RestoreProfile(name, baseName); });
            _nvcpBusy = false;
            await LoadNvcpAsync(name);
            NvcpStatus.Text = global ? "Configurações globais no padrão do driver." : "Programa restaurado ao padrão da NVIDIA.";
        }
        catch (Exception ex) { NvcpStatus.Text = "Não foi possível restaurar: " + ex.Message; }
        finally { _nvcpBusy = false; }
    }
}
