using System.Net;
using System.Text.RegularExpressions;

namespace ResetExplode;

/// <summary>Uma parte das notas de um driver NVIDIA (ex.: "Correções em jogos" e os itens).</summary>
public sealed record NvNoteSection(string Heading, string Text, List<string> Items);

/// <summary>Um driver NVIDIA publicado, com o que mudou nele (das notas oficiais que a própria NVIDIA devolve na consulta).</summary>
public sealed record NvRelease(string Version, DateTime? Date, string Title, List<NvNoteSection> Sections, string DetailsUrl, string ArticleUrl)
{
    public string DateText => Date is DateTime d ? d.ToString("dd/MM/yyyy") : "";
}

/// <summary>
/// Lê as notas de versão que a consulta de drivers da NVIDIA devolve em HTML (o mesmo texto da página do driver):
/// título "Game Ready for …", parágrafos e listas "Fixed Gaming Bugs", "Fixed General Bugs", "Open Issues" etc.
/// Os títulos vêm traduzidos; os itens ficam no inglês original (a NVIDIA só publica em inglês). Sem rede: fácil de testar.
/// </summary>
public static class NvidiaNotes
{
    public static List<NvNoteSection> Parse(string html, out string title, out string articleUrl)
    {
        title = ""; articleUrl = "";
        var sections = new List<NvNoteSection>();
        if (string.IsNullOrWhiteSpace(html)) return sections;
        html = SafeDecode(html);
        var link = Regex.Match(html, @"<a[^>]+href=""(https://www\.nvidia\.com/[^""]+)""", RegexOptions.IgnoreCase);
        if (link.Success) articleUrl = link.Groups[1].Value;
        html = Regex.Replace(html, @"<a[^>]*>.*?</a>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline); // "Learn more…"
        html = Regex.Replace(html, @"<img[^>]*>|<p[^>]*>|</p>", "", RegexOptions.IgnoreCase);

        // Cada <b>Título</b> abre uma parte até o próximo <b>.
        var heads = Regex.Matches(html, @"<b>(.*?)</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (heads.Count == 0)
        {
            string only = Clean(html);
            if (only.Length > 0) sections.Add(new NvNoteSection("Novidades", only, new List<string>()));
            return sections;
        }
        for (int i = 0; i < heads.Count; i++)
        {
            string raw = Clean(heads[i].Groups[1].Value);
            int start = heads[i].Index + heads[i].Length;
            int end = i + 1 < heads.Count ? heads[i + 1].Index : html.Length;
            string body = html[start..end];
            var items = Regex.Matches(body, @"<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                             .Select(m => Clean(m.Groups[1].Value)).Where(s => s.Length > 0).ToList();
            string text = Clean(Regex.Replace(body, @"<ul[^>]*>.*?</ul>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline));
            if (i == 0 && Regex.IsMatch(raw, @"^(Game Ready|Studio)", RegexOptions.IgnoreCase)) title = Translate(raw);
            if (text.Length == 0 && items.Count == 0) continue;
            sections.Add(new NvNoteSection(Translate(raw), text, items));
        }
        return sections;
    }

    /// <summary>Títulos que a NVIDIA usa, em português. O resto fica como veio.</summary>
    public static string Translate(string heading)
    {
        string h = heading.Trim().TrimEnd(':');
        var m = Regex.Match(h, @"^Game Ready (?:Driver )?for (.+)$", RegexOptions.IgnoreCase);
        if (m.Success) return "Game Ready para " + m.Groups[1].Value.Replace(" & ", " e ");
        m = Regex.Match(h, @"^(?:NVIDIA )?Studio (?:Driver )?for (.+)$", RegexOptions.IgnoreCase);
        if (m.Success) return "Studio para " + m.Groups[1].Value.Replace(" & ", " e ");
        return h.ToLowerInvariant() switch
        {
            "fixed gaming bugs" => "Correções em jogos",
            "fixed general bugs" => "Correções gerais",
            "fixed bugs" or "fixed issues" or "fixed issues in this release" => "Correções",
            "open issues" or "known issues" or "open issues in this release" => "Problemas conhecidos",
            "new features" or "what's new" or "new features and other changes" => "Novidades",
            "gaming technology" or "new game ready support" => "Tecnologias para jogos",
            "application profiles" or "new and updated application profiles" => "Perfis de jogos novos ou atualizados",
            "3d vision profiles" => "Perfis 3D Vision",
            "g-sync compatible displays" or "new g-sync compatible displays" => "Monitores G-SYNC Compatible novos",
            _ => h,
        };
    }

    public static DateTime? ParseDate(string s)
    {
        // "Wed Nov 04, 2026"
        string t = Regex.Replace(s ?? "", @"^\w{3}\s+", "").Trim();
        return DateTime.TryParseExact(t, new[] { "MMM dd, yyyy", "MMM d, yyyy" }, System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    private static string SafeDecode(string s)
    {
        if (!s.Contains('<') && s.Contains('%'))
            try { s = Uri.UnescapeDataString(s); } catch { }
        return s;
    }

    private static string Clean(string s)
    {
        s = Regex.Replace(s, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}
