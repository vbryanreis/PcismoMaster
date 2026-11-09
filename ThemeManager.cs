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
        ("AccentSoft", "#1A7C9CFF", "#1A2F5BE0"),
        ("IconA", "#3D6BFF", "#2F5BE0"),
        ("IconB", "#FF5A36", "#E8452A"),
        ("IconC", "#EFE6D2", "#1E2A4A"),
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
        ApplyCustom();
    }

    // ---- Cores do programa (escolhidas pelo usuário; null = a do tema) ----

    public static string? CustomAccent { get; set; }
    public static string? CustomIconA { get; set; }
    public static string? CustomIconB { get; set; }
    public static string? CustomIconC { get; set; }
    /// <summary>Botões principais, abas marcadas e o quadrado dos cartões na cor de destaque (em vez do branco/preto).</summary>
    public static bool AccentButtons { get; set; }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { var c = (Color)ColorConverter.ConvertFromString(hex.Trim().StartsWith("#") ? hex.Trim() : "#" + hex.Trim()); c.A = 255; return c; }
        catch { return null; }
    }

    /// <summary>Texto legível em cima de uma cor: preto em cor clara, branco em cor escura.</summary>
    public static Color OnColor(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6 ? Color.FromRgb(0x0E, 0x0E, 0x10) : Colors.White;

    private static void Set(object key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        Application.Current.Resources[key] = b;
    }

    /// <summary>Por cima da paleta: a cor de destaque e as três cores dos ícones que o usuário escolheu.</summary>
    private static void ApplyCustom()
    {
        if (ParseColor(CustomAccent) is Color a)
        {
            Set("Accent", a);
            Set("TagBlue", a);
            Set("AccentSoft", Color.FromArgb(0x1A, a.R, a.G, a.B));
            if (AccentButtons)
            {
                Set("AccentGradient", a);
                Set("AccentGradientV", a);
                Set("OnAccent", OnColor(a));
            }
        }
        if (ParseColor(CustomIconA) is Color ia) Set("IconA", ia);
        if (ParseColor(CustomIconB) is Color ib) Set("IconB", ib);
        if (ParseColor(CustomIconC) is Color ic) Set("IconC", ic);
    }
}
