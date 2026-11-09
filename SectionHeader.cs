using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Cabeçalho padrão dos cartões (o da aba Drivers): ícone num quadrado com o degradê do tema, título e uma linha
/// de explicação. Em XAML: &lt;local:SectionHeader Icon="&amp;#xE713;" Title="…" Subtitle="…"/&gt;; no código: <see cref="MainWindow.CardHeader"/>.
/// As cores vêm do tema (DynamicResource), então acompanham claro/escuro e a cor de destaque.
/// </summary>
public sealed class SectionHeader : DockPanel
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SectionHeader),
        new PropertyMetadata("", (d, e) => ((SectionHeader)d)._icon.Text = (string)e.NewValue));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionHeader),
        new PropertyMetadata("", (d, e) => ((SectionHeader)d)._title.Text = (string)e.NewValue));
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(SectionHeader),
        new PropertyMetadata("", (d, e) => ((SectionHeader)d).SetSubtitle((string)e.NewValue)));

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    private readonly TextBlock _icon = new() { Text = "", FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sub = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    public SectionHeader()
    {
        Margin = new Thickness(0, 0, 0, 12);
        _icon.SetResourceReference(TextBlock.FontFamilyProperty, "Icons");
        _icon.SetResourceReference(TextBlock.ForegroundProperty, "OnAccent");
        _sub.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(0), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, Child = _icon };
        tile.SetResourceReference(Border.BackgroundProperty, "AccentGradient");
        SetDock(tile, Dock.Left);
        Children.Add(tile);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_title);
        text.Children.Add(_sub);
        Children.Add(text);
    }

    private void SetSubtitle(string s)
    {
        _sub.Text = s;
        _sub.Visibility = string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
    }
}

public partial class MainWindow
{
    /// <summary>O mesmo cabeçalho de cartão, para os cartões montados no código.</summary>
    internal static SectionHeader CardHeader(string icon, string title, string? subtitle = null) =>
        new() { Icon = icon, Title = title, Subtitle = subtitle ?? "" };
}
