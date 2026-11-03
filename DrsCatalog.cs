using System.Globalization;
using System.Xml.Linq;

namespace ResetExplode;

/// <summary>Um valor nomeado de uma configuração (ex.: "On - DLSS-SR overridden" = 0x00000001).</summary>
public sealed record DrsChoice(string Name, uint Value);

/// <summary>Configuração conhecida: nome amigável, grupo, descrição, valores e o padrão.</summary>
public sealed record DrsMeta(uint Id, string Name, string Group, string Description, IReadOnlyList<DrsChoice> Choices, uint? Default, bool Hidden);

/// <summary>
/// Catálogo das configurações do driver NVIDIA: os arquivos CustomSettingNames.xml e Reference.xml do
/// nvidiaProfileInspector (Orbmu2k, licença MIT, em licenses/), embutidos no app. CustomSettingNames tem os nomes
/// e grupos mais claros e vence quando os dois descrevem a mesma configuração.
/// </summary>
public static class DrsCatalog
{
    private static Dictionary<uint, DrsMeta>? _all;

    public static IReadOnlyDictionary<uint, DrsMeta> All => _all ??= Load();

    private static Dictionary<uint, DrsMeta> Load()
    {
        var map = new Dictionary<uint, DrsMeta>();
        foreach (var file in new[] { "Npi/Reference.xml", "Npi/CustomSettingNames.xml" }) // o segundo sobrescreve o primeiro
        {
            using var stream = typeof(DrsCatalog).Assembly.GetManifestResourceStream(file);
            if (stream == null) continue;
            foreach (var s in XDocument.Load(stream).Descendants("CustomSetting"))
            {
                if (Hex(s.Element("HexSettingID")?.Value) is not uint id) continue;
                var choices = s.Element("SettingValues")?.Elements("CustomSettingValue")
                    .Select(v => Hex(v.Element("HexValue")?.Value) is uint val ? new DrsChoice(v.Element("UserfriendlyName")?.Value.Trim() ?? $"0x{val:X8}", val) : null)
                    .OfType<DrsChoice>().ToList() ?? new List<DrsChoice>();
                string name = s.Element("UserfriendlyName")?.Value.Trim() ?? $"0x{id:X8}";
                string group = s.Element("GroupName")?.Value.Trim() ?? "Outras";
                string desc = (s.Element("Description")?.Value ?? "").Replace("\\r\\n", "\n").Trim();
                uint? def = Hex(s.Element("OverrideDefault")?.Value);
                bool hidden = string.Equals(s.Element("Hidden")?.Value, "true", StringComparison.OrdinalIgnoreCase);
                if (map.TryGetValue(id, out var older))
                {
                    // Mantém do Reference o que o CustomSettingNames não traz (descrição, valores, padrão).
                    if (choices.Count == 0) choices = older.Choices.ToList();
                    if (desc.Length == 0) desc = older.Description;
                    def ??= older.Default;
                }
                map[id] = new DrsMeta(id, name, group, desc, choices, def, hidden);
            }
        }
        return map;
    }

    /// <summary>"05 - Upscaling and Frame Generation" → "Upscaling and Frame Generation" (a ordem fica pelo número).</summary>
    public static string GroupTitle(string group)
    {
        int dash = group.IndexOf(" - ", StringComparison.Ordinal);
        return dash > 0 && dash <= 4 && group[..dash].All(char.IsDigit) ? group[(dash + 3)..] : group;
    }

    public static uint? Hex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v) ? v : null;
    }

    /// <summary>Nome do valor para mostrar ("On", "Off"…), ou o número em hexadecimal se não houver nome.</summary>
    public static string ValueName(DrsMeta? meta, ulong value)
        => meta?.Choices.FirstOrDefault(c => c.Value == value)?.Name ?? $"0x{value:X8}";

    /// <summary>Interpreta o que o usuário escolheu ou digitou: nome de um valor, "0x…" ou número decimal.</summary>
    public static ulong? Parse(DrsMeta? meta, string text)
    {
        text = text.Trim();
        if (meta?.Choices.FirstOrDefault(c => string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase)) is { } hit) return hit.Value;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong h) ? h : null;
        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong d) ? d : null;
    }
}
