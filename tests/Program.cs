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
Test("Binary (hex) settings apply and restore like DWORDs", () => {
    var bin = new Module("bin-test", "t", "c", "d", "s", new[] { new Setting("HKLM", @"Software\Fake\UMD", "VSyncControl", 0, Hex: "3000") });
    var engine = Engine("binary"); var before = bin.Settings.Select(fake.Read).ToArray();
    engine.Enable(bin); Assert(engine.Matches(bin)); Assert(fake.Read(bin.Settings[0]).Kind == 3);
    engine.Restore(bin); Assert(bin.Settings.Select(fake.Read).SequenceEqual(before));
});
Test("Remove settings delete the value and restore brings it back", () => {
    var run = new Module("rm-test", "t", "c", "d", "s", new[] { new Setting("HKCU", @"Software\Fake\Run", "AMDNoiseSuppression", 0, Remove: true) });
    fake.Values[run.Settings[0]] = new(true, 1, "\"C:\\\\amd.exe\"");
    var engine = Engine("remove"); engine.Enable(run); Assert(engine.Matches(run)); Assert(!fake.Read(run.Settings[0]).Exists);
    engine.Restore(run); Assert(fake.Read(run.Settings[0]) == new SavedValue(true, 1, "\"C:\\\\amd.exe\""));
});
Test("GPU generation and driver series from the PCI device ID", () => {
    Assert(GpuCard.ArchOf(0x2684) == "Ada" && GpuCard.ArchOf(0x2504) == "Ampere" && GpuCard.ArchOf(0x1B80) == "Pascal" && GpuCard.ArchOf(0x13C2) == "Maxwell");
    Assert(GpuCard.ArchOf(0x2C02) == "Blackwell" && GpuCard.ArchOf(0x2182) == "Turing" && GpuCard.ArchOf(0x0001) == "");
    Assert(new GpuCard("", 0x2182, "Turing", false, 0, false, false).DriverSeries == "16 Series");
    Assert(new GpuCard("", 0x1E87, "Turing", true, 0, false, false).DriverSeries == "RTX 20 Series");
    Assert(new GpuCard("", 0x1B80, "Pascal", false, 0, false, false).DriverSeries == "10 Series");
    Assert(GpuCard.DeviceIdOf(@"PCI\VEN_10DE&DEV_2684&SUBSYS_16F310DE") == 0x2684);
});
Test("Recommended profile adapts to the card", () => {
    var desk = new GpuCard("RTX 4090", 0x2684, "Ada", true, 24576, false, false);
    var lap = new GpuCard("RTX 3060 Laptop", 0x2560, "Ampere", true, 6144, true, true);
    var nip = new (uint, uint, string)[] { (GpuRules.PowerMode, 1, "p"), (GpuRules.SysmemFallback, 1, "s"), (549528094, 1, "t") };
    Assert(GpuRules.Adapt(nip, desk).Length == 3);
    var l = GpuRules.Adapt(nip, lap);
    Assert(l.Length == 3 && l.Any(x => x.Id == 549528094) && l.Any(x => x.Id == GpuRules.OptimusRendering) && !l.Any(x => x.Id == GpuRules.PowerMode));
    var gtx = new GpuCard("GTX 1660", 0x2184, "Turing", false, 6144, false, false);
    var dlisr = new DebloatItem("NvDLISR", "nvidia", "t", "d", false, true); var pcf = new DebloatItem("NVPCF", "nvidia", "t", "d", false, true);
    Assert(GpuRules.ForCard(dlisr, gtx).Default && !GpuRules.ForCard(dlisr, desk).Default);
    Assert(!GpuRules.ForCard(pcf, lap).Default && GpuRules.ForCard(pcf, desk).Default);
    Assert(GpuCard.ArchOf(0x17C8) == "Maxwell" && GpuCard.IsRtx("Turing", "", 0x1F08) && !GpuCard.IsRtx("Turing", "NVIDIA GeForce GTX 1650", 0x1F82));
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
Test("Finder index: names, paths across volumes, unsorted records and filters", () => {
    const long Seq = 0x0003_0000_0000_0000; // sequência no FRN não atrapalha a ligação com o pai
    var idx = MftIndex.FromRecords(new (char, long, long, bool, string)[] {
        ('C', 40 | Seq, 30, false, "relatorio.txt"),   // vem antes do pai: ordem fora de registro
        ('C', 5, 5, true, "."),                       // raiz do volume (pai de si mesma)
        ('C', 30, 20 | Seq, true, "Docs"),
        ('C', 20, 5, true, "Users"),
        ('C', 50, 999, false, "orfao.txt"),           // pai que não existe
        ('D', 5, 5, true, "."),
        ('D', 30, 5, true, "Jogos"),
        ('D', 41, 30, false, "Relatorio-final.pdf"),
    });
    Assert(idx.Ready && idx.Count == 8);
    var all = idx.Search("relatorio", false, false, 10);
    Assert(all.Count == 2 && all[0].FullPath == @"C:\Users\Docs\relatorio.txt" && all[1].FullPath == @"D:\Jogos\Relatorio-final.pdf");
    Assert(idx.Search("orfao", false, false, 10).Single().FullPath == @"C:\orfao.txt");
    Assert(idx.Search("o", false, true, 10).Select(h => h.Name).SequenceEqual(new[] { "Docs", "Jogos" }));
    Assert(idx.Search("o", true, false, 10).All(h => !h.IsDir) && idx.Search("o", false, false, 2).Count == 2);
    Assert(idx.Search("  ", false, false, 10).Count == 0 && idx.Search("xyz", false, false, 10).Count == 0);
    idx.Release(); Assert(!idx.Ready && idx.Search("Docs", false, false, 10).Count == 0);
});
Test("Uninstaller: app name matching (clear, publisher only, unrelated)", () => {
    var ff = Uninstaller.Matcher.For("Mozilla Firefox (x64 pt-BR) 120.0", "Mozilla", "");
    Assert(ff.Level("Firefox") == 2 && ff.Level("Mozilla") == 1 && ff.Level("Thunderbird") == 0 && ff.Level("Mozilla Firefox") == 2);
    var zip = Uninstaller.Matcher.For("7-Zip 23.01 (x64)", "Igor Pavlov", "");
    Assert(zip.Level("7-Zip") == 2 && zip.Level("WinZip") == 0);
    var discord = Uninstaller.Matcher.For("Discord", "Discord Inc.", "");
    Assert(discord.Level("Discord") == 2 && discord.Level("discord-updater") == 2);
    var git = Uninstaller.Matcher.For("Git", "The Git Development Community", "");
    Assert(git.Level("Git") == 2 && git.Level("GitHub Desktop") == 0 && git.Level("Digital") == 0);
    var epic = Uninstaller.Matcher.For("Epic Games Launcher", "Epic Games, Inc.", "");
    Assert(epic.Level("EpicGamesLauncher") == 2 && epic.Level("My Games") == 0);
    Assert(Uninstaller.CleanName("Notepad++ (64-bit x64) v8.6.2") == "Notepad++");
});
Test("Uninstaller: folder guards never allow Windows, roots or personal folders", () => {
    string F(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
    string pf = F(Environment.SpecialFolder.ProgramFiles), prof = F(Environment.SpecialFolder.UserProfile), docs = F(Environment.SpecialFolder.MyDocuments);
    Assert(!Uninstaller.IsSafeFolder(F(Environment.SpecialFolder.Windows)) && !Uninstaller.IsSafeFolder(Path.Combine(F(Environment.SpecialFolder.Windows), "Foo")));
    Assert(!Uninstaller.IsSafeFolder(pf) && Uninstaller.IsSafeFolder(Path.Combine(pf, "FooApp")) && !Uninstaller.IsSafeFolder(Path.Combine(pf, "Common Files")));
    Assert(!Uninstaller.IsSafeFolder(Path.Combine(pf, "Microsoft", "Edge")) && !Uninstaller.IsSafeFolder(@"C:\"));
    Assert(Uninstaller.IsSafeFolder(Path.Combine(F(Environment.SpecialFolder.ApplicationData), "FooApp")));
    Assert(!Uninstaller.IsSafeFolder(F(Environment.SpecialFolder.LocalApplicationData)) && !Uninstaller.IsSafeFolder(Path.Combine(F(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Foo")));
    Assert(!Uninstaller.IsSafeFolder(docs) && Uninstaller.IsSafeFolder(Path.Combine(docs, "FooApp")));
    Assert(!Uninstaller.IsSafeFolder(Path.Combine(prof, "Downloads", "FooApp")) && !Uninstaller.IsSafeFolder(Path.Combine(prof, ".ssh")));
    Assert(Uninstaller.IsSafeFolder(Path.Combine(prof, ".fooapp")) && !Uninstaller.IsSafeFolder(prof));
    Assert(!Uninstaller.IsSafeFolder(F(Environment.SpecialFolder.DesktopDirectory)) && Uninstaller.IsSafeFolder(Path.Combine(Path.GetTempPath(), "FooApp")));
    Assert(Uninstaller.IsSafeFile(Path.Combine(F(Environment.SpecialFolder.DesktopDirectory), "FooApp.lnk")));
    Assert(!Uninstaller.IsSafeFile(Path.Combine(F(Environment.SpecialFolder.System), "drivers", "foo.sys")));
});
Test("Uninstaller: registry guards (system branches out, exact exceptions in)", () => {
    Assert(!Uninstaller.IsSafeRegPath("Software") && !Uninstaller.IsSafeRegPath(@"Software\Microsoft") && !Uninstaller.IsSafeRegPath(@"Software\Classes\foo"));
    Assert(Uninstaller.IsSafeRegPath(@"Software\FooApp") && Uninstaller.IsSafeRegPath(@"Software\Contoso\FooApp"));
    Assert(Uninstaller.IsSafeRegPath(@"Software\WOW6432Node\FooApp") && !Uninstaller.IsSafeRegPath(@"Software\WOW6432Node\Microsoft\X") && !Uninstaller.IsSafeRegPath(@"Software\WOW6432Node"));
    Assert(Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FooApp") && Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{1234-ABCD}"));
    Assert(!Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\Uninstall") && !Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Foo\Bar"));
    Assert(Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\App Paths\fooapp.exe") && !Uninstaller.IsSafeRegPath(@"Software\Microsoft\Windows\CurrentVersion\App Paths"));
    Assert(!Uninstaller.IsSafeRegPath(@"Software\..\System") && !Uninstaller.IsSafeRegPath(@"System\CurrentControlSet\Services\Foo"));
    Assert(Uninstaller.IsSafeRegValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "FooApp") && !Uninstaller.IsSafeRegValue(@"Software\Microsoft\Windows\CurrentVersion\Run", ""));
    Assert(!Uninstaller.IsSafeRegValue(@"Software\Microsoft\Windows\CurrentVersion", "FooApp"));
    var (exe, args) = Uninstaller.ParseCommand("\"C:\\Program Files\\Foo\\unins000.exe\" /SILENT");
    Assert(exe == @"C:\Program Files\Foo\unins000.exe" && args == "/SILENT");
    string reg = Uninstaller.ValueRegFile(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Foo", Microsoft.Win32.RegistryValueKind.String, "\"C:\\Foo\\foo.exe\" -min");
    Assert(reg.Contains(@"[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run]") && reg.Contains("\"Foo\"=\"\\\"C:\\\\Foo\\\\foo.exe\\\" -min\""));
});
Test("Uninstaller: deep folder scan finds app and publisher\\app, counts hidden files, removes read-only", () => {
    string root = Path.Combine(Path.GetTempPath(), "PCMasterTests-" + Guid.NewGuid().ToString("N")[..8]);
    try {
        string app = Path.Combine(root, "FooApp"), sub = Path.Combine(app, "cache");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(app, "settings.json"), "{}");
        string hidden = Path.Combine(sub, "state.bin"); File.WriteAllText(hidden, "x");
        File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.ReadOnly);
        Directory.CreateDirectory(Path.Combine(root, "Contoso", "FooApp")); Directory.CreateDirectory(Path.Combine(root, "Contoso", "OtherApp"));
        Directory.CreateDirectory(Path.Combine(root, "Contoso Labs", "FooApp Data"));
        Directory.CreateDirectory(Path.Combine(root, "Unrelated"));
        var m = Uninstaller.Matcher.For("FooApp 2.0", "Contoso Ltd", "");
        var found = Uninstaller.ScanFolders(new[] { root }, m, ScanMode.Moderado).ToDictionary(f => Path.GetRelativePath(root, f.FilePath!), f => f);
        Assert(found.Count == 3 && found.ContainsKey("FooApp") && found.ContainsKey(Path.Combine("Contoso", "FooApp")) && found.ContainsKey("Contoso Labs"));
        Assert(found.Values.All(f => f.Strong) && found["FooApp"].Detail.Contains("2 arquivo(s)") && found["FooApp"].Detail.Contains("1 oculto(s)") && found["FooApp"].Detail.Contains("1 subpasta(s)"));
        var adv = Uninstaller.ScanFolders(new[] { root }, m, ScanMode.Avancado);
        Assert(adv.Count == 3); // "Contoso" com outro app dentro nunca entra inteira
        var res = Uninstaller.DeleteLeftovers(new[] { found["FooApp"] }, "FooApp", recycle: false, backupRoot: Path.Combine(root, "bk"));
        Assert(res.Ok == 1 && res.Fail == 0 && !Directory.Exists(app) && !Uninstaller.StillThere(found["FooApp"]));
        Assert(Directory.Exists(Path.Combine(root, "Contoso", "OtherApp")) && Directory.Exists(Path.Combine(root, "Unrelated")));
    } finally { foreach (var f in Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>()) File.SetAttributes(f, FileAttributes.Normal); if (Directory.Exists(root)) Directory.Delete(root, true); }
});
Test("Overlay: linhas por componente, cor da temperatura e texto do atalho", () => {
    var parts = new[] { new OverlayPart("GPU", "GPU", "98%", "98%"), new OverlayPart("GPU", "GPU °C", "66 °C", "66 °C", 66), new OverlayPart("CPU", "CPU °C", "88 °C", "88 °C", 88) };
    var lines = OverlayMetrics.Lines(parts);
    Assert(lines.Count == 2 && lines[0] == new OverlayLine("GPU", "98%  66 °C") && lines[1].Label == "CPU");
    Assert(parts[0].Level == 0 && parts[1].Level == 1 && parts[2].Level == 3 && new OverlayPart("X", "X", "", "", 78).Level == 2);
    Assert(OverlayHotkey.Describe(OverlayHotkey.Ctrl | OverlayHotkey.Shift, "O") == "Ctrl + Shift + O" && OverlayHotkey.Describe(0, "F5") == "F5" && OverlayHotkey.Describe(OverlayHotkey.Alt, "D5") == "Alt + 5");
});
Console.WriteLine($"{passed} tests passed. No production tweaks applied.");
Directory.Delete(temp, true);

sealed class FakeStore : ISettingsStore
{
    public Dictionary<Setting, SavedValue> Values = new(); public int Writes; public int FailAt = -1; public bool AlwaysFail;
    public SavedValue Read(Setting s) => Values.GetValueOrDefault(s, new(false, 0, ""));
    public void Write(Setting s, SavedValue value) { Writes++; if (AlwaysFail || Writes == FailAt) throw new IOException("Injected failure"); Values[s] = value; }
}
