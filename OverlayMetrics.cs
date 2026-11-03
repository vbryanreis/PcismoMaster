namespace ResetExplode;

/// <summary>Item que pode aparecer no overlay. Group: "FPS", "GPU", "VRAM", "CPU", "RAM" ou "Outros".</summary>
public sealed record OverlayMetric(string Key, string Group, string Label, bool Default);

/// <summary>Linha do overlay já pronta para mostrar (ex.: "GPU" · "98%  65 °C  2790 MHz").</summary>
public sealed record OverlayLine(string Label, string Value);

/// <summary>Catálogo do que o overlay pode mostrar, na ordem em que aparece.</summary>
public static class OverlayMetrics
{
    public static readonly OverlayMetric[] All =
    {
        new("fps", "FPS", "FPS", true),
        new("frametime", "FPS", "Frametime (ms)", true),
        new("low1", "FPS", "1% low", true),
        new("graph", "FPS", "Gráfico de frametime", true),

        new("gpu_load", "GPU", "Uso", true),
        new("gpu_temp", "GPU", "Temperatura", true),
        new("gpu_hotspot", "GPU", "Hotspot", false),
        new("gpu_clock", "GPU", "Clock do núcleo", true),
        new("gpu_memclock", "GPU", "Clock da memória", false),
        new("gpu_power", "GPU", "Consumo", true),
        new("gpu_volt", "GPU", "Tensão do núcleo", false),
        new("gpu_fan", "GPU", "Ventoinha", false),
        new("gpu_perfcap", "GPU", "Limitador (NVIDIA)", false),

        new("vram_used", "VRAM", "Usada (GB)", true),
        new("vram_pct", "VRAM", "Uso (%)", false),
        new("vram_temp", "VRAM", "Temperatura", false),

        new("cpu_load", "CPU", "Uso total", true),
        new("cpu_maxcore", "CPU", "Núcleo mais ocupado", false),
        new("cpu_temp", "CPU", "Temperatura", true),
        new("cpu_clock", "CPU", "Clock médio", true),
        new("cpu_clockmax", "CPU", "Clock máximo (núcleo)", false),
        new("cpu_power", "CPU", "Consumo", true),

        new("ram_used", "RAM", "Usada (GB)", true),
        new("ram_pct", "RAM", "Uso (%)", false),

        new("game", "Outros", "Nome do jogo", true),
        new("clock", "Outros", "Hora", false),
    };

    public static HashSet<string> Defaults() => All.Where(m => m.Default).Select(m => m.Key).ToHashSet();

    /// <summary>Junta as partes em linhas por componente (formato vertical), na ordem em que aparecem.</summary>
    public static List<OverlayLine> Lines(IEnumerable<OverlayPart> parts) =>
        parts.GroupBy(p => p.Group).Select(g => new OverlayLine(g.Key, string.Join("  ", g.Select(p => p.Text)))).ToList();
}

/// <summary>
/// Um valor do overlay: <see cref="Text"/> entra na linha do componente (formato vertical) e <see cref="Short"/> + <see cref="Bare"/>
/// viram uma célula do formato horizontal (barra). <see cref="Temp"/> = temperatura em °C, usada para colorir.
/// </summary>
public sealed record OverlayPart(string Group, string Short, string Text, string Bare, float? Temp = null)
{
    /// <summary>0 = normal, 1 = fria (verde), 2 = morna (âmbar), 3 = quente (vermelha).</summary>
    public int Level => Temp is not float t ? 0 : t >= 85 ? 3 : t >= 75 ? 2 : 1;
}

/// <summary>Tecla de atalho do overlay (modificadores de RegisterHotKey + código virtual).</summary>
public static class OverlayHotkey
{
    public const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8;

    /// <summary>Texto do atalho, ex.: "Ctrl + Shift + O" ou "F5".</summary>
    public static string Describe(uint mods, string key)
    {
        var parts = new List<string>();
        if ((mods & Ctrl) != 0) parts.Add("Ctrl");
        if ((mods & Alt) != 0) parts.Add("Alt");
        if ((mods & Shift) != 0) parts.Add("Shift");
        if ((mods & Win) != 0) parts.Add("Win");
        parts.Add(key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]) ? key[1..] : key); // D5 → 5
        return string.Join(" + ", parts);
    }
}
