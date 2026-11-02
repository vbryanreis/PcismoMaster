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
        // Paleta do ícone: tons neutros levemente arroxeados; o roxo/ciano de destaque fica no Theme.xaml (igual nos dois modos).
        ("Bg", "#07060D", "#FFFFFF"),
        ("Panel", "#0B0A14", "#F6F4FD"),
        ("Card", "#110F1D", "#FFFFFF"),
        ("Hover", "#1B1830", "#ECE8FA"),
        ("Line", "#24203A", "#DAD4F0"),
        ("LineStrong", "#3F3866", "#9088B8"),
        ("Fg", "#F4F2FF", "#14111F"),
        ("Muted", "#9B95B8", "#4A4560"),
        ("Faint", "#4C466B", "#6A6485"),
        ("Ink", "#07060D", "#FFFFFF"),
        ("ThumbBrush", "#2C2746", "#D6D0EE"),
        ("ThumbHover", "#5A5290", "#ABA2D6"),
        ("ThumbDrag", "#8A80C8", "#8A80C8"),
        ("Accent", "#A66BFF", "#8B3FF0"),
        ("NavHover", "#3EC6EA", "#1E9BC4"), // hover das categorias: ciano no escuro, um azul mais fechado (menos claro) no modo claro
        (SystemColors.ControlBrushKey, "#07060D", "#FFFFFF"),
        (SystemColors.HighlightBrushKey, "#1B1830", "#ECE8FA"),
        (SystemColors.InactiveSelectionHighlightBrushKey, "#1B1830", "#ECE8FA"),
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
