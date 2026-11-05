using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Um arquivo do suporte do fabricante (BIOS ou driver) para o modelo da placa-mãe.</summary>
public sealed record BoardFile(string Category, string Title, string Version, DateTime? Date, string Url, string Size)
{
    public string DateText => Date is DateTime d ? d.ToString("dd/MM/yyyy") : "";
}

/// <summary>Uma linha da comparação: o que o site do fabricante tem x o que está instalado.</summary>
public sealed record BoardDriverRow(string Category, string Title, string SiteVersion, string SiteDate, string Url,
                                    string Installed, string InstalledDate, BoardStatus Status, string Device);

public enum BoardStatus { UpToDate, Newer, NotInstalled, Optional, Check, LocalOnly }

/// <summary>Resultado da consulta do suporte do fabricante para a placa-mãe deste PC.</summary>
public sealed class BoardCheck
{
    public string Brand { get; init; } = "";
    /// <summary>true = a lista veio do site do fabricante; false = só o que está instalado (o site não deixa programas consultarem).</summary>
    public bool Online { get; set; }
    public string Message { get; set; } = "";
    public BoardFile? LatestBios { get; set; }
    /// <summary>null = não deu para comparar (formato diferente ou sem consulta).</summary>
    public bool? BiosUpToDate { get; set; }
    public string BiosPage { get; init; } = "";
    public string DriversPage { get; init; } = "";
    public List<BoardDriverRow> Rows { get; set; } = new();
}

/// <summary>
/// Confere BIOS e drivers da placa-mãe no suporte oficial do fabricante. A ASUS tem uma consulta pública (a mesma que o site
/// usa): lista a BIOS e todos os drivers do modelo, que são comparados com o instalado. Gigabyte, MSI e ASRock bloqueiam
/// programas no site (só navegador): aí o app mostra os drivers da placa instalados e abre a página exata do modelo.
/// </summary>
public static class BoardSupport
{
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        return c;
    }

    public static async Task<BoardCheck> CheckAsync(BoardInfo b, List<DriverRow> installed, CancellationToken ct)
    {
        string model = CleanModel(b.Model);
        var (biosPage, driversPage) = Pages(b.Brand, model);
        var check = new BoardCheck { Brand = b.Brand, BiosPage = biosPage, DriversPage = driversPage };
        var programs = InstalledPrograms();

        if (b.Brand == "ASUS" && model.Length > 0)
        {
            try
            {
                var (bios, drivers) = await AsusAsync(model, ct);
                check.Online = drivers.Count > 0 || bios != null;
                check.LatestBios = bios;
                check.BiosUpToDate = bios == null ? null : CompareBios(BiosVersion(b.Bios), bios.Version);
                check.Rows = Compare(drivers, installed, programs);
                check.Message = check.Online ? $"Lista oficial da ASUS para {model} (Windows 11 64 bits)." : "A ASUS não devolveu arquivos para este modelo.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                check.Message = "Não foi possível consultar a ASUS agora: " + ex.Message;
            }
        }
        else
        {
            check.Message = b.Brand switch
            {
                "Gigabyte" or "MSI" or "ASRock" =>
                    $"O site da {b.Brand} só abre em navegador (bloqueia programas), então a versão mais recente não dá para ler daqui. " +
                    "Abaixo estão os drivers da placa instalados neste PC; compare com a página oficial do modelo nos botões.",
                "" => "Fabricante da placa-mãe não reconhecido. Abaixo estão os drivers instalados.",
                _ => $"A {b.Brand} não tem consulta pública de versões. Abaixo estão os drivers instalados; a página oficial abre nos botões.",
            };
        }
        if (!check.Online) check.Rows = LocalRows(installed, programs);
        return check;
    }

    // ------------------------------------------------------------------ ASUS

    private static async Task<(BoardFile? Bios, List<BoardFile> Drivers)> AsusAsync(string model, CancellationToken ct)
    {
        const string api = "https://www.asus.com/support/api/product.asmx/";
        string m = Uri.EscapeDataString(model);
        var biosTask = Http.GetStringAsync($"{api}GetPDBIOS?website=global&model={m}&pdhashedid=&cpu=", ct);
        var drvTask = Http.GetStringAsync($"{api}GetPDDrivers?website=global&model={m}&pdhashedid=&cpu=&osid=52", ct); // 52 = Windows 11 64 bits
        var bios = ParseAsus(await biosTask).FirstOrDefault(f => f.Category.Equals("BIOS", StringComparison.OrdinalIgnoreCase));
        return (bios, Newest(ParseAsus(await drvTask)));
    }

    /// <summary>Lê a resposta da ASUS: Result.Obj[] = categorias, cada uma com Files[] (Title, Version, ReleaseDate, DownloadUrl.Global).</summary>
    internal static List<BoardFile> ParseAsus(string json)
    {
        var list = new List<BoardFile>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Result", out var res) || res.ValueKind != JsonValueKind.Object) return list;
        if (!res.TryGetProperty("Obj", out var cats) || cats.ValueKind != JsonValueKind.Array) return list;
        foreach (var c in cats.EnumerateArray())
        {
            string cat = c.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            if (!c.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array) continue;
            foreach (var f in files.EnumerateArray())
            {
                string S(string p) => f.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                string url = f.TryGetProperty("DownloadUrl", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("Global", out var g) ? g.GetString() ?? "" : "";
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                DateTime? date = DateTime.TryParseExact(S("ReleaseDate"), "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;
                string title = Regex.Replace(S("Title"), @"\s+", " ").Trim().TrimEnd('.');
                list.Add(new BoardFile(cat, title.Length > 0 ? title : cat, S("Version").Trim().TrimStart('v', 'V'), date, url, S("FileSize")));
            }
        }
        return list;
    }

    /// <summary>Cada categoria lista versões antigas também: fica só a mais nova de cada produto (título sem números).</summary>
    internal static List<BoardFile> Newest(List<BoardFile> files) =>
        files.Where(f => !Regex.IsMatch(f.Category, @"^(BIOS|Manual|Firmware|BIOS Utilities)$", RegexOptions.IgnoreCase))
             .GroupBy(f => (f.Category.ToLowerInvariant(), Family(f.Title)))
             .Select(g => g.OrderByDescending(f => f.Date ?? DateTime.MinValue).ThenByDescending(f => f.Version, Comparer<string>.Create(CompareVersions)).First())
             .OrderBy(f => CategoryOrder(f.Category)).ThenBy(f => f.Title)
             .ToList();

    /// <summary>"Realtek Audio Driver v6.0.9888.1 for windows 11 64-bit" → "realtek audio driver for windows bit": agrupa versões do mesmo produto.</summary>
    internal static string Family(string title) =>
        Regex.Replace(Regex.Replace(title.ToLowerInvariant(), @"v?\d+(\.\d+)*|[^a-z ]", " "), @"\s+", " ").Trim();

    // ------------------------------------------------------------------ comparação com o instalado

    private static readonly string[] Vendors = { "Realtek", "Intel", "AMD", "MediaTek", "Qualcomm", "Killer", "Broadcom", "Marvell", "Aquantia", "ASMedia", "NVIDIA", "Nuvoton", "Genesys" };

    internal static List<BoardDriverRow> Compare(List<BoardFile> site, List<DriverRow> installed, Dictionary<string, string> programs)
    {
        var rows = new List<BoardDriverRow>();
        foreach (var f in site)
        {
            string kind = Kind(f.Category, f.Title);
            string? vendor = Vendors.FirstOrDefault(v => f.Title.Contains(v, StringComparison.OrdinalIgnoreCase));
            string inst = "", instDate = "", device = "";
            if (kind == "chipset")
            {
                var p = programs.FirstOrDefault(kv => Regex.IsMatch(kv.Key, @"(AMD Chipset Software|Chipset Device Software)", RegexOptions.IgnoreCase));
                if (p.Key != null) { inst = p.Value; device = p.Key; }
            }
            else if (kind == "software")
            {
                string key = Regex.Replace(Family(f.Title), @"\b(for|windows|bit|installer|driver|and|utility|tool)\b", "").Trim();
                var words = key.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
                var p = programs.FirstOrDefault(kv => words.Length > 0 && words.All(w => kv.Key.Contains(w, StringComparison.OrdinalIgnoreCase)));
                if (p.Key != null) { inst = p.Value; device = p.Key; }
            }
            else
            {
                var d = installed.FirstOrDefault(r => Matches(kind, r) && (vendor == null || r.Vendor.Contains(vendor, StringComparison.OrdinalIgnoreCase) ||
                                                                            r.Device.Contains(vendor, StringComparison.OrdinalIgnoreCase)));
                if (d != null) { inst = d.Version; instDate = d.Date; device = d.Device; }
            }

            BoardStatus st;
            if (inst.Length == 0) st = kind is "software" or "raid" or "vga" ? BoardStatus.Optional : BoardStatus.NotInstalled;
            else if (!Comparable(f.Version, inst)) st = BoardStatus.Check;
            else st = CompareVersions(f.Version, inst) > 0 ? BoardStatus.Newer : BoardStatus.UpToDate;
            rows.Add(new BoardDriverRow(CategoryName(f.Category, kind), f.Title, f.Version, f.DateText, f.Url, inst, instDate, st, device));
        }
        return rows;
    }

    /// <summary>Sem consulta online: os drivers da placa instalados, por categoria (para comparar com a página oficial).</summary>
    internal static List<BoardDriverRow> LocalRows(List<DriverRow> installed, Dictionary<string, string> programs)
    {
        var rows = new List<BoardDriverRow>();
        var chip = programs.FirstOrDefault(kv => Regex.IsMatch(kv.Key, @"(AMD Chipset Software|Chipset Device Software)", RegexOptions.IgnoreCase));
        rows.Add(chip.Key != null
            ? new BoardDriverRow("Chipset", chip.Key, "", "", "", chip.Value, "", BoardStatus.LocalOnly, chip.Key)
            : new BoardDriverRow("Chipset", "Pacote de chipset (AMD ou Intel)", "", "", "", "", "", BoardStatus.NotInstalled, ""));
        foreach (var kind in new[] { "audio", "lan", "wifi", "bluetooth", "raid", "usb" })
            foreach (var d in installed.Where(r => Matches(kind, r) && !r.Vendor.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)).Take(4))
                rows.Add(new BoardDriverRow(CategoryName("", kind), d.Device, "", "", "", d.Version, d.Date, BoardStatus.LocalOnly, d.Device));
        return rows;
    }

    private static string Kind(string category, string title)
    {
        string c = category.ToLowerInvariant(), t = title.ToLowerInvariant();
        if (c.Contains("chipset")) return "chipset";
        if (c.Contains("audio")) return "audio";
        if (c.Contains("bluetooth")) return "bluetooth";
        if (c.Contains("wireless") || c.Contains("wlan") || t.Contains("wi-fi") || t.Contains("wifi") || t.Contains("wlan")) return "wifi";
        if (c.Contains("lan") || c.Contains("network") || c.Contains("ethernet")) return "lan";
        if (c.Contains("sata") || c.Contains("raid") || c.Contains("storage")) return "raid";
        if (c.Contains("vga") || c.Contains("graphic")) return "vga";
        if (c.Contains("usb") || c.Contains("thunderbolt")) return "usb";
        if (c.Contains("card reader")) return "cardreader";
        return "software";
    }

    private static bool Matches(string kind, DriverRow r)
    {
        string n = r.Device;
        bool virt = Regex.IsMatch(n, @"WAN Miniport|Hyper-V|Virtual|VPN|TAP-|Loopback|Kernel Debug|Wintun|ZeroTier|Npcap", RegexOptions.IgnoreCase);
        return kind switch
        {
            "audio" => r.Class == "MEDIA" && !Regex.IsMatch(n, @"NVIDIA|AMD High Definition|Intel\(R\) Display|Virtual|Steam|Voicemeeter|Oculus", RegexOptions.IgnoreCase),
            "lan" => r.Class == "NET" && !virt && !Regex.IsMatch(n, @"Wi-?Fi|Wireless|WLAN|802\.11|Bluetooth", RegexOptions.IgnoreCase),
            "wifi" => r.Class == "NET" && !virt && Regex.IsMatch(n, @"Wi-?Fi|Wireless|WLAN|802\.11", RegexOptions.IgnoreCase),
            "bluetooth" => r.Class == "BLUETOOTH" && !Regex.IsMatch(n, @"Enumerator|RFCOMM|LE Generic|Device \(|Avrcp|Hands-?free", RegexOptions.IgnoreCase),
            "raid" => (r.Class is "SCSIADAPTER" or "HDC") && Regex.IsMatch(n, @"RAID|RAIDXpert|NVMe RAID|Rapid Storage|RST", RegexOptions.IgnoreCase),
            "vga" => r.Class == "DISPLAY",
            "usb" => r.Class == "USB" && Regex.IsMatch(n, @"Controller|Host|ASMedia|xHCI", RegexOptions.IgnoreCase) && !r.Vendor.StartsWith("(", StringComparison.Ordinal),
            "cardreader" => Regex.IsMatch(n, @"Card Reader", RegexOptions.IgnoreCase),
            _ => false,
        };
    }

    private static string CategoryName(string category, string kind) => kind switch
    {
        "chipset" => "Chipset", "audio" => "Áudio", "lan" => "Rede (cabo)", "wifi" => "Wi-Fi", "bluetooth" => "Bluetooth",
        "raid" => "SATA / RAID", "vga" => "Vídeo integrado", "usb" => "USB", "cardreader" => "Leitor de cartão",
        _ => category.Replace("Utilities: ", "").Replace("Software and Utility", "Programas") is { Length: > 0 } c ? c : "Programas",
    };

    private static int CategoryOrder(string category) => Kind(category, "") switch
    {
        "chipset" => 0, "audio" => 1, "lan" => 2, "wifi" => 3, "bluetooth" => 4, "raid" => 5, "usb" => 6, "vga" => 7, "cardreader" => 8, _ => 9,
    };

    // ------------------------------------------------------------------ versões

    internal static int CompareVersions(string a, string b)
    {
        var x = Numbers(a); var y = Numbers(b);
        for (int i = 0; i < Math.Max(x.Count, y.Count); i++)
        {
            long p = i < x.Count ? x[i] : 0, q = i < y.Count ? y[i] : 0;
            if (p != q) return p.CompareTo(q);
        }
        return 0;
    }

    /// <summary>Só compara quando os dois parecem a mesma numeração (ao menos 2 partes e o primeiro número próximo: 6.x → 7.x sim, 6.x x 31.x não).</summary>
    internal static bool Comparable(string a, string b)
    {
        var x = Numbers(a); var y = Numbers(b);
        return x.Count >= 2 && y.Count >= 2 && Math.Abs(x[0] - y[0]) <= 2;
    }

    private static List<long> Numbers(string v) =>
        Regex.Matches(v ?? "", @"\d+").Select(m => long.TryParse(m.Value, out var n) ? n : 0).ToList();

    /// <summary>"3646 (12/03/2025)" → "3646".</summary>
    internal static string BiosVersion(string bios) => Regex.Replace(bios ?? "", @"\s*\(.*\)\s*$", "").Trim();

    /// <summary>BIOS da ASUS é um número (3646); se o instalado não for número, não dá para comparar.</summary>
    internal static bool? CompareBios(string installed, string latest)
    {
        if (!long.TryParse(installed, out long i) || !long.TryParse(latest, out long l)) return null;
        return i >= l;
    }

    // ------------------------------------------------------------------ páginas oficiais

    /// <summary>"MAG B550 TOMAHAWK (MS-7C91)" → "MAG B550 TOMAHAWK"; tira o "Default string" de placas genéricas.</summary>
    internal static string CleanModel(string model)
    {
        string m = Regex.Replace(model ?? "", @"\s*\(MS-\w+\)\s*$", "").Trim();
        return Regex.IsMatch(m, @"^(Default string|To be filled|System Product Name|Not Applicable)$", RegexOptions.IgnoreCase) ? "" : m;
    }

    /// <summary>Página de BIOS e de drivers do modelo exato no site oficial (ou a busca do site com o modelo).</summary>
    internal static (string Bios, string Drivers) Pages(string brand, string model)
    {
        string e = Uri.EscapeDataString(model);
        if (model.Length == 0) return ("", "");
        switch (brand)
        {
            case "ASUS":
                return ($"https://www.asus.com/supportonly/{e}/helpdesk_bios/", $"https://www.asus.com/supportonly/{e}/helpdesk_download/");
            case "MSI":
                string slug = Regex.Replace(model.Trim(), @"\s+", "-");
                return ($"https://www.msi.com/Motherboard/{slug}/support#bios", $"https://www.msi.com/Motherboard/{slug}/support#driver");
            case "ASRock":
                string cpu = CpuIsAmd() ? "AMD" : "Intel";
                return ($"https://www.asrock.com/mb/{cpu}/{e}/index.asp#BIOS", $"https://www.asrock.com/mb/{cpu}/{e}/index.asp#Download");
            case "Gigabyte":
                string kw = Uri.EscapeDataString(Regex.Replace(model, @"\s*\(rev\.?.*\)$", "", RegexOptions.IgnoreCase));
                return ($"https://www.gigabyte.com/Search?kw={kw}", $"https://www.gigabyte.com/Search?kw={kw}");
            default:
                return ("", "");
        }
    }

    private static bool CpuIsAmd()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (k?.GetValue("VendorIdentifier") as string ?? "").Contains("AMD", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Programas instalados (nome → versão), para o pacote de chipset e os utilitários do fabricante.</summary>
    private static Dictionary<string, string> InstalledPrograms()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (var sub in root?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var k = root!.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n && n.Length > 0) d[n.Trim()] = (k.GetValue("DisplayVersion") as string ?? "").Trim();
                }
            }
            catch { }
        }
        return d;
    }
}
