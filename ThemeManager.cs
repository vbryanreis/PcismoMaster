using System.Windows;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Troca entre modo escuro e claro substituindo os pincéis da paleta (Theme.xaml). O XAML os referencia com DynamicResource,
/// então a troca vale na hora, sem recarregar janelas. Overlay e aviso de dispositivo ficam sempre escuros (aparecem sobre jogos).
/// </summary>
public static class ThemeManager
{
    public static bool IsLight { get; private set; }

    private static readonly (object Key, string Dark, string Light)[] Palette =
    {
        // Paleta neutra (preto/grafite no escuro, papel/grafite no claro). O destaque é monocromático: branco no escuro, preto no claro.
        ("Bg", "#0E0E10", "#F4F4F2"),
        ("Panel", "#0A0A0B", "#ECECEA"),
        ("Card", "#131315", "#FFFFFF"),
        ("Hover", "#1C1C20", "#E4E4E0"),
        ("Line", "#26262B", "#D4D4CF"),
        ("LineStrong", "#3A3A41", "#A6A6A0"),
        ("Fg", "#ECECEE", "#111113"),
        ("Muted", "#9A9AA2", "#55555C"),
        ("Faint", "#66666E", "#7A7A80"),
        ("Ink", "#0E0E10", "#FFFFFF"),
        ("ThumbBrush", "#2A2A30", "#CFCFCA"),
        ("ThumbHover", "#4A4A52", "#A6A6A0"),
        ("ThumbDrag", "#7A7A84", "#7A7A80"),
        ("Accent", "#7C9CFF", "#2F5BE0"),
        ("AccentGradient", "#ECECEE", "#111113"),
        ("AccentGradientV", "#ECECEE", "#111113"),
        ("OnAccent", "#0E0E10", "#FFFFFF"),
        ("TagBlue", "#7C9CFF", "#2F5BE0"),
        ("TagAmber", "#E8B44A", "#A86F00"),
        ("TagGreen", "#5FCF8A", "#1E8A4C"),
        ("NavHover", "#FFFFFF", "#000000"),
        (SystemColors.ControlBrushKey, "#0E0E10", "#F4F4F2"),
        (SystemColors.HighlightBrushKey, "#1C1C20", "#E4E4E0"),
        (SystemColors.InactiveSelectionHighlightBrushKey, "#1C1C20", "#E4E4E0"),
    };

    public static void Apply(bool light)
    {
        IsLight = light;
        var res = Application.Current.Resources;
        foreach (var (key, dark, lightHex) in Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(light ? lightHex : dark);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            res[key] = brush; // o XAML usa DynamicResource: trocar o pincel atualiza todos os controles na hora
        }
    }
}
