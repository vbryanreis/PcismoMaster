using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>TWEAKING · Drivers: instalar o driver NVIDIA em silêncio e as configurações recomendadas da placa de vídeo.</summary>
public partial class MainWindow
{
    private bool _gfxInit, _gfxBusy;
    /// <summary>A placa NVIDIA deste PC (as configurações e a instalação enxuta se ajustam a ela).</summary>
    private GpuCard? _gfxCard;
    private readonly Dictionary<string, CheckBox> _gfxToggles = new();

    // Interruptores que não são do registro: as configurações do perfil global (Profile Inspector)
    private const string DrsNip = "gfx-nv-drs", DrsGsync = "gfx-nv-gsync";

    private async void InitGfxTweaks() => await InitGfxTweaksAsync();

    private async Task InitGfxTweaksAsync()
    {
        if (_gfxInit) return;
        _gfxInit = true;
        GfxStatus.Text = "Lendo o que já está aplicado…";
        // Quais fabricantes existem neste PC (pelo código PCI, mesmo sem driver).
        var (vendors, card) = await Task.Run(() =>
            (DriverManager.ReadGpus().Select(g => g.Vendor.ToLowerInvariant()).Where(v => v.Length > 0).Distinct().ToList(), GpuDetect.DetectNvidia()));
        _gfxCard = card;
        var notes = GpuRules.Adaptations(card);
        GfxCardInfo.Text = card == null ? "" : "Sua placa: " + card.Summary + (notes.Count > 0 ? ". Ajustado para ela: " + string.Join("; ", notes) + "." : ". As configurações do script servem para ela como estão.");
        GfxCardInfo.Visibility = card == null ? Visibility.Collapsed : Visibility.Visible;
        bool nvidia = vendors.Contains("nvidia");
        GfxNvInstallRow.Visibility = nvidia ? Visibility.Visible : Visibility.Collapsed;
        if (DebloatRows.Children.Count == 0) InitDebloat(vendors);
        BtnGfxNvDefault.Visibility = GfxNvInstallRow.Visibility;
        // Ajustes que a reinstalação do driver desfez: o backup antigo sai e eles voltam marcados para aplicar de novo.
        var (applied, stale) = await Task.Run(() =>
        {
            var undone = GraphicsTweaks.All.Where(t => { try { return GraphicsTweaks.DiscardIfStale(t); } catch { return false; } }).Select(t => t.Id).ToHashSet();
            try { if (GraphicsTweaks.DiscardDrsIfStale(DrsNip, GraphicsTweaks.NipFor(card))) undone.Add(DrsNip); } catch { }
            try { if (GraphicsTweaks.DiscardDrsIfStale(DrsGsync, GraphicsTweaks.GsyncOff)) undone.Add(DrsGsync); } catch { }
            return (GraphicsTweaks.All.ToDictionary(t => t.Id, GraphicsTweaks.IsApplied), undone);
        });
        bool firstTime = !applied.Values.Any(x => x) && !GraphicsTweaks.DrsApplied(DrsNip) && !GraphicsTweaks.DrsApplied(DrsGsync) && stale.Count == 0;

        void Section(string title)
            => GfxRows.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Label"), Margin = new Thickness(0, 10, 0, 8) });

        foreach (var (vendor, title) in new[] { ("nvidia", "NVIDIA"), ("amd", "AMD"), ("intel", "INTEL"), ("all", "WINDOWS · TODAS AS PLACAS") })
        {
            if (vendor != "all" && !vendors.Contains(vendor)) continue;
            Section(title);
            if (vendor == "nvidia")
            {
                GfxRow(DrsNip, "Perfil global recomendado (Profile Inspector)", false,
                    "Modo de latência baixa Ligado, VSync forçado desligado, GPU OpenGL em seleção automática, " + (card?.Laptop == true ? "" : "energia em desempenho máximo, ") + "filtragem em alto desempenho, cache de shader ilimitado, " +
                    "otimização em threads e mais (o .nip do script" + (notes.Count > 0 ? ", ajustado à sua placa" : "") + "). Antes, o valor atual de cada um é guardado.", GraphicsTweaks.DrsApplied(DrsNip), firstTime || stale.Contains(DrsNip));
                GfxRow(DrsGsync, "Desligar o G-SYNC", true,
                    "Desliga o G-SYNC em todos os jogos (parte do script). Deixe desligado se você usa monitor G-SYNC/FreeSync.", GraphicsTweaks.DrsApplied(DrsGsync), stale.Contains(DrsGsync));
            }
            foreach (var t in GraphicsTweaks.All.Where(t => t.Vendor == vendor))
                GfxRow(t.Id, t.Title, t.Advanced, t.Description, applied[t.Id], firstTime && t.DefaultOn || stale.Contains(t.Id));
        }
        GfxStatus.Text = stale.Count > 0
            ? $"A reinstalação do driver desfez {stale.Count} ajuste(s). Eles já estão marcados de novo: clique em Aplicar."
            : applied.Values.Any(x => x) || !firstTime
            ? "Os marcados como APLICADO já estão valendo. Desmarque e clique em Aplicar para desfazer um ajuste."
            : "Os recomendados já vêm marcados (os AVANÇADOS ficam desligados). Clique em Aplicar.";
    }

    private void GfxRow(string id, string title, bool advanced, string desc, bool isApplied, bool defaultOn, Panel? target = null) =>
        _gfxToggles[id] = ToggleRow(title, advanced, desc, isApplied, defaultOn, target ?? GfxRows);

    /// <summary>Linha com título, selo SEGURO/AVANÇADO (e APLICADO), descrição e o interruptor (Tag = já aplicado). Usada também no TWEAKING.</summary>
    private CheckBox ToggleRow(string title, bool advanced, string desc, bool isApplied, bool defaultOn, Panel target)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var head = new WrapPanel();
        head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var badge = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock { Text = advanced ? "AVANÇADO" : "SEGURO" } };
        if (advanced) { badge.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xCF, 0x4D)); TextElement.SetForeground(badge, (Brush)FindResource("TempWarn")); }
        head.Children.Add(badge);
        if (isApplied)
        {
            var on = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(7, 2, 7, 2),
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0x4E, 0xD1, 0x7A)), Child = new TextBlock { Text = "APLICADO" } };
            TextElement.SetForeground(on, (Brush)FindResource("ChartGpu"));
            head.Children.Add(on);
        }
        var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        text.Children.Add(head);
        text.Children.Add(new TextBlock { Text = desc, Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        grid.Children.Add(text);
        var sw = new CheckBox { VerticalAlignment = VerticalAlignment.Center, IsChecked = isApplied || defaultOn, Tag = isApplied };
        Grid.SetColumn(sw, 1);
        grid.Children.Add(sw);
        target.Children.Add(grid);
        return sw;
    }

    private void RebuildGfx() => _ = RebuildGfxAsync();
    private Task RebuildGfxAsync() { GfxRows.Children.Clear(); _gfxToggles.Clear(); _gfxInit = false; return InitGfxTweaksAsync(); }

    /// <summary>
    /// Depois de instalar o driver NVIDIA pelo app: relê o cartão (o driver limpo desfaz os ajustes) e aplica, sem perguntar,
    /// o que estiver marcado — como o "Driver Updated Install &amp; Settings" do script. Devolve o resumo para a tela.
    /// </summary>
    private async Task<string> ApplyAfterInstallAsync()
    {
        await RebuildGfxAsync();
        return await ApplyGfxAsync(ask: false);
    }

    private void BtnGfxDefaults_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (id, sw) in _gfxToggles)
            sw.IsChecked = id == DrsNip || GraphicsTweaks.All.FirstOrDefault(t => t.Id == id) is { DefaultOn: true };
    }

    private async void BtnGfxApply_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin()) return;
        string? done = await ApplyGfxAsync(ask: true);
        if (done != null) GfxStatus.Text = done;
    }

    /// <summary>Aplica os marcados e desfaz os desmarcados. <paramref name="ask"/>: confirma antes (no automático, só aplica, nunca desfaz).</summary>
    private async Task<string> ApplyGfxAsync(bool ask)
    {
        var turnOn = _gfxToggles.Where(kv => kv.Value.IsChecked == true && kv.Value.Tag is false).Select(kv => kv.Key).ToList();
        var turnOff = ask ? _gfxToggles.Where(kv => kv.Value.IsChecked != true && kv.Value.Tag is true).Select(kv => kv.Key).ToList() : new List<string>();
        if (turnOn.Count + turnOff.Count == 0) return ask ? "Nada mudou: os marcados já estão aplicados." : "As configurações marcadas já estavam aplicadas.";
        if (ask && turnOn.Contains("gfx-nv-hdcp") && MessageBox.Show(this,
                "Desligar o HDCP faz Netflix, Disney+, Prime Video e Blu-ray pararem de tocar no PC (ou tocarem em baixa qualidade). Continuar?",
                "Desligar o HDCP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return GfxStatus.Text;
        if (ask && MessageBox.Show(this, $"Aplicar {turnOn.Count} ajuste(s) e desfazer {turnOff.Count}? O valor anterior de cada um é guardado.",
                "Configurações da placa de vídeo", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return GfxStatus.Text;

        _gfxBusy = true;
        BtnGfxApply.IsEnabled = false;
        GfxStatus.Text = "Aplicando…";
        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var id in turnOff) Try(id, () =>
            {
                if (id == DrsNip || id == DrsGsync) GraphicsTweaks.RestoreDrs(id);
                else GraphicsTweaks.Engine.Restore(GraphicsTweaks.ModuleFor(GraphicsTweaks.All.First(t => t.Id == id)));
            });
            foreach (var id in turnOn.Where(id => id != DrsNip && id != DrsGsync)) Try(id, () =>
            {
                var m = GraphicsTweaks.ModuleFor(GraphicsTweaks.All.First(t => t.Id == id));
                if (m.Settings.Length == 0) throw new InvalidOperationException("nenhuma chave deste ajuste neste PC");
                GraphicsTweaks.Engine.Enable(m);
            });
        });
        // Perfil global NVIDIA: com novas tentativas e, se preciso, num processo novo (driver recém-instalado).
        foreach (var id in turnOn.Where(id => id == DrsNip || id == DrsGsync))
        {
            GfxStatus.Text = "Aplicando o perfil global da NVIDIA…";
            if (await GraphicsTweaks.ApplyDrsRobustAsync(id) is string err) errors.Add($"{id}: {err}");
        }
        void Try(string id, Action a) { try { a(); } catch (Exception ex) { lock (errors) errors.Add($"{id}: {ex.Message}"); } }

        _gfxBusy = false;
        BtnGfxApply.IsEnabled = true;
        BtnGfxReboot.Visibility = Visibility.Visible;
        await RebuildGfxAsync();
        return (errors.Count == 0 ? $"Pronto: {turnOn.Count} configuração(ões) aplicada(s)" + (turnOff.Count > 0 ? $", {turnOff.Count} desfeita(s)." : ".") : $"Concluído com {errors.Count} erro(s): " + string.Join(" · ", errors) + ".")
               + " Os ajustes de driver (P0, preempção, clock gating, serviços, modo MSI, HDCP…) só valem depois de reiniciar.";
    }

    private void BtnGfxUndoAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var sw in _gfxToggles.Values) sw.IsChecked = false;
        BtnGfxApply_Click(sender, e);
    }

    private async void BtnGfxNvPanel_Click(object sender, RoutedEventArgs e)
    {
        if (GraphicsTweaks.HasNvidiaControlPanel()) { try { GraphicsTweaks.OpenNvidiaControlPanel(); } catch (Exception ex) { GfxStatus.Text = ex.Message; } return; }
        if (_gfxBusy || !RequireAdmin()) return;
        _gfxBusy = true;
        try
        {
            GfxStatus.Text = "Instalando o Painel de Controle NVIDIA pela Microsoft Store…";
            GfxStatus.Text = await NvidiaTools.InstallControlPanelAsync(CancellationToken.None);
            if (GraphicsTweaks.HasNvidiaControlPanel()) GraphicsTweaks.OpenNvidiaControlPanel();
        }
        catch (Exception ex) { GfxStatus.Text = "Não foi possível abrir o Painel de Controle NVIDIA: " + ex.Message; }
        finally { _gfxBusy = false; }
    }

    /// <summary>"NVIDIA Settings: Default" do script: perfil global de volta ao padrão do driver.</summary>
    private async void BtnGfxNvDefault_Click(object sender, RoutedEventArgs e)
    {
        if (_gfxBusy || !RequireAdmin()) return;
        if (MessageBox.Show(this,
                "Voltar o perfil global da NVIDIA ao padrão do driver?\n\n" +
                "Sai tudo o que foi mudado nele: o perfil recomendado, o G-SYNC desligado e também o que você ou outros programas mudaram " +
                "(Painel de Controle, Profile Inspector). Os perfis de jogos não são tocados. Isso não tem desfazer.",
                "Perfil global NVIDIA", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _gfxBusy = true;
        try
        {
            await Task.Run(() => GraphicsTweaks.ResetGlobalDrs(DrsNip, DrsGsync));
            RebuildGfx();
            await Task.Delay(300);
            GfxStatus.Text = "Perfil global no padrão do driver.";
        }
        catch (Exception ex) { GfxStatus.Text = "Não foi possível restaurar o perfil global: " + ex.Message; }
        finally { _gfxBusy = false; }
    }

    private void BtnGfxReboot_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Reiniciar o PC agora? Salve e feche seus programas.", "Reiniciar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5") { UseShellExecute = false, CreateNoWindow = true });
    }

}
