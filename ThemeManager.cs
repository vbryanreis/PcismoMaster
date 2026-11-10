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

    // ---- Tema Legacy: o visual da 0.12.0 (roxo → azul, hover azul, cantos arredondados, ícones de glifo) ----

    /// <summary>Paleta da 0.12.0. O destaque em degradê (AccentGradient, OnAccent, AccentSoft) vem do ThemeLegacy.xaml.</summary>
    private static readonly (object Key, string Dark, string Light)[] LegacyPalette =
    {
        ("Bg", "#07060D", "#FFFFFF"),
        ("Panel", "#0B0A14", "#F6F4FD"),
        ("Card", "#110F1D", "#FFFFFF"),
        ("Hover", "#1B1830", "#ECE8FA"),
        ("Line", "#24203A", "#DAD4F0"),
        ("LineStrong", "#3F3866", "#9088B8"),
        ("Fg", "#F4F2FF", "#14111F"),
        ("Muted", "#9B95B8", "#4A4560"),
        ("Faint", "#7A7499", "#6A6485"),
        ("Ink", "#07060D", "#FFFFFF"),
        ("ThumbBrush", "#2C2746", "#D6D0EE"),
        ("ThumbHover", "#5A5290", "#ABA2D6"),
        ("ThumbDrag", "#8A80C8", "#8A80C8"),
        ("Accent", "#A66BFF", "#8B3FF0"),
        ("NavHover", "#3EC6EA", "#1E9BC4"), // hover das categorias: ciano no escuro, um azul mais fechado no modo claro
        // Chaves criadas depois da 0.12.0, em tons do tema antigo
        ("TagBlue", "#29B4D9", "#1E9BC4"),
        ("TagAmber", "#FFCF4D", "#A86F00"),
        ("TagGreen", "#4ED17A", "#1E8A4C"),
        ("IconA", "#A66BFF", "#8B3FF0"),
        ("IconB", "#29B4D9", "#1E9BC4"),
        ("IconC", "#F4F2FF", "#14111F"),
        (SystemColors.ControlBrushKey, "#07060D", "#FFFFFF"),
        (SystemColors.HighlightBrushKey, "#1B1830", "#ECE8FA"),
        (SystemColors.InactiveSelectionHighlightBrushKey, "#1B1830", "#ECE8FA"),
    };

    /// <summary>Tema Legacy ligado (vale desde a abertura: os estilos são lidos uma vez, então trocar pede reiniciar o app).</summary>
    public static bool Legacy { get; private set; }

    /// <summary>Liga o tema Legacy: põe o ThemeLegacy.xaml por cima do Theme.xaml. Chamar antes de criar a janela principal.</summary>
    public static void UseLegacy(bool on)
    {
        if (Legacy || !on) { Legacy |= on; return; }
        Legacy = true;
        try
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            dicts.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ThemeLegacy.xaml") });
        }
        catch (Exception ex) { Legacy = false; DriverClean.Log($"Tema Legacy não carregou: {ex.Message}"); }
    }

    private static System.Windows.Media.Effects.DropShadowEffect Glow(string hex, double blur, double opacity)
    {
        var e = new System.Windows.Media.Effects.DropShadowEffect
            { Color = (Color)ColorConverter.ConvertFromString(hex), ShadowDepth = 0, BlurRadius = blur, Opacity = opacity };
        e.Freeze();
        return e;
    }

    /// <summary>Canto de um elemento montado no código: reto no tema atual, o arredondado da 0.12.0 no Legacy.</summary>
    public static CornerRadius R(double r) => Legacy ? new CornerRadius(r) : new CornerRadius(0);
    public static CornerRadius R(double tl, double tr, double br, double bl) => Legacy ? new CornerRadius(tl, tr, br, bl) : new CornerRadius(0);

    public static void Apply(bool light)
    {
        IsLight = light;
        var res = Application.Current.Resources;
        if (Legacy)
        {
            // Tira as chaves que o tema atual põe aqui em cima, para valerem as do ThemeLegacy.xaml (o degradê roxo → ciano)
            foreach (var (key, _, _) in Palette) res.Remove(key);
            foreach (var (key, dark, lightHex) in LegacyPalette)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? lightHex : dark));
                b.Freeze();
                res[key] = b;
            }
            // Brilho do hover no menu lateral: no escuro, o da 0.12.0; no claro, curto e fraco para o texto ficar nítido
            res["NavGlowItem"] = Glow(light ? "#8B3FF0" : "#A647FE", light ? 3 : 8, light ? 0.25 : 0.8);
            res["NavGlowGroup"] = Glow(light ? "#1E9BC4" : "#5FB0FF", light ? 3 : 10, light ? 0.25 : 0.85);
            return; // no Legacy as cores escolhidas em Cores do programa não entram
        }
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
    /// <summary>Texto principal, texto secundário, fundo do programa e cartões/painéis.</summary>
    public static string? CustomFg { get; set; }
    public static string? CustomMuted { get; set; }
    public static string? CustomBg { get; set; }
    public static string? CustomCard { get; set; }
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

    /// <summary>Clareia (amount &gt; 0) ou escurece a cor; em cor muito clara, "clarear" escurece, para continuar dando contraste.</summary>
    public static Color Shade(Color c, double amount)
    {
        bool light = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6;
        if (light) amount = -Math.Abs(amount);
        byte F(byte v) => (byte)Math.Clamp(Math.Round(amount >= 0 ? v + (255 - v) * amount : v * (1 + amount)), 0, 255);
        return Color.FromRgb(F(c.R), F(c.G), F(c.B));
    }

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
        if (ParseColor(CustomFg) is Color fg) Set("Fg", fg);
        if (ParseColor(CustomMuted) is Color mu) { Set("Muted", mu); Set("Faint", Color.FromArgb(0xB4, mu.R, mu.G, mu.B)); }
        if (ParseColor(CustomBg) is Color bg)
        {
            Set("Bg", bg); Set("Ink", bg); Set(SystemColors.ControlBrushKey, bg);
            // o menu lateral um pouco mais escuro (ou mais claro, num fundo escuro) que o fundo
            Set("Panel", Shade(bg, -0.04));
        }
        if (ParseColor(CustomCard) is Color cd) { Set("Card", cd); Set("Hover", Shade(cd, 0.06)); }
    }
}
