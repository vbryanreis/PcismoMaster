using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Perfil na lista da esquerda.</summary>
public sealed record NpiProfileVm(string Name, string Display, string Detail, bool IsGlobal);

/// <summary>Linha da lista de configurações: cabeçalho de grupo ou uma configuração editável.</summary>
public sealed class NpiRowVm : INotifyPropertyChanged
{
    public bool IsHeader { get; init; }
    public string Name { get; init; } = "";
    public DrsItem? Item { get; init; }
    public List<string> Choices { get; init; } = new();
    public string OriginalText { get; init; } = "";

    private string _edit = "";
    private bool _reset;
    public string EditText
    {
        get => _edit;
        set { if (_edit == value) return; _edit = value; _reset = false; Notify(); }
    }
    /// <summary>Marcado para sair do perfil (voltar a herdar) ao aplicar.</summary>
    public bool ResetPending { get => _reset; set { _reset = value; Notify(); } }
    public bool IsDirty => !IsHeader && (_reset || _edit != OriginalText);

    public bool Editable => Item?.Type != DrsType.Binary;
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RowVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ResetVisibility => !IsHeader && (Item?.Source == DrsSource.User || IsDirty) ? Visibility.Visible : Visibility.Collapsed;
    public FontWeight NameWeight => Item?.Source == DrsSource.User || IsDirty ? FontWeights.SemiBold : FontWeights.Normal;
    public Brush NameBrush => Res(IsDirty ? "TempWarn" : Item?.Source == DrsSource.User ? "Accent" : "Fg");
    public string SourceText => _reset ? "→ herdar" : IsDirty ? "alterada" : Item?.Source switch
    {
        DrsSource.User => "você", DrsSource.Nvidia => "NVIDIA", DrsSource.Global => "global", _ => "padrão",
    };
    public Brush SourceBrush => Res(IsDirty ? "TempWarn" : Item?.Source switch
    {
        DrsSource.User => "Accent", DrsSource.Nvidia => "AccentCyan", DrsSource.Global => "Muted", _ => "Faint",
    });
    public string Tip => Item == null ? "" : $"{Item.Name}  ·  ID 0x{Item.Id:X8}" + (Item.Meta?.Description is { Length: > 0 } d ? "\n\n" + d : "");

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()
    {
        foreach (var n in new[] { nameof(EditText), nameof(ResetPending), nameof(IsDirty), nameof(ResetVisibility), nameof(NameWeight),
                                  nameof(NameBrush), nameof(SourceText), nameof(SourceBrush) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

/// <summary>FERRAMENTAS · Drivers · Profile Inspector: configurações dos perfis do driver NVIDIA (DRS), no estilo do nvidiaProfileInspector.</summary>
public partial class MainWindow
{
    private bool _npiInit, _npiBusy;
    private string _npiBase = "Base Profile";
    private List<NpiProfileVm> _npiProfiles = new();
    private DrsProfileData? _npiData;
    private List<NpiRowVm> _npiRows = new();

    private void DrvTab_Changed(object sender, RoutedEventArgs e)
    {
        if (DrvPanel == null || NpiPanel == null || NvcpPanel == null) return;
        bool npi = DrvTabNpi.IsChecked == true, nvcp = DrvTabNvcp.IsChecked == true;
        DrvPanel.Visibility = npi || nvcp ? Visibility.Collapsed : Visibility.Visible;
        NpiPanel.Visibility = npi ? Visibility.Visible : Visibility.Collapsed;
        NvcpPanel.Visibility = nvcp ? Visibility.Visible : Visibility.Collapsed;
        if (npi) InitNpi();
        if (nvcp) InitNvcp();
    }

    private async void InitNpi()
    {
        if (_npiInit) return;
        _npiInit = true;
        if (!NvDrs.Available)
        {
            NpiStatus.Text = "O Profile Inspector precisa de uma placa de vídeo NVIDIA com o driver oficial instalado.";
            return;
        }
        await ReloadNpiProfilesAsync(select: null);
    }

    private async Task ReloadNpiProfilesAsync(string? select)
    {
        NpiStatus.Text = "Lendo os perfis do driver…";
        try
        {
            var (baseName, list) = await Task.Run(() => { _ = DrsCatalog.All; return ProfileInspector.LoadProfiles(); });
            _npiBase = baseName;
            _npiProfiles = new List<NpiProfileVm> { new(baseName, $"{ProfileInspector.GlobalLabel} ({baseName})", "vale para todos os jogos", true) };
            _npiProfiles.AddRange(list.Select(p => new NpiProfileVm(p.Name, p.Name,
                (p.Predefined ? "NVIDIA" : "criado por você") + (p.Apps > 0 ? $" · {p.Apps} executável(is)" : ""), false)));
            NpiStatus.Text = "";
            ApplyNpiProfileFilter();
            NpiProfiles.SelectedItem = _npiProfiles.FirstOrDefault(p => p.Name == (select ?? baseName)) ?? _npiProfiles[0];
        }
        catch (Exception ex) { NpiStatus.Text = "Não foi possível ler os perfis: " + ex.Message; }
    }

    private void ApplyNpiProfileFilter()
    {
        string q = NpiProfileSearch.Text.Trim();
        var keep = NpiProfiles.SelectedItem;
        var view = q.Length == 0 ? _npiProfiles : _npiProfiles.Where(p => p.IsGlobal || p.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        NpiProfiles.ItemsSource = view;
        NpiProfileCount.Text = $"{view.Count - 1:N0} de {_npiProfiles.Count - 1:N0} perfis de jogos";
        if (keep != null && view.Contains(keep)) NpiProfiles.SelectedItem = keep;
    }

    private void NpiProfileSearch_Changed(object sender, TextChangedEventArgs e) { if (_npiInit) ApplyNpiProfileFilter(); }

    private bool NpiHasPending => _npiRows.Any(r => r.IsDirty);

    private async void NpiProfiles_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (NpiProfiles.SelectedItem is not NpiProfileVm p || p.Name == _npiData?.Name) return;
        if (NpiHasPending && MessageBox.Show(this, "Há alterações não aplicadas neste perfil. Descartar?", "Profile Inspector",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            NpiProfiles.SelectedItem = _npiProfiles.FirstOrDefault(x => x.Name == _npiData?.Name);
            return;
        }
        await LoadNpiProfileAsync(p.Name);
    }

    private async Task LoadNpiProfileAsync(string name)
    {
        if (_npiBusy) return;
        _npiBusy = true;
        NpiStatus.Text = "Lendo as configurações…";
        try
        {
            bool all = NpiShowAll.IsChecked == true;
            string baseName = _npiBase;
            _npiData = await Task.Run(() => ProfileInspector.Load(name, baseName, all));
            NpiTitle.Text = _npiData.IsGlobal ? $"{ProfileInspector.GlobalLabel} ({name})" : name;
            BtnNpiDelete.IsEnabled = !_npiData.IsGlobal;
            BuildNpiApps();
            _npiRows = _npiData.Items.Select(i => new NpiRowVm
            {
                Item = i, Name = i.Name, OriginalText = i.ValueText,
                Choices = i.Meta?.Choices.Select(c => c.Name).ToList() ?? new List<string>(),
            }).ToList();
            foreach (var r in _npiRows) r.EditText = r.OriginalText;
            int mine = _npiData.Items.Count(i => i.Source == DrsSource.User);
            NpiStatus.Text = $"{_npiData.Items.Count} configurações · {mine} alterada(s) por você neste perfil.";
            ApplyNpiFilter();
        }
        catch (Exception ex) { NpiStatus.Text = "Não foi possível ler o perfil: " + ex.Message; }
        finally { _npiBusy = false; }
    }

    private void BuildNpiApps()
    {
        NpiApps.Children.Clear();
        if (_npiData == null || _npiData.IsGlobal) return;
        foreach (var app in _npiData.Apps)
        {
            var chip = new Border { Style = (Style)FindResource("MaintBadge"), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 3, 4, 3) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center, ToolTip = app.IsPredefined ? "Executável definido pela NVIDIA" : "Executável adicionado por você" });
            var rm = new Button { Content = "✕", Style = (Style)FindResource("OutlineButton"), BorderThickness = new Thickness(0), Padding = new Thickness(5, 0, 5, 0), FontSize = 10,
                Margin = new Thickness(4, 0, 0, 0), ToolTip = "Tirar este executável do perfil" };
            string exe = app.Name;
            rm.Click += async (_, _) => await NpiChangeAsync($"Tirar {exe} do perfil?", () => ProfileInspector.RemoveApp(_npiData!.Name, exe), reloadList: false);
            row.Children.Add(rm);
            chip.Child = row;
            NpiApps.Children.Add(chip);
        }
        var add = new Button { Content = "+ Adicionar .exe", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(10, 3, 10, 3), FontSize = 11,
            Margin = new Thickness(0, 0, 6, 6) };
        add.Click += async (_, _) =>
        {
            var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe", Title = "Executável do jogo para este perfil" };
            if (dlg.ShowDialog(this) != true) return;
            string exe = Path.GetFileName(dlg.FileName);
            await NpiChangeAsync(null, () => ProfileInspector.AddApp(_npiData!.Name, exe), reloadList: false);
        };
        NpiApps.Children.Add(add);
    }

    private void ApplyNpiFilter()
    {
        string q = NpiSettingSearch.Text.Trim();
        bool onlyChanged = NpiOnlyChanged.IsChecked == true;
        var visible = _npiRows.Where(r =>
            (!onlyChanged || r.IsDirty || r.Item!.Source == DrsSource.User) &&
            (q.Length == 0 || r.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || $"0x{r.Item!.Id:X8}".Contains(q, StringComparison.OrdinalIgnoreCase)
             || r.EditText.Contains(q, StringComparison.CurrentCultureIgnoreCase)));
        var list = new List<NpiRowVm>();
        foreach (var g in visible.GroupBy(r => r.Item!.Group))
        {
            list.Add(new NpiRowVm { IsHeader = true, Name = DrsCatalog.GroupTitle(g.Key) });
            list.AddRange(g);
        }
        NpiSettings.ItemsSource = list;
    }

    private void NpiFilter_Changed(object sender, RoutedEventArgs e) { if (_npiData != null) ApplyNpiFilter(); }

    private async void NpiShowAll_Changed(object sender, RoutedEventArgs e)
    {
        if (_npiData == null) return;
        if (NpiHasPending && MessageBox.Show(this, "Recarregar descarta as alterações não aplicadas. Continuar?", "Profile Inspector",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) { NpiShowAll.IsChecked = !NpiShowAll.IsChecked; return; }
        await LoadNpiProfileAsync(_npiData.Name);
    }

    private void BtnNpiReset_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not NpiRowVm r) return;
        if (r.IsDirty && !r.ResetPending && r.Item!.Source != DrsSource.User) { r.EditText = r.OriginalText; return; } // só desfaz a edição
        r.ResetPending = true;
    }

    private async void BtnNpiUndo_Click(object sender, RoutedEventArgs e) { if (_npiData != null) await LoadNpiProfileAsync(_npiData.Name); }

    private async void BtnNpiApply_Click(object sender, RoutedEventArgs e)
    {
        if (_npiData == null || !RequireAdmin()) return;
        var changes = new List<DrsChange>();
        foreach (var r in _npiRows.Where(r => r.IsDirty))
        {
            var i = r.Item!;
            if (r.ResetPending) { changes.Add(new DrsChange(i.Id, i.Type, 0, null, true)); continue; }
            if (i.Type is DrsType.WString or DrsType.String) { changes.Add(new DrsChange(i.Id, i.Type, 0, r.EditText, false)); continue; }
            if (DrsCatalog.Parse(i.Meta, r.EditText) is not ulong n)
            {
                NpiStatus.Text = $"\"{r.EditText}\" não é um valor válido para {i.Name}. Escolha da lista, ou digite um número (ex.: 0x00000001 ou 1).";
                return;
            }
            changes.Add(new DrsChange(i.Id, i.Type == DrsType.Qword ? DrsType.Qword : DrsType.Dword, n, null, false));
        }
        if (changes.Count == 0) { NpiStatus.Text = "Nenhuma alteração para aplicar."; return; }
        string name = _npiData.Name, baseName = _npiBase;
        await NpiChangeAsync(null, () => ProfileInspector.Apply(name, baseName, changes), reloadList: false,
            done: $"{changes.Count} configuração(ões) aplicada(s) em {(_npiData.IsGlobal ? ProfileInspector.GlobalLabel : name)}. Vale na próxima vez que o jogo abrir.");
    }

    private async void BtnNpiRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_npiData == null) return;
        string name = _npiData.Name, baseName = _npiBase;
        await NpiChangeAsync($"Restaurar o padrão de \"{NpiTitle.Text}\"? Tudo o que você mudou neste perfil é desfeito (os valores da NVIDIA voltam).",
            () => ProfileInspector.RestoreProfile(name, baseName), reloadList: false, done: "Perfil restaurado para o padrão do driver.");
    }

    private async void BtnNpiNew_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        string? name = Prompt("Novo perfil", "Nome do perfil (ex.: o nome do jogo):");
        if (string.IsNullOrWhiteSpace(name)) return;
        string? exe = null;
        if (MessageBox.Show(this, "Escolher agora o executável do jogo para este perfil?", "Profile Inspector", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe" };
            if (dlg.ShowDialog(this) == true) exe = Path.GetFileName(dlg.FileName);
        }
        string n = name.Trim();
        await NpiChangeAsync(null, () => ProfileInspector.CreateProfile(n, exe), reloadList: true, select: n, done: $"Perfil \"{n}\" criado.");
    }

    private async void BtnNpiDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_npiData == null || _npiData.IsGlobal) return;
        string name = _npiData.Name;
        await NpiChangeAsync($"Excluir o perfil \"{name}\"? Se for um perfil da NVIDIA, o driver pode recriá-lo numa atualização.",
            () => ProfileInspector.DeleteProfile(name), reloadList: true, done: $"Perfil \"{name}\" excluído.");
    }

    private void BtnNpiExport_Click(object sender, RoutedEventArgs e)
    {
        if (_npiData == null) return;
        var dlg = new SaveFileDialog { Filter = "Perfil do NVIDIA Profile Inspector (*.nip)|*.nip", FileName = string.Concat(_npiData.Name.Split(Path.GetInvalidFileNameChars())) + ".nip" };
        if (dlg.ShowDialog(this) != true) return;
        try { ProfileInspector.Export(new[] { _npiData.Name }, _npiBase, dlg.FileName); NpiStatus.Text = "Exportado: " + dlg.FileName; }
        catch (Exception ex) { NpiStatus.Text = "Não foi possível exportar: " + ex.Message; }
    }

    private async void BtnNpiImport_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var dlg = new OpenFileDialog { Filter = "Perfil do NVIDIA Profile Inspector (*.nip)|*.nip" };
        if (dlg.ShowDialog(this) != true) return;
        string file = dlg.FileName, baseName = _npiBase;
        int count = 0;
        await NpiChangeAsync($"Importar \"{Path.GetFileName(file)}\"? As configurações do arquivo são gravadas nos perfis dele.",
            () => count = ProfileInspector.Import(file, baseName), reloadList: true, select: _npiData?.Name, done: null);
        if (count > 0) NpiStatus.Text = $"{count} perfil(is) importado(s) de {Path.GetFileName(file)}.";
    }

    /// <summary>Confirma (se houver pergunta), roda a gravação fora da thread da janela e recarrega o perfil (ou a lista).</summary>
    private async Task NpiChangeAsync(string? confirm, Action work, bool reloadList, string? select = null, string? done = null)
    {
        if (!RequireAdmin()) return;
        if (confirm != null && MessageBox.Show(this, confirm, "Profile Inspector", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        BtnNpiApply.IsEnabled = false;
        NpiStatus.Text = "Gravando no driver…";
        string? keep = select ?? _npiData?.Name;
        try
        {
            await Task.Run(work);
            _npiData = null;
            if (reloadList) await ReloadNpiProfilesAsync(keep);
            else if (keep != null) await LoadNpiProfileAsync(keep);
            if (done != null) NpiStatus.Text = done;
        }
        catch (DrsException ex) when (ex.Status == -167) { NpiStatus.Text = "Esse executável já pertence a outro perfil. Tire-o de lá primeiro."; }
        catch (Exception ex) { NpiStatus.Text = "Não foi possível gravar: " + ex.Message; }
        finally { BtnNpiApply.IsEnabled = true; }
    }

    /// <summary>Pergunta simples com uma caixa de texto, no tema do app.</summary>
    private string? Prompt(string title, string label)
    {
        var box = new TextBox { Margin = new Thickness(0, 8, 0, 14) };
        var ok = new Button { Content = "OK", Style = (Style)FindResource("PrimaryButton"), Padding = new Thickness(18, 7, 18, 7), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancelar", Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(14, 7, 14, 7), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("Fg") });
        stack.Children.Add(box);
        stack.Children.Add(buttons);
        var w = new Window { Title = title, Owner = this, Content = stack, SizeToContent = SizeToContent.WidthAndHeight, MinWidth = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = (Brush)FindResource("Card"), ShowInTaskbar = false };
        ok.Click += (_, _) => w.DialogResult = true;
        w.Loaded += (_, _) => box.Focus();
        return w.ShowDialog() == true ? box.Text : null;
    }
}
