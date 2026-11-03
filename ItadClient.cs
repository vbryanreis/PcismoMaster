using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResetExplode;

// ---- Modelos da API do IsThereAnyDeal (só os campos que a aba Promoções usa) ----

public sealed class ItadPrice
{
    [JsonPropertyName("amount")] public double Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "";
}

public sealed class ItadShop
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class ItadAssets
{
    [JsonPropertyName("banner300")] public string? Banner300 { get; set; }
    [JsonPropertyName("banner400")] public string? Banner400 { get; set; }
    [JsonPropertyName("banner600")] public string? Banner600 { get; set; }
    [JsonPropertyName("boxart")] public string? Boxart { get; set; }
    /// <summary>Banner horizontal mais adequado para os cartões (o maior que vier).</summary>
    public string? Banner => Banner400 ?? Banner600 ?? Banner300 ?? Boxart;
}

/// <summary>Preço de um jogo numa loja. Na lista de promoções vem com o menor preço histórico junto.</summary>
public sealed class ItadDeal
{
    [JsonPropertyName("shop")] public ItadShop Shop { get; set; } = new();
    [JsonPropertyName("price")] public ItadPrice Price { get; set; } = new();
    [JsonPropertyName("regular")] public ItadPrice Regular { get; set; } = new();
    [JsonPropertyName("cut")] public int Cut { get; set; }
    [JsonPropertyName("voucher")] public string? Voucher { get; set; }
    [JsonPropertyName("storeLow")] public ItadPrice? StoreLow { get; set; }
    [JsonPropertyName("historyLow")] public ItadPrice? HistoryLow { get; set; }
    /// <summary>H = menor preço da história, N = novo menor da história, S = menor preço nesta loja.</summary>
    [JsonPropertyName("flag")] public string? Flag { get; set; }
    [JsonPropertyName("expiry")] public DateTimeOffset? Expiry { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

public sealed class ItadGame
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    /// <summary>game, dlc ou package.</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("mature")] public bool Mature { get; set; }
    [JsonPropertyName("assets")] public ItadAssets? Assets { get; set; }
}

public sealed class ItadDealItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("assets")] public ItadAssets? Assets { get; set; }
    [JsonPropertyName("deal")] public ItadDeal Deal { get; set; } = new();
}

public sealed class ItadDealsPage
{
    [JsonPropertyName("nextOffset")] public int NextOffset { get; set; }
    [JsonPropertyName("hasMore")] public bool HasMore { get; set; }
    [JsonPropertyName("list")] public List<ItadDealItem> List { get; set; } = new();
}

public sealed class ItadHistoryLow
{
    [JsonPropertyName("all")] public ItadPrice? All { get; set; }
}

/// <summary>Preços de um jogo em todas as lojas cobertas, mais o menor preço da história.</summary>
public sealed class ItadGamePrices
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("historyLow")] public ItadHistoryLow? HistoryLow { get; set; }
    [JsonPropertyName("deals")] public List<ItadDeal> Deals { get; set; } = new();
}

public sealed class ItadGiveawayGame
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("assets")] public ItadAssets? Assets { get; set; }
}

/// <summary>Jogo grátis por tempo limitado (Epic, GOG, Steam…).</summary>
public sealed class ItadGiveaway
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("shop")] public ItadShop? Shop { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("expiry")] public DateTimeOffset? Expiry { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("games")] public List<ItadGiveawayGame> Games { get; set; } = new();
}

public sealed class ItadShopInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("deals")] public int Deals { get; set; }
}

/// <summary>Filtros da lista de promoções (mesmos nomes de ordenação do site do IsThereAnyDeal).</summary>
public sealed class ItadDealsQuery
{
    /// <summary>-hot (em alta), -cut (maior desconto), price (menor preço), -time (mais recentes), rank (populares)…</summary>
    public string Sort { get; set; } = "-hot";
    public List<int> Shops { get; set; } = new();
    /// <summary>Desconto mínimo em % (0 = qualquer).</summary>
    public int CutMin { get; set; }
    /// <summary>Preço máximo na moeda do país (null = qualquer).</summary>
    public double? PriceMax { get; set; }
    /// <summary>1 = jogo, 2 = DLC, 3 = pacote. Vazio = todos.</summary>
    public List<int> Types { get; set; } = new();
    public bool HistoricalLowOnly { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 40;
}

/// <summary>Erro da API já com mensagem para mostrar na tela.</summary>
public sealed class ItadException(string message) : Exception(message);

/// <summary>
/// Cliente da API do IsThereAnyDeal (api.isthereanydeal.com). Usa a chave do app do PcismoMaster embutida no build do
/// Release (segredo do GitHub, nunca no código) ou a chave própria do usuário. A chave vai no cabeçalho, nunca na URL.
/// As respostas ficam em cache em memória por alguns minutos para respeitar o limite de requisições da API.
/// </summary>
public sealed class ItadClient
{
    public const string BaseUrl = "https://api.isthereanydeal.com";
    public const int SteamShopId = 61;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly Dictionary<string, (DateTime At, string Json)> _cache = new();

    public ItadClient(HttpMessageHandler? handler = null)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
    }

    /// <summary>Chave própria do usuário (tem prioridade sobre a embutida).</summary>
    public string? UserKey { get; set; }

    /// <summary>Chave do app colocada no build pelo workflow de Release (-p:ItadApiKey=…).</summary>
    public static string? EmbeddedKey { get; } =
        typeof(ItadClient).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ItadApiKey")?.Value is { Length: > 0 } k ? k : null;

    public string? ApiKey => CleanKey(UserKey) ?? CleanKey(EmbeddedKey);

    /// <summary>Tira espaços, quebras de linha e aspas que vêm junto ao copiar e colar a chave do site.</summary>
    public static string? CleanKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string k = new string(key.Where(c => !char.IsWhiteSpace(c)).ToArray()).Trim('"', '\'', '“', '”', '‘', '’');
        return k.Length == 0 ? null : k;
    }
    public bool Configured => ApiKey != null;

    /// <summary>País dos preços: o da região do Windows (Brasil = BR, em reais); se não der para saber, BR.</summary>
    public static string Country
    {
        get
        {
            try
            {
                string r = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                return r.Length == 2 && r.All(char.IsLetter) ? r.ToUpperInvariant() : "BR";
            }
            catch { return "BR"; }
        }
    }

    public void ClearCache() => _cache.Clear();

    // ---- Endpoints ----

    /// <summary>Lojas cobertas no país, com quantas promoções cada uma tem agora (não precisa de chave).</summary>
    public async Task<List<ItadShopInfo>> ShopsAsync(string country, CancellationToken ct = default) =>
        Parse<List<ItadShopInfo>>(await SendAsync(HttpMethod.Get, $"/service/shops/v1?country={country}", null, false, TimeSpan.FromHours(12), ct));

    /// <summary>Lista de promoções. Vai por GET com os filtros na URL (o POST com corpo JSON deu "Missing data for country").</summary>
    public async Task<ItadDealsPage> DealsAsync(ItadDealsQuery q, string country, CancellationToken ct = default) =>
        Parse<ItadDealsPage>(await SendAsync(HttpMethod.Get, "/deals/v2?" + BuildDealsQuery(q, country), null, true, DefaultTtl, ct));

    public async Task<List<ItadGame>> SearchAsync(string title, int results = 20, CancellationToken ct = default) =>
        Parse<List<ItadGame>>(await SendAsync(HttpMethod.Get,
            $"/games/search/v1?title={Uri.EscapeDataString(title.Trim())}&results={results}", null, true, DefaultTtl, ct));

    /// <summary>Preços nas lojas para até 200 jogos por chamada (divide sozinho se vierem mais).</summary>
    public async Task<List<ItadGamePrices>> PricesAsync(IEnumerable<string> gameIds, string country, bool dealsOnly = false,
                                                        CancellationToken ct = default)
    {
        var all = new List<ItadGamePrices>();
        foreach (var chunk in gameIds.Distinct().Chunk(200))
            all.AddRange(Parse<List<ItadGamePrices>>(await SendAsync(HttpMethod.Post,
                $"/games/prices/v3?country={country}{(dealsOnly ? "&deals=true" : "")}", JsonSerializer.Serialize(chunk), true, DefaultTtl, ct)));
        return all;
    }

    public async Task<List<ItadGiveaway>> GiveawaysAsync(CancellationToken ct = default) =>
        Parse<List<ItadGiveaway>>(await SendAsync(HttpMethod.Get, "/giveaways/v1?limit=50", null, true, TimeSpan.FromMinutes(30), ct));

    /// <summary>IDs do IsThereAnyDeal a partir dos appids da Steam ("220" → id do Half-Life 2).</summary>
    public async Task<Dictionary<string, string>> LookupSteamAsync(IEnumerable<string> appIds, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var chunk in appIds.Distinct().Chunk(200))
        {
            var res = Parse<Dictionary<string, string?>>(await SendAsync(HttpMethod.Post, $"/lookup/id/shop/{SteamShopId}/v1",
                JsonSerializer.Serialize(chunk.Select(a => "app/" + a)), true, TimeSpan.FromDays(1), ct));
            foreach (var (k, v) in res)
                if (v != null && k.StartsWith("app/")) map[k[4..]] = v;
        }
        return map;
    }

    /// <summary>IDs do IsThereAnyDeal pelo nome exato do jogo (para jogos de fora da Steam).</summary>
    public async Task<Dictionary<string, string>> LookupTitlesAsync(IEnumerable<string> titles, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var chunk in titles.Distinct().Chunk(200))
        {
            var res = Parse<Dictionary<string, string?>>(await SendAsync(HttpMethod.Post, "/lookup/id/title/v1",
                JsonSerializer.Serialize(chunk), true, TimeSpan.FromDays(1), ct));
            foreach (var (k, v) in res) if (v != null) map[k] = v;
        }
        return map;
    }

    // ---- Requisição com cache ----

    private async Task<string> SendAsync(HttpMethod method, string path, string? body, bool needsKey, TimeSpan ttl, CancellationToken ct)
    {
        string cacheKey = method + " " + path + " " + body;
        if (_cache.TryGetValue(cacheKey, out var hit) && DateTime.UtcNow - hit.At < ttl) return hit.Json;
        if (needsKey && !Configured) throw new ItadException("Falta a chave da API do IsThereAnyDeal.");

        using var req = new HttpRequestMessage(method, BaseUrl + path);
        if (needsKey) req.Headers.TryAddWithoutValidation("ITAD-API-Key", ApiKey);
        if (body != null)
        {
            // A API pede exatamente "application/json" (sem o "; charset=utf-8" que o .NET põe por padrão).
            req.Content = new StringContent(body, Encoding.UTF8);
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new ItadException("Sem conexão com o IsThereAnyDeal: " + ex.Message); }

        using (resp)
        {
            string text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new ItadException(ErrorMessage(resp, text));
            _cache[cacheKey] = (DateTime.UtcNow, text);
            if (_cache.Count > 300) // não deixa o cache crescer sem fim: descarta os mais velhos
                foreach (var old in _cache.OrderBy(kv => kv.Value.At).Take(100).Select(kv => kv.Key).ToList()) _cache.Remove(old);
            return text;
        }
    }

    private static string ErrorMessage(HttpResponseMessage resp, string text)
    {
        string reason = "";
        try { using var doc = JsonDocument.Parse(text); if (doc.RootElement.TryGetProperty("reason_phrase", out var r)) reason = r.GetString() ?? ""; }
        catch { }

        switch (resp.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                // A API diz o motivo exato ("Missing api key", "Invalid or expired api key"…): mostra junto para facilitar.
                if (reason.Contains("invalid", StringComparison.OrdinalIgnoreCase) || reason.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    return "O IsThereAnyDeal diz que a chave é inválida ou expirou. Confira se colou a \"API Key\" do seu app " +
                           "(não o Client ID nem o Client Secret) ou gere uma nova no site.";
                if (reason.Contains("missing", StringComparison.OrdinalIgnoreCase))
                    return "O IsThereAnyDeal não recebeu a chave da API. Cole a chave em \"Chave da API\".";
                return "O IsThereAnyDeal recusou a chave da API" + (reason.Length > 0 ? $" ({reason})" : "") + ". Confira a chave em \"Chave da API\".";
            case HttpStatusCode.TooManyRequests:
                var wait = resp.Headers.RetryAfter?.Delta ?? (resp.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                return wait is { } w && w > TimeSpan.Zero
                    ? $"Muitas consultas seguidas ao IsThereAnyDeal. Tente de novo em {Math.Ceiling(w.TotalSeconds)} s."
                    : "Muitas consultas seguidas ao IsThereAnyDeal. Tente de novo em alguns minutos.";
        }
        return $"O IsThereAnyDeal respondeu com erro {(int)resp.StatusCode}{(reason.Length > 0 ? ": " + reason : "")}.";
    }

    public static T Parse<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json) ?? throw new ItadException("Resposta vazia do IsThereAnyDeal."); }
        catch (JsonException) { throw new ItadException("Resposta inesperada do IsThereAnyDeal."); }
    }

    /// <summary>
    /// Parâmetros do GET /deals/v2 (sem o "?"). As lojas vão separadas por vírgula e o filtro vai como JSON na URL,
    /// como nos exemplos da documentação. Os intervalos levam sempre "min" e "max" (null = sem limite), como a API exige.
    /// </summary>
    public static string BuildDealsQuery(ItadDealsQuery q, string country)
    {
        var filter = new Dictionary<string, object?>();
        if (q.CutMin > 0) filter["cut"] = new Dictionary<string, object?> { ["min"] = q.CutMin, ["max"] = null };
        if (q.PriceMax is double max) filter["price"] = new Dictionary<string, object?> { ["min"] = null, ["max"] = max };
        if (q.Types.Count > 0) filter["type"] = q.Types;
        if (q.HistoricalLowOnly) filter["flag"] = "H";

        var parts = new List<string>
        {
            "country=" + Uri.EscapeDataString(country),
            "offset=" + q.Offset.ToString(CultureInfo.InvariantCulture),
            "limit=" + q.Limit.ToString(CultureInfo.InvariantCulture),
            "sort=" + Uri.EscapeDataString(q.Sort),
            "mature=false",
        };
        if (q.Shops.Count > 0) parts.Add("shops=" + string.Join(",", q.Shops.Select(s => s.ToString(CultureInfo.InvariantCulture))));
        if (filter.Count > 0) parts.Add("filter=" + Uri.EscapeDataString(JsonSerializer.Serialize(filter)));
        return string.Join("&", parts);
    }

    // ---- Formatação ----

    private static readonly Dictionary<string, CultureInfo?> CurrencyCultures = new();

    /// <summary>Preço na moeda certa ("R$ 49,90", "$19.99"…); 0 vira "Grátis".</summary>
    public static string FormatPrice(ItadPrice? p)
    {
        if (p == null) return "—";
        if (p.Amount <= 0) return "Grátis";
        return CultureFor(p.Currency) is { } c ? p.Amount.ToString("C", c) : $"{p.Amount:0.00} {p.Currency}";
    }

    /// <summary>Valor na moeda do país dos preços (para os rótulos dos filtros).</summary>
    public static string FormatAmount(double amount, string country)
    {
        try { return FormatPrice(new ItadPrice { Amount = amount, Currency = new RegionInfo(country).ISOCurrencySymbol }).Replace(",00", "").Replace(".00", ""); }
        catch { return amount.ToString("0"); }
    }

    private static CultureInfo? CultureFor(string iso)
    {
        lock (CurrencyCultures)
        {
            if (CurrencyCultures.TryGetValue(iso, out var c)) return c;
            static bool Uses(CultureInfo ci, string iso) { try { return new RegionInfo(ci.Name).ISOCurrencySymbol == iso; } catch { return false; } }
            var current = CultureInfo.CurrentCulture;
            c = Uses(current, iso) ? current
                : iso == "BRL" ? CultureInfo.GetCultureInfo("pt-BR")
                : iso == "USD" ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.GetCultures(CultureTypes.SpecificCultures).FirstOrDefault(ci => Uses(ci, iso));
            return CurrencyCultures[iso] = c;
        }
    }

    /// <summary>"Acaba hoje", "Acaba em 3 dias"… (null se não tiver data de fim).</summary>
    public static string? FormatExpiry(DateTimeOffset? expiry, DateTimeOffset now)
    {
        if (expiry is not { } e) return null;
        var left = e - now;
        if (left <= TimeSpan.Zero) return "Encerrada";
        if (left.TotalHours < 24) return left.TotalHours < 1 ? "Acaba em menos de 1 hora" : $"Acaba em {(int)left.TotalHours} h";
        int days = (int)Math.Ceiling(left.TotalDays);
        return days == 1 ? "Acaba amanhã" : $"Acaba em {days} dias";
    }
}
