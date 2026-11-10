using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// Aba Configurações: atualização do programa, idioma (português, inglês, espanhol), tema (atual ou Legacy) e as cores
/// (destaque, textos, fundo, cartões e ícones). A tradução troca os textos das telas na hora, guardando o original em
/// cada elemento para poder voltar ao português.
/// </summary>
public partial class MainWindow
{
    private bool _settingsBuilt;

    private void InitSettings()
    {
        if (_settingsBuilt) return;
        _settingsBuilt = true;
        var host = SettingsHost;

        // ---- Atualização ----
        TweakCard(host, "", "Atualização do programa", $"Versão instalada: v{AppUpdater.Current.ToString(3)}. O PcismoMaster também procura sozinho ao abrir.", out var upd);
        var updStatus = OptText("", "Muted", 12, 8);
        upd.Children.Add(updStatus);
        upd.Children.Add(TweakButton("Procurar atualização agora", true, async (_, _) =>
        {
            if (!AppUpdater.Supported) { updStatus.Text = Loc.T("A atualização automática só funciona no .exe publicado (este é um build de teste)."); return; }
            updStatus.Text = Loc.T("Procurando atualização…");
            await CheckUpdateAsync(true);
            updStatus.Text = UpdText.Text;
        }));

        // ---- Idioma ----
        TweakCard(host, "", "Idioma", "Português, inglês ou espanhol. Troca na hora; o que ainda não tem tradução continua em português.", out var lang);
        var langRow = new WrapPanel();
        foreach (var (code, name) in Loc.Languages)
        {
            var chip = new RadioButton { Style = (Style)FindResource("Chip"), Content = name, GroupName = "AppLang", IsChecked = Loc.Lang == code, Margin = new Thickness(0, 0, 8, 6) };
            chip.Checked += (_, _) => SetLanguage(code);
            langRow.Children.Add(chip);
        }
        lang.Children.Add(langRow);

        // ---- Tema ----
        TweakCard(host, "", "Tema", "O visual atual ou o Legacy (o antigo, da 0.12.0). Dá para alternar quando quiser.", out var theme);
        theme.Children.Add(ThemeChoice(this));

        // ---- Cores ----
        TweakCard(host, "", "Cores", "Destaque, textos, fundo do programa, cartões e as três cores dos ícones.", out var colors);
        BuildColorPicker(colors);
        TranslateUi();
    }

    // ---------- Idioma ----------

    private DispatcherTimer? _locTimer;

    private void InitLanguage()
    {
        Loc.Lang = _settings.Language is "en" or "es" ? _settings.Language : "pt";
        StartLocTimer();
    }

    private void SetLanguage(string code)
    {
        if (Loc.Lang == code) return;
        Loc.Lang = code;
        _settings.Language = code;
        try { _settings.Save(); } catch { }
        StartLocTimer();
        TranslateUi();
    }

    /// <summary>Fora do português, revisa a tela a cada segundo: páginas montadas depois (listas, cartões) também são traduzidas.</summary>
    private void StartLocTimer()
    {
        if (Loc.Lang == "pt") { _locTimer?.Stop(); return; }
        if (_locTimer == null)
        {
            _locTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            _locTimer.Tick += (_, _) => { if (IsVisible && WindowState != WindowState.Minimized) TranslateUi(); };
        }
        _locTimer.Start();
    }

    private static readonly DependencyProperty LocOrigProperty =
        DependencyProperty.RegisterAttached("LocOrig", typeof(string), typeof(MainWindow));
    private static readonly DependencyProperty LocShownProperty =
        DependencyProperty.RegisterAttached("LocShown", typeof(string), typeof(MainWindow));

    private static readonly DependencyProperty LocTipProperty =
        DependencyProperty.RegisterAttached("LocTip", typeof(string), typeof(MainWindow));

    /// <summary>Traduz (ou devolve ao português) os textos visíveis da janela.</summary>
    private void TranslateUi() => Translate(this);

    private static void Translate(DependencyObject node)
    {
        if (node is UIElement { Visibility: not Visibility.Visible }) return; // o que está escondido é traduzido quando aparecer
        if (node is TextBox or PasswordBox) return;

        switch (node)
        {
            case TextBlock tb when tb.Inlines.Count <= 1 && !BindingOperations.IsDataBound(tb, TextBlock.TextProperty):
                { var t = Swap(tb, tb.Text); if (t != tb.Text) tb.Text = t; }
                break;
            case HeaderedContentControl hc when hc.Header is string h:
                { var t = Swap(hc, h); if (t != h) hc.Header = t; }
                break;
            case ContentControl cc when cc.Content is string c && !BindingOperations.IsDataBound(cc, ContentControl.ContentProperty):
                { var t = Swap(cc, c); if (t != c) cc.Content = t; }
                break;
        }
        if (node is FrameworkElement { ToolTip: string tip } fe)
        {
            var o = (string?)fe.GetValue(LocTipProperty);
            if (o == null || tip != Loc.T(o)) { o = tip; fe.SetValue(LocTipProperty, o); }
            var t = Loc.T(o);
            if (t != tip) fe.ToolTip = t;
        }

        int n = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(node) : 0;
        for (int i = 0; i < n; i++) Translate(VisualTreeHelper.GetChild(node, i));
    }

    /// <summary>Guarda o texto original na primeira vez; se o código trocou o texto depois, o novo vira o original.</summary>
    private static string Swap(DependencyObject d, string current)
    {
        var shown = (string?)d.GetValue(LocShownProperty);
        var orig = (string?)d.GetValue(LocOrigProperty);
        if (orig == null || current != shown) { orig = current; d.SetValue(LocOrigProperty, orig); }
        var t = Loc.T(orig);
        d.SetValue(LocShownProperty, t);
        return t;
    }
}
