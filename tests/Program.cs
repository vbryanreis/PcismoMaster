using ResetExplode;
using ResetExplode.Modular;
using Microsoft.Win32;

int passed = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
void Assert(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected failure"); }
var temp = Path.Combine(Path.GetTempPath(), "PcismoMasterTests-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
var module = ModuleCatalog.All.Single(m => m.Id == "background");
var fake = new FakeStore();
ModuleEngine Engine(string id) => new(fake, Path.Combine(temp, id));
Test("Catalog has no overlapping registry settings", () => {
    var settings = ModuleCatalog.All.SelectMany(m => m.Settings).Select(s => s.Hive + s.Path + s.Name).ToArray();
    Assert(settings.Distinct(StringComparer.OrdinalIgnoreCase).Count() == settings.Length);
});
Test("Enable, restart engine, restore absent and custom values", () => {
    fake.Values[module.Settings[0]] = new(true, 4, "7");
    var before = module.Settings.Select(fake.Read).ToArray();
    var engine = Engine("roundtrip"); engine.Enable(module); Assert(engine.Matches(module));
    Throws(() => engine.Enable(module));
    Engine("roundtrip").Restore(module);
    Assert(module.Settings.Select(fake.Read).SequenceEqual(before)); Assert(!engine.HasBackup(module));
});
Test("Write failure rolls back partial changes", () => {
    fake.FailAt = fake.Writes + 2;
    var before = module.Settings.Select(fake.Read).ToArray(); var engine = Engine("failure");
    Throws(() => engine.Enable(module)); Assert(module.Settings.Select(fake.Read).SequenceEqual(before)); Assert(!engine.HasBackup(module));
});
Test("Recovery failure preserves backup and can be retried", () => {
    fake.AlwaysFail = true; var engine = Engine("retry"); Throws(() => engine.Enable(module)); Assert(engine.HasBackup(module));
    fake.AlwaysFail = false; engine.Restore(module); Assert(!engine.HasBackup(module));
});
Test("External drift is detected and original backup remains usable", () => {
    var engine = Engine("drift"); var before = module.Settings.Select(fake.Read).ToArray(); engine.Enable(module);
    fake.Values[module.Settings[0]] = new(true, 4, "99"); Assert(!engine.Matches(module)); engine.Restore(module);
    Assert(module.Settings.Select(fake.Read).SequenceEqual(before));
});
Test("Corrupted journal fails closed", () => {
    var engine = Engine("corrupt"); engine.Enable(module); var writes = fake.Writes;
    File.WriteAllText(Path.Combine(temp, "corrupt", module.Id + ".json"), "{}");
    Throws(() => engine.Restore(module)); Assert(fake.Writes == writes); Assert(engine.HasBackup(module));
});
Test("Restore without backup fails clearly and writes nothing", () => {
    var engine = Engine("never-created"); var writes = fake.Writes;
    try { engine.Restore(module); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
    Assert(fake.Writes == writes); Assert(!engine.HasBackup(module));
});
var stockCurve = Enumerable.Range(0, 40).Select(i => (Mv: 700 + i * 10, Mhz: 1000 + i * 50)).ToList();
Test("V/F: core offset shifts every point, point offsets add on top", () => {
    var p = new OcProfile { CoreOffsetMhz = 100, PointOffsetsMhz = { [5] = 30 } };
    var t = VfCurveMath.TargetMhz(stockCurve, p);
    Assert(t[3] == stockCurve[3].Mhz + 100); Assert(t[5] == stockCurve[5].Mhz + 130);
    var d = VfCurveMath.DeltasKhz(stockCurve, p); Assert(d[0] == 0 && d[5] == 130_000);
});
Test("V/F: undervolt flattens the curve at the cap and nothing passes it", () => {
    var p = new OcProfile { UvCapMv = 900, UvCapMhz = 2000 };
    var t = VfCurveMath.TargetMhz(stockCurve, p);
    int cap = VfCurveMath.NearestIndex(stockCurve, 900);
    Assert(stockCurve[cap].Mv == 900); Assert(t.Skip(cap).All(x => x == 2000)); Assert(t.Max() == 2000);
    Assert(t[cap - 1] == Math.Min(stockCurve[cap - 1].Mhz, 2000));
});
Test("V/F: deltas never pass the driver's ±1000 MHz limit", () => {
    var t = VfCurveMath.TargetMhz(stockCurve, new OcProfile { CoreOffsetMhz = 900, PointOffsetsMhz = { [2] = 900 } });
    Assert(t.Select((x, i) => Math.Abs(x - stockCurve[i].Mhz)).Max() <= 1000);
    var uv = VfCurveMath.DeltasKhz(stockCurve, new OcProfile { UvCapMv = 700, UvCapMhz = 300 });
    Assert(uv.Min() >= -VfCurveMath.MaxBelowCapMhz * 1000);
});
Test("V/F: after the cap every point gets one offset that keeps it under the cap (like Afterburner)", () => {
    var p = new OcProfile { CoreOffsetMhz = 481, UvCapMv = 900, UvCapMhz = 2895 };
    int cap = VfCurveMath.NearestIndex(stockCurve, 900);
    var d = VfCurveMath.DeltasKhz(stockCurve, p);
    Assert(d.Skip(1).Take(cap - 1).All(x => x == 481_000));
    Assert(d.Skip(cap + 1).Distinct().Count() == 1);
    Assert(Enumerable.Range(cap + 1, stockCurve.Count - cap - 1).All(i => stockCurve[i].Mhz + d[i] / 1000 < 2895));
});
Test("V/F: stock recovery and readability check", () => {
    var live = stockCurve.Select(c => (c.Mv, c.Mhz + 45)).ToList();
    var back = VfCurveMath.SubtractDeltas(live, Enumerable.Repeat(45_000, 40).ToArray());
    Assert(back.SequenceEqual(stockCurve)); Assert(VfCurveMath.Readable(stockCurve));
    Assert(!VfCurveMath.Readable(stockCurve.Select((c, i) => (c.Mv, i == 20 ? 50 : c.Mhz)).ToList()));
});
Test("V/F: detects the flat top an undervolt leaves (and not a rising stock curve)", () => {
    Assert(VfCurveMath.FlatStart(stockCurve) == null);
    var flat = VfCurveMath.TargetMhz(stockCurve, new OcProfile { CoreOffsetMhz = 100, UvCapMv = 900, UvCapMhz = 2100 })
        .Select((m, i) => (stockCurve[i].Mv, m)).ToList();
    Assert(VfCurveMath.FlatStart(flat) == VfCurveMath.NearestIndex(stockCurve, 900));
});
Test("Fan curve interpolates, clamps at the ends and holds with hysteresis", () => {
    var curve = new List<FanPoint> { new(40, 30), new(80, 90) };
    Assert(FanCurveMath.SpeedAt(curve, 20) == 30); Assert(FanCurveMath.SpeedAt(curve, 60) == 60); Assert(FanCurveMath.SpeedAt(curve, 95) == 90);
    Assert(FanCurveMath.Next(curve, 70, 60, 3) == 75);   // esquentou: sobe na hora
    Assert(FanCurveMath.Next(curve, 59, 60, 3) == 60);   // esfriou 1 °C: segura
    Assert(FanCurveMath.Next(curve, 50, 60, 3) == 50);   // esfriou bem: desce com folga de 3 °C (pede 45, entrega 50)
});
Test("Profiles clone deeply", () => {
    var a = new OcProfile { PointOffsetsMhz = { [1] = 10 } }; var b = a.Clone();
    b.PointOffsetsMhz[1] = 99; b.FanCurve[0] = new(10, 10);
    Assert(a.PointOffsetsMhz[1] == 10); Assert(a.FanCurve[0] != b.FanCurve[0]);
});
Test("Real registry roundtrip only inside isolated test key", () => {
    var path = @"Software\PcismoMaster.Tests\" + Guid.NewGuid();
    var setting = new Setting("HKCU", path, "Sample", 0); var store = new RegistryStore();
    try {
        foreach (var value in new[] { new SavedValue(true, 4, "-1"), new(true, 1, "\"hello\""), new(true, 2, "\"%TEMP%\""), new(true, 7, "[\"a\",\"b\"]"), new(true, 3, "\"AQID\""), new(true, 11, "1234567890123"), new(false, 0, "") }) {
            store.Write(setting, value); Assert(store.Read(setting) == value);
        }
    } finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
});
// ---- Promoções (IsThereAnyDeal): tudo com um handler falso, sem rede e sem chave de verdade ----
Dictionary<string, string> QueryOf(string query) => query.Split('&').Select(p => p.Split('=', 2))
    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));
Test("ITAD: deals query sends country, sort, shops and a filter with min and max ranges", () => {
    var p = QueryOf(ItadClient.BuildDealsQuery(new ItadDealsQuery { Sort = "-cut", CutMin = 50, PriceMax = 25.5, Types = { 1 }, HistoricalLowOnly = true, Shops = { 61, 35 }, Offset = 40 }, "BR"));
    Assert(p["country"] == "BR" && p["sort"] == "-cut" && p["offset"] == "40" && p["limit"] == "40" && p["mature"] == "false" && p["shops"] == "61,35");
    using var doc = System.Text.Json.JsonDocument.Parse(p["filter"]); var f = doc.RootElement;
    Assert(f.GetProperty("cut").GetProperty("min").GetInt32() == 50 && f.GetProperty("cut").GetProperty("max").ValueKind == System.Text.Json.JsonValueKind.Null);
    Assert(f.GetProperty("price").GetProperty("min").ValueKind == System.Text.Json.JsonValueKind.Null && f.GetProperty("price").GetProperty("max").GetDouble() == 25.5);
    Assert(f.GetProperty("type")[0].GetInt32() == 1 && f.GetProperty("flag").GetString() == "H");
    var empty = QueryOf(ItadClient.BuildDealsQuery(new ItadDealsQuery(), "BR"));
    Assert(empty["country"] == "BR" && empty["sort"] == "-hot" && !empty.ContainsKey("filter") && !empty.ContainsKey("shops"));
});
Test("ITAD: deals list goes as GET with the country in the URL; POSTs send exactly application/json", () => {
    var h = new FakeItadHandler { Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"nextOffset":0,"hasMore":false,"list":[]}""") } };
    var api = new ItadClient(h) { UserKey = "k" };
    api.DealsAsync(new ItadDealsQuery { CutMin = 75 }, "BR").GetAwaiter().GetResult();
    var r = h.Requests[0];
    Assert(r.Method == HttpMethod.Get && r.Content == null && r.RequestUri!.AbsolutePath == "/deals/v2" && QueryOf(r.RequestUri.Query.TrimStart('?'))["country"] == "BR");
    h.Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
    api.PricesAsync(new[] { "g1" }, "BR").GetAwaiter().GetResult();
    Assert(h.Requests[1].Method == HttpMethod.Post && h.Requests[1].Content!.Headers.ContentType!.ToString() == "application/json");
    Assert(h.Requests[1].RequestUri!.Query.Contains("country=BR"));
});
Test("ITAD: deals list parses prices, flags, nulls and expiry", () => {
    const string json = """
        {"nextOffset":1,"hasMore":true,"list":[{"id":"g1","slug":"hl2","title":"Half-Life 2","type":"game","mature":false,
        "assets":{"banner400":"https://img/b400.jpg"},"deal":{"shop":{"id":61,"name":"Steam"},"price":{"amount":4.79,"amountInt":479,"currency":"BRL"},
        "regular":{"amount":47.99,"amountInt":4799,"currency":"BRL"},"cut":90,"voucher":null,"storeLow":null,"historyLow":null,"historyLow_1y":null,
        "historyLow_3m":null,"flag":"N","drm":[],"platforms":[],"timestamp":"2026-11-01T10:00:00+00:00","expiry":"2026-11-05T17:00:00+00:00","url":"https://itad.link/x"}}]}
        """;
    var page = ItadClient.Parse<ItadDealsPage>(json); var d = page.List[0].Deal;
    Assert(page.HasMore && page.NextOffset == 1 && page.List[0].Assets!.Banner == "https://img/b400.jpg");
    Assert(d.Shop.Name == "Steam" && d.Cut == 90 && d.Price.Amount == 4.79 && d.HistoryLow == null && d.Flag == "N" && d.Expiry!.Value.Day == 5);
    Throws(() => ItadClient.Parse<ItadDealsPage>("<html>"));
});
Test("ITAD: prices are shown in the right currency and 0 is free", () => {
    string Clean(string s) => s.Replace(' ', ' ').Replace(' ', ' ');
    Assert(Clean(ItadClient.FormatPrice(new ItadPrice { Amount = 49.9, Currency = "BRL" })) == "R$ 49,90");
    Assert(ItadClient.FormatPrice(new ItadPrice { Amount = 19.99, Currency = "USD" }) == "$19.99");
    Assert(ItadClient.FormatPrice(new ItadPrice { Amount = 0, Currency = "BRL" }) == "Grátis" && ItadClient.FormatPrice(null) == "—");
    var now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    Assert(ItadClient.FormatExpiry(now.AddDays(3), now) == "Acaba em 3 dias" && ItadClient.FormatExpiry(now.AddHours(5), now) == "Acaba em 5 h");
    Assert(ItadClient.FormatExpiry(null, now) == null && ItadClient.FormatExpiry(now.AddHours(-1), now) == "Encerrada");
});
Test("ITAD: key goes in the header (never in the URL) and answers are cached", () => {
    var h = new FakeItadHandler(); var api = new ItadClient(h) { UserKey = "test-key" };
    var a = api.SearchAsync("Half-Life").GetAwaiter().GetResult(); var b = api.SearchAsync("Half-Life").GetAwaiter().GetResult();
    Assert(h.Requests.Count == 1 && !h.Requests[0].RequestUri!.ToString().Contains("test-key"));
    Assert(h.Requests[0].Headers.GetValues("ITAD-API-Key").Single() == "test-key");
    Assert(h.Requests[0].RequestUri!.AbsolutePath == "/games/search/v1" && h.Requests[0].RequestUri!.Query.Contains("title=Half-Life"));
});
Test("ITAD: missing key, refused key and rate limit give clear messages", () => {
    var h = new FakeItadHandler(); var api = new ItadClient(h);
    try { api.GiveawaysAsync().GetAwaiter().GetResult(); throw new Exception("should fail"); } catch (ItadException e) { Assert(e.Message.Contains("chave")); }
    Assert(h.Requests.Count == 0); // sem chave nem chega a chamar a API
    var shops = api.ShopsAsync("BR").GetAwaiter().GetResult(); Assert(h.Requests.Count == 1); // lojas não precisam de chave
    api.UserKey = "k";
    h.Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
    try { api.GiveawaysAsync().GetAwaiter().GetResult(); throw new Exception("should fail"); } catch (ItadException e) { Assert(e.Message.Contains("recusou a chave")); }
    h.Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) { Content = new StringContent("""{"status_code":403,"reason_phrase":"Invalid or expired api key"}""") };
    try { api.SearchAsync("y").GetAwaiter().GetResult(); throw new Exception("should fail"); } catch (ItadException e) { Assert(e.Message.Contains("inválida ou expirou") && e.Message.Contains("Client ID")); }
    h.Reply = _ => { var r = new HttpResponseMessage((System.Net.HttpStatusCode)429) { Content = new StringContent("") }; r.Headers.RetryAfter = new(TimeSpan.FromSeconds(30)); return r; };
    try { api.SearchAsync("x").GetAwaiter().GetResult(); throw new Exception("should fail"); } catch (ItadException e) { Assert(e.Message.Contains("30 s")); }
});
Test("ITAD: pasted key is cleaned of spaces, line breaks and quotes", () => {
    Assert(ItadClient.CleanKey("  \"abc123def\"\r\n") == "abc123def" && ItadClient.CleanKey("abc 123") == "abc123");
    Assert(ItadClient.CleanKey("   ") == null && ItadClient.CleanKey(null) == null && ItadClient.CleanKey("“abc”") == "abc");
});
Test("ITAD: Steam lookup maps appids and skips games it does not know", () => {
    var h = new FakeItadHandler { Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"app/220":"g-hl2","app/1":null}""") } };
    var map = new ItadClient(h) { UserKey = "k" }.LookupSteamAsync(new[] { "220", "1" }).GetAwaiter().GetResult();
    Assert(map.Count == 1 && map["220"] == "g-hl2" && h.Requests[0].RequestUri!.AbsolutePath == "/lookup/id/shop/61/v1");
});
Console.WriteLine($"{passed} tests passed. No production tweaks applied.");
Directory.Delete(temp, true);

sealed class FakeItadHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Requests.Add(request); return Task.FromResult(Reply(request)); }
}

sealed class FakeStore : ISettingsStore
{
    public Dictionary<Setting, SavedValue> Values = new(); public int Writes; public int FailAt = -1; public bool AlwaysFail;
    public SavedValue Read(Setting s) => Values.GetValueOrDefault(s, new(false, 0, ""));
    public void Write(Setting s, SavedValue value) { Writes++; if (AlwaysFail || Writes == FailAt) throw new IOException("Injected failure"); Values[s] = value; }
}
