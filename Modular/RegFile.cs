using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ResetExplode.Modular;

/// <summary>O que sai de um .reg: valores (com backup pelo ModuleEngine), chaves inteiras a apagar e quantas linhas ficaram de fora.</summary>
public sealed record RegContent(Setting[] Settings, string[] DeleteKeys, int Skipped);

/// <summary>
/// Lê o texto de um .reg do Regedit (o que os scripts do Ultimate importam) e devolve os valores como Setting, no mesmo
/// formato que o RegistryStore lê (para o "já aplicado" e o desfazer baterem). HKEY_CLASSES_ROOT vira HKLM\SOFTWARE\Classes;
/// HKEY_USERS e tipos de hives de app (settings.dat) ficam de fora.
/// </summary>
public static class RegFile
{
    public static RegContent Parse(string text)
    {
        var settings = new List<Setting>();
        var deletes = new List<string>();
        int skipped = 0;
        (string Hive, string Path)? key = null;
        foreach (var line in Logical(text))
        {
            if (line.Length == 0 || line[0] == ';' || line.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase)) continue;
            if (line[0] == '[')
            {
                bool delete = line.StartsWith("[-");
                var k = MapKey(line.TrimStart('[', '-').TrimEnd(']'));
                if (k == null) { key = null; skipped++; continue; }
                if (delete) { deletes.Add(k.Value.Hive + "\\" + k.Value.Path); key = null; }
                else key = k;
                continue;
            }
            if (key == null) { skipped++; continue; }
            int eq = NameEnd(line);
            if (eq < 0) { skipped++; continue; }
            string name = line[0] == '@' ? "" : Unescape(line[1..(eq - 1)]);
            string data = line[(eq + 1)..].Trim();
            var s = Value(key.Value.Hive, key.Value.Path, name, data);
            if (s == null) skipped++; else settings.Add(s);
        }
        // O último valor vence (o .reg pode repetir a mesma chave/valor).
        var last = settings.GroupBy(s => (s.Hive, s.Path.ToLowerInvariant(), s.Name.ToLowerInvariant())).Select(g => g.Last()).ToArray();
        return new(last, deletes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }

    /// <summary>Junta as linhas continuadas com "\" no fim (valores hex longos).</summary>
    private static IEnumerable<string> Logical(string text)
    {
        var sb = new StringBuilder();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            string l = raw.Trim();
            if (l.EndsWith('\\') && (sb.Length > 0 || l.Contains("=hex"))) { sb.Append(l[..^1]); continue; }
            sb.Append(l);
            yield return sb.ToString();
            sb.Clear();
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static (string Hive, string Path)? MapKey(string full)
    {
        int slash = full.IndexOf('\\');
        string root = slash < 0 ? full : full[..slash], rest = slash < 0 ? "" : full[(slash + 1)..];
        (string Hive, string Path)? key = root.ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => ("HKCU", rest),
            "HKEY_LOCAL_MACHINE" or "HKLM" => ("HKLM", rest),
            "HKEY_CLASSES_ROOT" or "HKCR" => ("HKLM", rest.Length == 0 ? @"SOFTWARE\Classes" : @"SOFTWARE\Classes\" + rest),
            _ => null,
        };
        return key is { Path.Length: > 0 } ? key : null;
    }

    /// <summary>Posição do "=" que separa o nome do valor ("nome com \"aspas\""=… ou @=…).</summary>
    private static int NameEnd(string line)
    {
        if (line.StartsWith("@=")) return 1;
        if (line[0] != '"') return -1;
        for (int i = 1; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == '"') return i + 1 < line.Length && line[i + 1] == '=' ? i + 1 : -1;
        }
        return -1;
    }

    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static Setting? Value(string hive, string path, string name, string data)
    {
        if (data == "-") return new(hive, path, name, 0, Remove: true);
        if (data.StartsWith('"') && data.EndsWith('"') && data.Length >= 2)
            return Make(hive, path, name, 1, JsonSerializer.Serialize(Unescape(data[1..^1])));
        if (data.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(data[6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint d)
                ? Make(hive, path, name, 4, JsonSerializer.Serialize(unchecked((int)d))) : null;
        if (!data.StartsWith("hex", StringComparison.OrdinalIgnoreCase)) return null;
        int colon = data.IndexOf(':');
        if (colon < 0) return null;
        string type = data[3..colon].Trim('(', ')');
        byte[] bytes;
        try { bytes = data[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(b => Convert.ToByte(b, 16)).ToArray(); }
        catch { return null; }
        return type.ToLowerInvariant() switch
        {
            "" or "3" => Make(hive, path, name, 3, JsonSerializer.Serialize(bytes)),
            "0" => Make(hive, path, name, -1, JsonSerializer.Serialize(bytes)),
            "2" => Make(hive, path, name, 2, JsonSerializer.Serialize(Utf16(bytes).TrimEnd('\0'))),
            "7" => Make(hive, path, name, 7, JsonSerializer.Serialize(Utf16(bytes).TrimEnd('\0').Split('\0'))),
            "4" when bytes.Length == 4 => Make(hive, path, name, 4, JsonSerializer.Serialize(BitConverter.ToInt32(bytes))),
            "b" when bytes.Length == 8 => Make(hive, path, name, 11, JsonSerializer.Serialize(BitConverter.ToInt64(bytes))),
            _ => null,
        };
    }

    private static string Utf16(byte[] b) => Encoding.Unicode.GetString(b);
    private static Setting Make(string hive, string path, string name, int kind, string data) => new(hive, path, name, 0, Kind: kind, Data: data);
}
