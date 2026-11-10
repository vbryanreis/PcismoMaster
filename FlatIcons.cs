using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>
/// Ícones chapados em até três cores (IconA azul, IconB laranja, IconC creme), desenhados numa grade de 24×24.
/// Cada camada pega a cor do tema por DynamicResource, então trocar as cores em "Cores do programa" vale na hora.
/// Ícone sem desenho próprio cai no glifo da fonte, pintado com IconA.
/// </summary>
public static class FlatIcons
{
    private enum Ink { A, B, C }
    private sealed record Layer(Ink Ink, Geometry Shape);

    private static Geometry P(string data) => Geometry.Parse(data);
    private static Geometry Circle(double cx, double cy, double r) => new EllipseGeometry(new Point(cx, cy), r, r);
    private static Geometry Ring(double cx, double cy, double r, double inner) =>
        new CombinedGeometry(GeometryCombineMode.Exclude, Circle(cx, cy, r), Circle(cx, cy, inner));

    /// <summary>Engrenagem: dentes retos em volta de um círculo.</summary>
    private static Geometry Gear(double cx, double cy, double r, double tooth, int teeth)
    {
        Geometry g = Circle(cx, cy, r);
        for (int i = 0; i < teeth; i++)
        {
            var t = new RectangleGeometry(new Rect(cx - tooth / 2, cy - r - tooth * 0.9, tooth, tooth * 1.4))
                { Transform = new RotateTransform(360.0 * i / teeth, cx, cy) };
            g = new CombinedGeometry(GeometryCombineMode.Union, g, t);
        }
        return g;
    }

    private static Layer A(Geometry g) => new(Ink.A, g);
    private static Layer B(Geometry g) => new(Ink.B, g);
    private static Layer C(Geometry g) => new(Ink.C, g);

    private static readonly Dictionary<string, Layer[]> Shapes = new()
    {
        ["home"] = new[] { C(P("M5,11 H19 V21 H5 Z")), B(P("M1.5,11.5 L12,3 L22.5,11.5 Z")), A(P("M10,14 H14 V21 H10 Z")) },
        ["eye"] = new[] { C(P("M1,12 C5,4.5 19,4.5 23,12 C19,19.5 5,19.5 1,12 Z")), A(Circle(12, 12, 4.6)), B(Circle(12, 12, 1.9)) },
        ["monitor"] = new[] { A(P("M2,3.5 H22 V16.5 H2 Z")), C(P("M4,5.5 H20 V14.5 H4 Z")), B(P("M10,17 H14 V19.5 H17.5 V21.5 H6.5 V19.5 H10 Z")) },
        ["thermo"] = new[] { C(P("M9,4.5 A3,3 0 0 1 15,4.5 V14 H9 Z")), B(Circle(12, 17.5, 5)), B(P("M11,7 H13 V14 H11 Z")) },
        ["gpu"] = new[] { A(P("M1.5,6 H22.5 V17 H1.5 Z")), C(Circle(8.5, 11.5, 4)), A(Circle(8.5, 11.5, 1.5)), B(P("M15,8.5 H20.5 V14.5 H15 Z")), C(P("M3.5,17 H8 V20.5 H3.5 Z")) },
        ["disk"] = new[] { A(P("M3,3 H21 V21 H3 Z")), C(Circle(12, 11, 6)), B(Circle(12, 11, 2)), B(P("M15,17.5 H19 V19 H15 Z")) },
        ["battery"] = new[] { C(P("M1.5,6.5 H19 V17.5 H1.5 Z")), A(P("M19,9.5 H22.5 V14.5 H19 Z")), B(P("M3.5,8.5 H12 V15.5 H3.5 Z")) },
        ["signal"] = new[] { A(P("M2.5,15 H7 V21 H2.5 Z")), B(P("M9.75,9.5 H14.25 V21 H9.75 Z")), C(P("M17,3 H21.5 V21 H17 Z")) },
        ["wrench"] = new[] { C(P("M15.5,2.5 A6,6 0 0 0 9.6,10.4 L2.5,17.5 L6.5,21.5 L13.6,14.4 A6,6 0 0 0 21.5,8.5 L17.8,10.5 L13.5,6.2 Z")), B(Circle(5.2, 18.8, 1.4)) },
        ["checklist"] = new[] { C(P("M4.5,1.5 H19.5 V22.5 H4.5 Z")), B(P("M7,4.5 H10 V7.5 H7 Z M7,10.5 H10 V13.5 H7 Z M7,16.5 H10 V19.5 H7 Z")),
                                A(P("M12,5.25 H17 V6.75 H12 Z M12,11.25 H17 V12.75 H12 Z M12,17.25 H17 V18.75 H12 Z")) },
        ["trash"] = new[] { A(P("M5.5,7 H18.5 L17.2,22.5 H6.8 Z")), A(P("M3.5,3.8 H20.5 V6 H3.5 Z")), C(P("M9,1.5 H15 V3.8 H9 Z")),
                            C(P("M9,9.5 H10.6 V20 H9 Z M13.4,9.5 H15 V20 H13.4 Z")) },
        ["gear"] = new[] { B(Gear(12, 12, 8, 3.4, 8)), C(Circle(12, 12, 3.6)) },
        ["gears"] = new[] { A(Gear(9, 10, 6, 2.6, 8)), C(Circle(9, 10, 2.4)), B(Gear(17.5, 17.5, 4, 2, 6)), C(Circle(17.5, 17.5, 1.5)) },
        ["lock"] = new[] { C(P("M6.5,10.5 V7.5 A5.5,5.5 0 0 1 17.5,7.5 V10.5 H15 V7.5 A3,3 0 0 0 9,7.5 V10.5 Z")), B(P("M3.5,10 H20.5 V22.5 H3.5 Z")),
                           C(Circle(12, 15, 2)), C(P("M11.2,15 H12.8 V19 H11.2 Z")) },
        ["bolt"] = new[] { B(P("M14,1 L4,14 H11 L9.5,23 L20,9.5 H13 Z")), C(P("M14,1 L13,9.5 H20 L16,14.5 L9,14 Z")) },
        ["sliders"] = new[] { C(P("M2.5,6 H21.5 V8 H2.5 Z M2.5,16 H21.5 V18 H2.5 Z")), B(Circle(8, 7, 3.2)), A(Circle(16, 17, 3.2)) },
        ["update"] = new[] { C(Circle(12, 12, 3.2)), A(P("M2.5,12 A9.5,9.5 0 0 1 19.5,6.3 L22,4 V11 H15 L17.4,8.6 A6.5,6.5 0 0 0 5.5,12 Z")),
                             B(P("M21.5,12 A9.5,9.5 0 0 1 4.5,17.7 L2,20 V13 H9 L6.6,15.4 A6.5,6.5 0 0 0 18.5,12 Z")) },
        ["download"] = new[] { A(P("M2.5,16.5 H21.5 V22 H2.5 Z")), B(P("M9.5,1.5 H14.5 V9.5 H18.5 L12,16.5 L5.5,9.5 H9.5 Z")), C(P("M16,18.5 H19 V20 H16 Z")) },
        ["shield"] = new[] { A(P("M12,1 L21,4.8 V11 C21,17 17.2,21 12,23 C6.8,21 3,17 3,11 V4.8 Z")), C(P("M12,1 L21,4.8 V11 C21,17 17.2,21 12,23 Z")),
                             B(P("M7.5,11.5 L9.4,9.6 L11,11.2 L15.2,7 L17.1,8.9 L11,15 Z")) },
        ["chip"] = new[] { B(P("M8,2 H9.6 V6 H8 Z M11.2,2 H12.8 V6 H11.2 Z M14.4,2 H16 V6 H14.4 Z M8,18 H9.6 V22 H8 Z M11.2,18 H12.8 V22 H11.2 Z M14.4,18 H16 V22 H14.4 Z " +
                                  "M2,8 H6 V9.6 H2 Z M2,11.2 H6 V12.8 H2 Z M2,14.4 H6 V16 H2 Z M18,8 H22 V9.6 H18 Z M18,11.2 H22 V12.8 H18 Z M18,14.4 H22 V16 H18 Z")),
                           A(P("M5.5,5.5 H18.5 V18.5 H5.5 Z")), C(P("M9,9 H15 V15 H9 Z")) },
        ["sparkle"] = new[] { B(P("M11,2 L13.2,9.8 L21,12 L13.2,14.2 L11,22 L8.8,14.2 L1,12 L8.8,9.8 Z")), C(P("M19,1.5 L20,4 L22.5,5 L20,6 L19,8.5 L18,6 L15.5,5 L18,4 Z")),
                              A(P("M19,15.5 L19.8,17.2 L21.5,18 L19.8,18.8 L19,20.5 L18.2,18.8 L16.5,18 L18.2,17.2 Z")) },
        ["palette"] = new[] { C(P("M12,2 C6.2,2 2,6.4 2,12 C2,17.6 6.4,22 12,22 C13.6,22 14.2,20.8 13.6,19.4 C13,18 13.8,16.8 15.4,16.8 H18 C20.4,16.8 22,15 22,12 C22,6.4 17.6,2 12,2 Z")),
                              B(Circle(7, 11.5, 1.9)), A(Circle(10, 6.8, 1.9)), B(Circle(15, 6.8, 1.9)), A(Circle(18, 11.5, 1.9)) },
        ["layers"] = new[] { C(P("M2,16 L4.5,14.7 L12,18.5 L19.5,14.7 L22,16 L12,21 Z")), B(P("M2,12 L4.5,10.7 L12,14.5 L19.5,10.7 L22,12 L12,17 Z")), A(P("M2,8 L12,3 L22,8 L12,13 Z")) },
        ["tag"] = new[] { B(P("M2,3 H11.5 L22,13.5 L13.5,22 L2,11.5 Z")), C(Circle(7, 8, 2.1)) },
        ["target"] = new[] { B(Circle(12, 12, 10)), C(Circle(12, 12, 7)), B(Circle(12, 12, 4)), A(P("M11,12 L19.5,3.5 L21,2 L21.5,4.5 L23,5 L21.5,6.5 L13,13 Z")) },
        ["gauge"] = new[] { C(P("M2,17 A10,10 0 0 1 22,17 Z")), A(P("M1.5,17.5 H22.5 V20 H1.5 Z")), B(P("M10.6,16.2 L17.5,7.5 L13.4,17.8 Z")) },
        ["overlay"] = new[] { A(P("M1.5,3.5 H17.5 V15.5 H1.5 Z")), C(P("M6.5,8.5 H22.5 V20.5 H6.5 Z")), B(P("M6.5,8.5 H22.5 V11.5 H6.5 Z")) },
        ["stopwatch"] = new[] { C(Circle(12, 14, 8.5)), A(P("M9.5,1.5 H14.5 V4 H9.5 Z")), A(P("M11,4 H13 V6 H11 Z")), B(P("M11.2,8.5 H12.8 V14.8 H11.2 Z")), B(Circle(12, 14.5, 1.6)) },
        ["flame"] = new[] { B(P("M12,1.5 C14,6.8 19.5,9 19.5,15 A7.5,7.5 0 0 1 4.5,15 C4.5,11 7.5,9.2 8.6,6.2 C9.8,8.8 11.5,9.8 11.5,9.8 C11.8,7 11,4.6 12,1.5 Z")),
                            C(P("M12,12.5 C13.6,14.6 15.2,15.8 15.2,18 A3.2,3.2 0 0 1 8.8,18 C8.8,15.8 10.6,14.6 12,12.5 Z")) },
        ["power"] = new[] { C(Ring(12, 13, 9.5, 6.5)), B(P("M10.5,1.5 H13.5 V12 H10.5 Z")) },
        ["box"] = new[] { A(P("M12,12 L3,7 V17 L12,22 Z")), B(P("M12,12 L21,7 V17 L12,22 Z")), C(P("M3,7 L12,2 L21,7 L12,12 Z")) },
        ["plug"] = new[] { C(P("M8.5,2 H10.5 V8 H8.5 Z M13.5,2 H15.5 V8 H13.5 Z")), A(P("M6,7.5 H18 V12.5 A6,6 0 0 1 6,12.5 Z")), B(P("M10.8,18 H13.2 V23 H10.8 Z")) },
        ["chart"] = new[] { C(P("M2,20.5 H22 V22.5 H2 Z")), A(P("M3.5,12 H8 V19 H3.5 Z")), B(P("M9.75,6 H14.25 V19 H9.75 Z")), C(P("M16,2 H20.5 V19 H16 Z")) },
        ["globe"] = new[] { A(Circle(12, 12, 10)), C(P("M12,2 C8.5,5 8.5,19 12,22 C15.5,19 15.5,5 12,2 Z")), B(P("M2.6,8.5 H21.4 V10 H2.6 Z M2.6,14 H21.4 V15.5 H2.6 Z")) },
    };

    /// <summary>Ícone de cada página do menu lateral.</summary>
    private static readonly Dictionary<string, string> ByNav = new()
    {
        ["NavHome"] = "home", ["NavDash"] = "eye", ["NavSystem"] = "monitor", ["NavSensors"] = "thermo", ["NavGpu"] = "gpu", ["NavDisks"] = "disk",
        ["NavEnergy"] = "battery", ["NavNet"] = "signal", ["NavMaint"] = "wrench",
        ["NavOptStart"] = "checklist", ["NavOptUpdate"] = "update", ["NavOptDebloat"] = "trash", ["NavOptGeral"] = "gear", ["NavOptPriv"] = "lock", ["NavOptPower"] = "bolt",
        ["NavOptNet"] = "globe", ["NavOptAdv"] = "sliders", ["NavOptGpu"] = "gpu", ["NavDriver"] = "download", ["NavOptMitig"] = "shield",
        ["NavOptSvc"] = "gears", ["NavOptBios"] = "chip", ["NavOptClean"] = "sparkle", ["NavOptLook"] = "palette", ["NavOptProfiles"] = "layers",
        ["NavLauncher"] = "layers", ["NavDeals"] = "tag", ["NavGames"] = "target", ["NavBottleneck"] = "gauge", ["NavOverlay"] = "overlay",
        ["NavBench"] = "stopwatch", ["NavStress"] = "bolt", ["NavOc"] = "flame", ["NavStartup"] = "power", ["NavApps"] = "box",
        ["NavUninstall"] = "trash", ["NavInteg"] = "palette", ["NavTasks"] = "stopwatch", ["NavUtil"] = "wrench", ["NavMakerPorts"] = "plug", ["NavMakerDrv"] = "chip",
    };

    /// <summary>Glifos da fonte de ícones que têm desenho próprio (cabeçalhos de cartão e blocos do Início).</summary>
    private static readonly Dictionary<string, string> ByGlyph = new()
    {
        ["\uE895"] = "update",
        [""] = "home", [""] = "monitor", [""] = "thermo", [""] = "gpu", [""] = "gpu", [""] = "disk",
        [""] = "battery", [""] = "signal", [""] = "globe", [""] = "wrench", [""] = "checklist", [""] = "trash",
        [""] = "gear", [""] = "lock", [""] = "bolt", [""] = "sliders", [""] = "download", [""] = "shield",
        [""] = "gears", [""] = "chip", [""] = "sparkle", [""] = "palette", [""] = "layers", [""] = "tag",
        [""] = "target", [""] = "chart", [""] = "overlay", [""] = "stopwatch", [""] = "flame", [""] = "power",
        [""] = "box", [""] = "plug", [""] = "shield", [""] = "monitor", [""] = "sparkle", [""] = "checklist",
        [""] = "sliders", [""] = "target", [""] = "chart",
    };

    public static string? KeyForNav(string navName) => ByNav.GetValueOrDefault(navName);
    public static string? KeyForGlyph(string glyph) => ByGlyph.GetValueOrDefault(glyph);

    /// <summary>O ícone desenhado (ou null se a chave não existe), no tamanho pedido.</summary>
    public static FrameworkElement? Create(string? key, double size)
    {
        if (ThemeManager.Legacy) return null; // tema Legacy: ficam os glifos da fonte, como na 0.12.0
        if (key == null || !Shapes.TryGetValue(key, out var layers)) return null;
        var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var l in layers)
        {
            var path = new System.Windows.Shapes.Path { Data = l.Shape };
            path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, l.Ink switch { Ink.A => "IconA", Ink.B => "IconB", _ => "IconC" });
            canvas.Children.Add(path);
        }
        return new Viewbox { Width = size, Height = size, Child = canvas, SnapsToDevicePixels = true };
    }

    /// <summary>Ícone para um glifo da fonte: o desenho próprio, ou o glifo pintado com a cor 1 dos ícones.</summary>
    public static FrameworkElement ForGlyph(string glyph, double size)
    {
        if (Create(KeyForGlyph(glyph), size) is { } drawn) return drawn;
        var tb = new TextBlock { Text = glyph, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        tb.SetResourceReference(TextBlock.FontFamilyProperty, "Icons");
        tb.SetResourceReference(TextBlock.ForegroundProperty, "IconA");
        return tb;
    }

    /// <summary>Troca o glifo do primeiro TextBlock de cada item do menu pelo ícone desenhado (mesma largura de 28).</summary>
    public static void ApplyToNav(Panel root)
    {
        foreach (var rb in Descendants<RadioButton>(root))
        {
            if (rb.Content is not StackPanel sp || sp.Children.Count == 0 || sp.Children[0] is not TextBlock glyph) continue;
            var icon = Create(KeyForNav(rb.Name), 17) ?? Create(KeyForGlyph(glyph.Text), 17);
            if (icon == null) continue;
            icon.Margin = new Thickness(0, 0, 11, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.RemoveAt(0); // trocar direto pelo índice falha no WPF (o filho antigo ainda está ligado)
            sp.Children.Insert(0, icon);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }
}
