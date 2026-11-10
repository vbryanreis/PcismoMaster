using System.Globalization;
using System.IO;
using System.Reflection;

namespace ResetExplode.Modular;

/// <summary>
/// Idioma do programa: português (o original), inglês ou espanhol. As traduções ficam em Modular/Lang.txt
/// ("pt|en|es" por linha). A busca ignora maiúsculas e espaços nas pontas; texto em CAIXA ALTA continua em CAIXA ALTA.
/// O que não tem tradução fica em português.
/// </summary>
public static class Loc
{
    public static readonly (string Code, string Name)[] Languages = { ("pt", "Português"), ("en", "English"), ("es", "Español") };

    /// <summary>"pt", "en" ou "es".</summary>
    public static string Lang { get; set; } = "pt";

    private static Dictionary<string, (string En, string Es)>? _map;

    public static int Count => Map.Count;

    private static Dictionary<string, (string En, string Es)> Map => _map ??= Parse(ReadEmbedded());

    private static string ReadEmbedded()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lang.txt");
            if (s == null) return "";
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }
        catch { return ""; }
    }

    public static Dictionary<string, (string En, string Es)> Parse(string text)
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var p = line.Split('|');
            if (p.Length < 3) continue;
            map[Key(p[0])] = (p[1], p[2]);
        }
        return map;
    }

    private static string Key(string s) => s.Trim().ToLower(new CultureInfo("pt-BR"));

    /// <summary>Usa outro conjunto de traduções (para testes).</summary>
    public static void Use(string text) => _map = Parse(text);

    /// <summary>O texto no idioma escolhido; sem tradução, o próprio texto.</summary>
    public static string T(string pt) => T(pt, Lang);

    public static string T(string pt, string lang)
    {
        if (lang == "pt" || string.IsNullOrWhiteSpace(pt)) return pt;
        if (!Map.TryGetValue(Key(pt), out var tr))
        {
            // "TWEAKING · Geral", "Ícones · cor 1": traduz cada parte
            if (pt.Contains(" · ")) return string.Join(" · ", pt.Split(" · ").Select(p => T(p, lang)));
            return pt;
        }
        string t = lang == "es" ? tr.Es : tr.En;
        if (t.Length == 0) return pt;
        // Mantém CAIXA ALTA e os espaços das pontas do original
        bool caps = pt.Count(char.IsLetter) >= 2 && pt == pt.ToUpper(new CultureInfo("pt-BR"));
        if (caps) t = t.ToUpper(lang == "es" ? new CultureInfo("es-ES") : CultureInfo.InvariantCulture);
        int lead = pt.Length - pt.TrimStart().Length, trail = pt.Length - pt.TrimEnd().Length;
        return pt[..lead] + t + pt[(pt.Length - trail)..];
    }
}
