using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ResetExplode.Modular;

/// <summary>Uma versão publicada do Windows (linha do histórico oficial de lançamentos).</summary>
public sealed record WinRelease(int Build, int Revision, string Kb, string Type, string Date)
{
    public string BuildText => $"{Build}.{Revision}";
    /// <summary>B = mensal de segurança; D = prévia opcional; OOB = fora do ciclo (emergencial).</summary>
    public string TypeText => Type.EndsWith(" B") ? "Mensal de segurança" : Type.EndsWith(" D") ? "Prévia opcional" : Type.Contains("OOB") ? "Emergencial (fora do ciclo)" : "Atualização";
}

/// <summary>Um problema conhecido publicado pela Microsoft (Windows release health).</summary>
public sealed record WinIssue(string Title, string Summary, int OriginBuild, int OriginRevision, string OriginKb, string Status, string? FixKb, string Updated)
{
    public bool Open => !Status.StartsWith("Resolved", StringComparison.OrdinalIgnoreCase);
    public string StatusText => Status switch
    {
        var s when s.StartsWith("Resolved", StringComparison.OrdinalIgnoreCase) => "Resolvido" + (FixKb != null ? " na " + FixKb : ""),
        var s when s.StartsWith("Mitigated", StringComparison.OrdinalIgnoreCase) => "Contornado (ainda sem correção definitiva)",
        var s when s.StartsWith("Confirmed", StringComparison.OrdinalIgnoreCase) => "Confirmado, sem correção",
        var s when s.StartsWith("Investigating", StringComparison.OrdinalIgnoreCase) => "Em investigação",
        var s => s,
    };
}

/// <summary>Uma atualização que o Windows Update oferece para este PC.</summary>
public sealed record WuItem(string Title, string Kb, string Kind, long Size);

/// <summary>Resultado da avaliação de uma atualização: ok, atenção ou problema, com os motivos.</summary>
public sealed record WinVerdict(string Level, string Text, List<WinIssue> Issues, List<WinIssue> Fixes);

/// <summary>
/// Atualizações do Windows: o que falta instalar e se a Microsoft já reconheceu problema nelas. Lê duas páginas oficiais
/// (histórico de lançamentos e "known issues" do Windows release health), então funciona mesmo com o Windows Update
/// bloqueado; quando o Update responde, também lista o que ele oferece (drivers, Defender, .NET…). Nada é instalado aqui.
/// </summary>
public static class WinUpdates
{
    private static readonly HttpClient Http = CreateHttp();
    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        return c;
    }

    public sealed record LocalInfo(string Product, string DisplayVersion, int Build, int Revision)
    {
        public bool Win11 => Build >= 22000;
        public string BuildText => $"{Build}.{Revision}";
    }

    public static LocalInfo ReadLocal()
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        int build = int.TryParse(k?.GetValue("CurrentBuild")?.ToString(), out var b) ? b : 0;
        int ubr = k?.GetValue("UBR") is int u ? u : 0;
        string disp = k?.GetValue("DisplayVersion")?.ToString() ?? k?.GetValue("ReleaseId")?.ToString() ?? "";
        string prod = build >= 22000 ? "Windows 11" : "Windows 10";
        return new LocalInfo(prod, disp, build, ubr);
    }

    public static string HistoryUrl(LocalInfo l) => l.Win11
        ? "https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information"
        : "https://learn.microsoft.com/en-us/windows/release-health/release-information";

    public static string IssuesUrl(LocalInfo l) =>
        $"https://learn.microsoft.com/en-us/windows/release-health/status-windows-{(l.Win11 ? "11" : "10")}-{l.DisplayVersion.ToLowerInvariant()}";

    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<br\\s*/?>", " "), "<[^>]+>", "")).Trim();
    private static string Clean(string s) => Regex.Replace(s, "\\s+", " ").Trim();

    /// <summary>Lê a tabela de histórico da versão (OS build NNNNN) na página de informações de lançamento.</summary>
    public static List<WinRelease> ParseHistory(string html, int build)
    {
        var list = new List<WinRelease>();
        int at = html.IndexOf($"(OS build {build})", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return list;
        int start = html.IndexOf("<table", at, StringComparison.OrdinalIgnoreCase);
        int end = start < 0 ? -1 : html.IndexOf("</table>", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return list;
        foreach (Match row in Regex.Matches(html[start..end], "<tr>(.*?)</tr>", RegexOptions.Singleline))
        {
            var cells = Regex.Matches(row.Groups[1].Value, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline).Select(m => Clean(Text(m.Groups[1].Value))).ToList();
            if (cells.Count < 5) continue;
            var bm = Regex.Match(cells[3], "^(\\d+)\\.(\\d+)$");
            if (!bm.Success) continue;
            list.Add(new WinRelease(int.Parse(bm.Groups[1].Value), int.Parse(bm.Groups[2].Value), cells[4], cells[1], cells[2]));
        }
        return list;
    }

    /// <summary>Lê o resumo de problemas conhecidos (título, update de origem, status) da página da versão.</summary>
    public static List<WinIssue> ParseIssues(string html)
    {
        var list = new List<WinIssue>();
        int at = html.IndexOf("id=\"known-issues\"", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return list;
        int start = html.IndexOf("<tbody", at, StringComparison.OrdinalIgnoreCase);
        int end = start < 0 ? -1 : html.IndexOf("</tbody>", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return list;
        foreach (Match row in Regex.Matches(html[start..end], "<tr>(.*?)</tr>", RegexOptions.Singleline))
        {
            var cells = Regex.Matches(row.Groups[1].Value, "<td[^>]*>(.*?)</td>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
            if (cells.Count < 4) continue;
            string title = Clean(Text(Regex.Match(cells[0], "<b>(.*?)</b>", RegexOptions.Singleline).Groups[1].Value));
            string summary = Clean(Text(Regex.Replace(cells[0], "<a[^>]*>.*?</a>", "", RegexOptions.Singleline)));
            var ob = Regex.Match(cells[1], "OS Builds? (\\d+)\\.(\\d+)");
            string okb = Regex.Match(cells[1], "KB\\d+").Value;
            string status = Clean(Text(cells[2]));
            string? fix = Regex.Match(cells[2], "KB\\d+") is { Success: true } fm ? fm.Value : null;
            if (title.Length == 0) continue;
            list.Add(new WinIssue(title, summary, ob.Success ? int.Parse(ob.Groups[1].Value) : 0, ob.Success ? int.Parse(ob.Groups[2].Value) : 0,
                okb, status.Split(' ')[0], fix, Clean(Text(cells[3]))));
        }
        return list;
    }

    /// <summary>
    /// Avalia uma atualização cumulativa: um problema conhecido vale para ela se começou nesta revisão ou antes e ainda
    /// não foi resolvido até ela. As atualizações são cumulativas, então o problema de um mês segue nos seguintes até a correção.
    /// </summary>
    public static WinVerdict Judge(WinRelease r, List<WinIssue> issues, List<WinRelease> history, int installedRevision)
    {
        int Rev(string? kb) => kb == null ? int.MaxValue : history.FirstOrDefault(h => h.Kb.Equals(kb, StringComparison.OrdinalIgnoreCase))?.Revision ?? int.MaxValue;
        bool Same(WinIssue i) => i.OriginBuild == 0 || i.OriginBuild == r.Build || history.Any(h => h.Kb.Equals(i.OriginKb, StringComparison.OrdinalIgnoreCase));
        int OriginRev(WinIssue i) => history.FirstOrDefault(h => h.Kb.Equals(i.OriginKb, StringComparison.OrdinalIgnoreCase))?.Revision ?? i.OriginRevision;

        var affects = issues.Where(i => Same(i) && OriginRev(i) <= r.Revision && (i.Open || Rev(i.FixKb) > r.Revision)).ToList();
        var fixes = issues.Where(i => !i.Open && i.FixKb != null && Rev(i.FixKb) <= r.Revision && Rev(i.FixKb) > installedRevision).ToList();
        var news = affects.Where(i => OriginRev(i) > installedRevision).ToList(); // problemas que você ainda não tem e passaria a ter
        if (news.Any(i => i.Status.StartsWith("Confirmed") || i.Status.StartsWith("Investigating")))
            return new WinVerdict("problema", $"Cuidado: a Microsoft confirmou {news.Count} problema(s) que começaram nesta atualização ou depois da sua e ainda não têm correção.", affects, fixes);
        if (news.Count > 0)
            return new WinVerdict("atencao", $"Atenção: {news.Count} problema(s) conhecido(s) começaram nesta atualização ou depois da sua; a Microsoft já tem contorno, mas não a correção definitiva.", affects, fixes);
        if (r.Type.EndsWith(" D"))
            return new WinVerdict("ok", "Sem problemas conhecidos novos. É uma prévia opcional: pode esperar a mensal do próximo mês, que traz o mesmo conteúdo já testado por mais gente.", affects, fixes);
        return new WinVerdict("ok", "Sem problemas conhecidos novos publicados pela Microsoft até agora.", affects, fixes);
    }

    public static async Task<string> GetAsync(string url, CancellationToken ct = default) => await Http.GetStringAsync(url, ct);

    /// <summary>Pergunta ao Windows Update o que ele oferece (só procura, não baixa). Se estiver bloqueado, tenta direto no servidor da Microsoft.</summary>
    public static async Task<(List<WuItem> Items, string? Error)> SearchWindowsUpdateAsync()
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $s = New-Object -ComObject Microsoft.Update.Session
            $q = $s.CreateUpdateSearcher()
            try { $r = $q.Search("IsInstalled=0 and IsHidden=0") }
            catch { $q.ServerSelection = 2; $r = $q.Search("IsInstalled=0 and IsHidden=0") }
            $o = foreach ($u in $r.Updates) {
              [pscustomobject]@{ T = [string]$u.Title; K = (@($u.KBArticleIDs) | ForEach-Object { 'KB' + $_ }) -join ','
                C = (@($u.Categories | ForEach-Object { [string]$_.Name }) -join ', '); Z = [long]$u.MaxDownloadSize }
            }
            ConvertTo-Json -Compress -InputObject @($o)
            """;
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8;" + script)))
        { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        try
        {
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(180_000)) { try { p.Kill(true); } catch { } return (new(), "o Windows Update demorou demais para responder"); }
            string json = (await o).Trim(), err = (await e).Trim();
            int i = json.IndexOf('[');
            if (i < 0) return (new(), err.Length > 0 ? FirstLine(err) : "o Windows Update não respondeu");
            var list = new List<WuItem>();
            foreach (var el in JsonDocument.Parse(json[i..]).RootElement.EnumerateArray())
                list.Add(new WuItem(S(el, "T"), S(el, "K"), S(el, "C"), el.TryGetProperty("Z", out var z) && z.TryGetInt64(out var zz) ? zz : 0));
            return (list, null);
        }
        catch (Exception ex) { return (new(), ex.Message); }

        static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        static string FirstLine(string s) => s.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("At ") && !l.StartsWith("No ")) ?? s;
    }
}
