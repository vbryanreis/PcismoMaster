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
Test("Tweaking: .reg parser reads every value type, deletes and continuation lines", () => {
    var r = RegFile.Parse("Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\T]\r\n\"S\"=\"a \\\"b\\\" c\\\\d\"\r\n\"D\"=dword:ffffffff\r\n" +
        "\"B\"=hex:01,02,\\\r\n  03\r\n\"E\"=hex(2):25,00,41,00,25,00,00,00\r\n\"M\"=hex(7):61,00,00,00,62,00,00,00,00,00\r\n\"Q\"=hex(b):01,00,00,00,00,00,00,00\r\n" +
        "\"X\"=-\r\n@=\"def\"\r\n\"D\"=dword:00000002\r\n[-HKEY_CLASSES_ROOT\\Folder\\shell\\x]\r\n[HKEY_USERS\\.DEFAULT\\Y]\r\n\"Z\"=dword:1\r\n");
    var v = r.Settings.ToDictionary(s => s.Name);
    Assert(v.Count == 8 && r.DeleteKeys.Single() == @"HKLM\SOFTWARE\Classes\Folder\shell\x" && r.Skipped == 2);
    Assert(v["S"].Kind == 1 && System.Text.Json.JsonSerializer.Deserialize<string>(v["S"].Data!) == "a \"b\" c\\d");
    Assert(v["D"].Kind == 4 && v["D"].Data == "2"); // o último valor vence
    Assert(v["B"].Kind == 3 && System.Text.Json.JsonSerializer.Deserialize<byte[]>(v["B"].Data!)!.SequenceEqual(new byte[] { 1, 2, 3 }));
    Assert(v["E"].Kind == 2 && v["E"].Data == "\"%A%\"" && v["M"].Kind == 7 && v["M"].Data == "[\"a\",\"b\"]" && v["Q"].Kind == 11 && v["Q"].Data == "1");
    Assert(v["X"].Remove && v[""].Kind == 1);
    var store = new FakeStore(); var eng = new ModuleEngine(store, Path.Combine(temp, "regfile"));
    var m = new Module("tw-test-reg", "t", "Tweaking", "", "", r.Settings);
    store.Values[v["X"]] = new(true, 4, "7");
    eng.Enable(m); Assert(eng.Matches(m) && !store.Read(v["X"]).Exists);
    eng.Restore(m); Assert(store.Read(v["X"]) == new SavedValue(true, 4, "7") && !store.Read(v["S"]).Exists);
});
Test("Otimizar: catálogo com ids únicos, valores sem conflito e sem desligar proteções", () => {
    var all = OptCatalog.All;
    Assert(all.Length > 140 && all.Select(t => t.Id).Distinct().Count() == all.Length);
    Assert(all.All(t => t.Settings.Length > 0 || t.Extras.Length > 0) && all.All(t => t.Title.Length > 0 && t.Description.Length > 0));
    Assert(all.All(t => (t.Page == OptPage.Debloat) == (t.Level != OptLevel.Normal)) && all.Where(t => t.Level == OptLevel.Avancado).All(t => !t.DefaultOn && t.Warning != null));
    Assert(all.All(t => OptCatalog.Categories.Contains(t.Category)));
    var sets = all.SelectMany(t => t.Settings.Select(s => (t, s))).ToArray();
    Assert(sets.All(x => x.s.Hive is "HKCU" or "HKLM" or "HKU" && x.s.Path.Length > 0 && !x.s.Path.StartsWith('\\')));
    // Um valor só pode estar em dois ajustes do mesmo grupo exclusivo (ex.: IPv6): desfazer fora de ordem não deixa valor errado.
    foreach (var g in sets.GroupBy(x => (x.s.Hive, x.s.Path.ToLowerInvariant(), x.s.Name.ToLowerInvariant())).Where(g => g.Select(x => x.t.Id).Distinct().Count() > 1))
        if (g.Select(x => x.t.Group).Distinct().Count() != 1 || g.First().t.Group == null) throw new Exception("valor repetido: " + g.Key);
    Assert(all.Count(t => t.Group == "ipv6") == 3);
    string[] forbidden = { "Windows Defender", "EnableLUA", "HypervisorEnforcedCodeIntegrity", "DeviceGuard", "SmartScreen", "Windows Firewall" };
    Assert(!sets.Any(x => forbidden.Any(f => x.s.Path.Contains(f, StringComparison.OrdinalIgnoreCase) || x.s.Name.Contains(f, StringComparison.OrdinalIgnoreCase))));
    Assert(OptCatalog.Profiles.All(p => p.Tweaks.All(id => OptCatalog.ById(id) != null)));
    foreach (var x in all.SelectMany(t => t.Extras).Where(x => x.Kind == ExtraKind.Power))
        Assert(Guid.TryParse(x.Args[0], out _) && Guid.TryParse(x.Args[1], out _) && int.TryParse(x.Args[2], out _));
    foreach (var x in all.SelectMany(t => t.Extras).Where(x => x.Kind == ExtraKind.DeleteKeys)) Assert(x.Args.All(k => k.StartsWith(@"HKLM\")));
    Assert(OptCatalog.SvcHostThresholdKb() >= 3_670_016);
});
Test("Otimizar: aplicar e desfazer voltam exatamente o valor de antes (registro, serviços, extras, grupo, monitor)", () => Task.Run(async () => {
    var store = new FakeStore(); var ps = new FakePs(); var disp = new FakeDisplay();
    var eng = new OptEngine(store, Path.Combine(temp, "opt"), ps, disp, svc => svc != "DiagTrack");
    var tel = OptCatalog.ById("telemetry")!;
    var failed = await eng.ApplyAsync(new[] { tel }, null);
    Assert(failed.Count == 0 && eng.Ours(tel) && store.Read(tel.Settings[0]) == new SavedValue(true, 4, "0") && ps.Scripts.Count == 0);
    Assert((await eng.StatesAsync(new[] { tel }))["telemetry"] == new OptState(true, true));
    await eng.UndoAsync(new[] { tel }, null);
    Assert(!eng.Ours(tel) && !store.Read(tel.Settings[0]).Exists);
    // Serviço que não existe neste PC: não cria a chave e aparece como "não se aplica".
    var dt = OptCatalog.ById("diagtrack")!;
    Assert(eng.SettingsFor(dt).Length == 0 && (await eng.StatesAsync(new[] { dt }))["diagtrack"].On == null);
    // Extra: lê o estado antes, grava o diário e desfaz com o valor lido.
    var hib = OptCatalog.ById("hibernation")!;
    ps.Read = "@@{\"hibernation#0\":\"1\"}";
    Assert((await eng.ApplyAsync(new[] { hib }, null)).Count == 0 && eng.Ours(hib) && ps.Scripts.Last().Contains("powercfg /hibernate off"));
    await eng.UndoAsync(new[] { hib }, null);
    Assert(!eng.Ours(hib) && ps.Scripts.Last().Contains("powercfg /hibernate on"));
    // Falha num bloco: o ajuste aparece como falho e o diário fica para tentar de novo.
    ps.Fail = "hibernation";
    Assert((await eng.ApplyAsync(new[] { hib }, null)).ContainsKey("hibernation") && eng.Ours(hib));
    ps.Fail = null;
    await eng.UndoAsync(new[] { hib }, null);
    Assert(!eng.Ours(hib));
    // Grupo exclusivo: ligar um desfaz o outro.
    var v4 = OptCatalog.ById("ipv4_prefer")!; var teredo = OptCatalog.ById("teredo_off")!;
    store.Values[v4.Settings[0]] = new(true, 4, "0");
    await eng.ApplyAsync(new[] { v4 }, null);
    await eng.ApplyAsync(new[] { teredo }, null);
    Assert(!eng.Ours(v4) && eng.Ours(teredo) && store.Read(teredo.Settings[0]) == new SavedValue(true, 4, "8"));
    await eng.UndoAsync(new[] { teredo }, null);
    Assert(store.Read(v4.Settings[0]) == new SavedValue(true, 4, "0"));
    // Taxa de atualização: vai ao máximo e volta.
    var hz = OptCatalog.ById("refresh_rate")!;
    Assert((await eng.StatesAsync(new[] { hz }))["refresh_rate"].On == false);
    await eng.ApplyAsync(new[] { hz }, null);
    Assert(disp.Hz[@"\\.\DISPLAY1"] == 144 && (await eng.StatesAsync(new[] { hz }))["refresh_rate"].On == true);
    await eng.UndoAsync(new[] { hz }, null);
    Assert(disp.Hz[@"\\.\DISPLAY1"] == 60);
    // Contas novas: só os valores de usuário dos ajustes aplicados, no perfil padrão carregado.
    var dark = OptCatalog.ById("dark_mode")!;
    await eng.ApplyAsync(new[] { dark }, null);
    var nu = eng.NewUserSettings();
    Assert(nu.Length == 2 && nu.All(s => s.Hive == "HKU" && s.Path.StartsWith(@"PM_Default\Software\Microsoft")));
}).GetAwaiter().GetResult());
Test("Otimizar: leitura dos extras entende cada estado e o desfazer só volta o que mudou", () => {
    var power = new OptExtra(ExtraKind.Power, "a", "b", "100");
    Assert(OptEngine.IsOn(power, "100,100") == true && OptEngine.IsOn(power, "100,0") == false && OptEngine.IsOn(power, "") == null && OptEngine.IsOn(power, "?") == null);
    var tasks = new OptExtra(ExtraKind.Tasks, @"\A\x", @"\A\y");
    Assert(OptEngine.IsOn(tasks, "Disabled|") == true && OptEngine.IsOn(tasks, "Ready|Disabled") == false && OptEngine.IsOn(tasks, "|") == null);
    Assert(OptEngine.IsOn(new OptExtra(ExtraKind.FeatureOff, "Recall"), "") == null && OptEngine.IsOn(new OptExtra(ExtraKind.FeatureOff, "Recall"), "Disabled") == true);
    Assert(OptEngine.IsOn(new OptExtra(ExtraKind.Capabilities, "x*"), "") == true && OptEngine.IsOn(new OptExtra(ExtraKind.Discord, "A", "true"), "-") == null);
    var eng = new OptEngine(new FakeStore(), Path.Combine(temp, "opt2"), new FakePs(), new FakeDisplay(), _ => true);
    var t = OptCatalog.ById("telemetry_tasks")!;
    string undo = eng.UndoScript(t, t.Extras[0], "Ready|Disabled||Ready|||")!;
    Assert(undo.Split("Enable-ScheduledTask").Length == 3 && !undo.Contains("Disable-"));
    var appx = OptCatalog.ById("ai_remove")!;
    Assert(eng.UndoScript(appx, appx.Extras[0], "Microsoft.Copilot") == null && eng.ApplyScript(appx, appx.Extras[0], 0, "")!.Contains("Remove-AppxProvisionedPackage"));
});
Test("Otimizar: todo PowerShell gerado tem sintaxe válida (só analisa, não roda)", () => Task.Run(async () => {
    string ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
    if (!File.Exists(ps)) return;
    var fake = new FakePs();
    var eng = new OptEngine(new FakeStore(), Path.Combine(temp, "opt3"), fake, new FakeDisplay(), _ => true);
    await eng.StatesAsync(OptCatalog.All);
    var scripts = new List<string>(fake.Scripts);
    if (Environment.GetEnvironmentVariable("PM_DUMP_READ") is { Length: > 0 } dump) File.WriteAllText(dump, fake.Scripts[0]); // diagnóstico manual (só leitura)
    foreach (var t in OptCatalog.All)
        for (int i = 0; i < t.Extras.Length; i++)
            foreach (var before in new[] { "", "1", "Enabled", "True", "100,0", "Ready|Ready|Ready|Ready|Ready|Ready|Ready", "a,b", "\"x\"" })
            {
                if (eng.ApplyScript(t, t.Extras[i], i, before) is { } a) scripts.Add(a);
                if (eng.UndoScript(t, t.Extras[i], before) is { } u) scripts.Add(u);
            }
    foreach (var p in Bloatware.Programs) if (Bloatware.ProgramReinstall(p.Id) is { } r) scripts.Add(r);
    string dir = Path.Combine(temp, "ps-parse"); Directory.CreateDirectory(dir);
    for (int i = 0; i < scripts.Count; i++) File.WriteAllText(Path.Combine(dir, i + ".ps1"), scripts[i]);
    var psi = new System.Diagnostics.ProcessStartInfo(ps, "-NoProfile -NonInteractive -Command \"Get-ChildItem '" + dir + "' -Filter *.ps1 | ForEach-Object { $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$e); foreach ($x in $e) { 'ERR ' + $_.Name + ': ' + $x.Message } }\"")
        { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
    using var proc = System.Diagnostics.Process.Start(psi)!;
    string output = proc.StandardOutput.ReadToEnd(); proc.WaitForExit();
    if (output.Contains("ERR ")) throw new Exception(output);
    Assert(scripts.Count > 50);
}).GetAwaiter().GetResult());
Test("Limpeza: respeita idade e máscara, expande perfis e não sai da pasta", () => {
    string root = Path.Combine(temp, "clean");
    Directory.CreateDirectory(Path.Combine(root, "P1", "Cache", "sub")); Directory.CreateDirectory(Path.Combine(root, "P2", "Cache")); Directory.CreateDirectory(Path.Combine(root, "P3"));
    string old = Path.Combine(root, "P1", "Cache", "sub", "old.bin");
    File.WriteAllText(old, "123456"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-5));
    File.WriteAllText(Path.Combine(root, "P1", "Cache", "new.bin"), "12"); File.WriteAllText(Path.Combine(root, "P2", "Cache", "a.db"), "1"); File.WriteAllText(Path.Combine(root, "P3", "keep.txt"), "k");
    Assert(Cleanup.Expand(Path.Combine(root, "*", "Cache")).Count() == 2);
    var cat = new CleanCategory("t", "t", "", true, new[] { Path.Combine(root, "*", "Cache") }, 2);
    Assert(Cleanup.Measure(cat) == (6, 1));
    var (freed, skipped) = Cleanup.Clean(cat);
    Assert(freed == 6 && skipped == 0 && !Directory.Exists(Path.Combine(root, "P1", "Cache", "sub")) && File.Exists(Path.Combine(root, "P1", "Cache", "new.bin")) && File.Exists(Path.Combine(root, "P3", "keep.txt")));
    Assert(Cleanup.Files(Path.Combine(root, "P2", "Cache"), 0, "*.db").Count() == 1 && !Cleanup.Files(Path.Combine(root, "P2", "Cache"), 0, "*.pf").Any());
    Assert(Cleanup.Categories.Select(c => c.Id).Distinct().Count() == Cleanup.Categories.Length && Cleanup.Size(1536) == "2 KB");
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
Test("ITAD: DRM filter goes in the deals filter and each deal reads its DRM list", () => {
    var p = QueryOf(ItadClient.BuildDealsQuery(new ItadDealsQuery { Drm = { 61, 16, ItadClient.DrmFree } }, "BR"));
    using var doc = System.Text.Json.JsonDocument.Parse(p["filter"]);
    Assert(doc.RootElement.GetProperty("drm").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(new[] { 61, 16, 1000 }));
    var d = ItadClient.Parse<ItadDeal>("""{"shop":{"id":6,"name":"Fanatical"},"price":{"amount":10,"currency":"BRL"},"regular":{"amount":20,"currency":"BRL"},"cut":50,"drm":[{"id":61,"name":"Steam"}],"url":"https://x"}""");
    Assert(d.Drm.Single().Id == 61 && d.Drm[0].Name == "Steam");
    Assert(ItadClient.DrmChoices.Any(c => c.Id == 61) && ItadClient.DrmChoices.Any(c => c.Id == ItadClient.DrmFree));
});
Test("ITAD: a deal passes the DRM filter only if it runs just on the checked DRMs", () => {
    ItadDeal Deal(int shop, double price, params int[] drm) => new() { Shop = new ItadShop { Id = shop, Name = "S" + shop }, Price = new ItadPrice { Amount = price },
        Drm = drm.Select(i => new ItadDrm { Id = i, Name = i == 61 ? "Steam" : i == 62 ? "Ubisoft Connect" : i == 1000 ? "Drm Free" : "D" + i }).ToList() };
    var steamOnly = new[] { 61 };
    Assert(ItadClient.RunsOn(Deal(61, 1, 61), steamOnly) && !ItadClient.RunsOn(Deal(16, 1, 16), steamOnly));
    Assert(!ItadClient.RunsOn(Deal(61, 1, 61, 62), steamOnly) && ItadClient.RunsOn(Deal(61, 1, 61, 62), new[] { 61, 62 })); // pede Ubisoft também
    Assert(!ItadClient.RunsOn(Deal(37, 1), steamOnly) && ItadClient.RunsOn(Deal(37, 1), Array.Empty<int>())); // sem DRM informado / sem filtro
    var prices = new ItadGamePrices { Deals = { Deal(16, 5, 16), Deal(6, 8, 61), Deal(61, 9, 61) } };
    Assert(ItadClient.BestDeal(prices, steamOnly)!.Shop.Id == 6 && ItadClient.BestDeal(prices, Array.Empty<int>())!.Shop.Id == 16);
    Assert(ItadClient.BestDeal(prices, new[] { 4 }) == null && ItadClient.BestDeal(null, steamOnly) == null);
    Assert(ItadClient.DrmNote(Deal(61, 1, 61)) == null && ItadClient.DrmNote(Deal(6, 1, 61)) == "ativa na Steam");
    Assert(ItadClient.DrmNote(Deal(35, 1, 1000)) == "sem DRM" && ItadClient.DrmNote(Deal(6, 1, 61, 62)) == "ativa na Steam + Ubisoft Connect" && ItadClient.DrmNote(Deal(37, 1)) == null);
});
Test("ITAD: the heart adds a favorite once and a second click removes it", () => {
    var favs = new List<DealFavorite>(); var now = new DateTime(2026, 11, 4);
    Assert(DealFavorite.Toggle(favs, "g1", "Hades", "hades", "https://img/h.jpg", now) && favs.Single().Title == "Hades" && favs[0].Added == now);
    Assert(DealFavorite.Toggle(favs, "g2", "Celeste", "celeste", null, now) && favs.Count == 2);
    Assert(!DealFavorite.Toggle(favs, "g1", "Hades", "hades", null, now) && favs.Single().Id == "g2");
    var back = System.Text.Json.JsonSerializer.Deserialize<List<DealFavorite>>(System.Text.Json.JsonSerializer.Serialize(favs))!;
    Assert(back.Single().Id == "g2" && back[0].Banner == null && back[0].Slug == "celeste");
});
Test("ITAD: Steam lookup maps appids and skips games it does not know", () => {
    var h = new FakeItadHandler { Reply = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"app/220":"g-hl2","app/1":null}""") } };
    var map = new ItadClient(h) { UserKey = "k" }.LookupSteamAsync(new[] { "220", "1" }).GetAwaiter().GetResult();
    Assert(map.Count == 1 && map["220"] == "g-hl2" && h.Requests[0].RequestUri!.AbsolutePath == "/lookup/id/shop/61/v1");
});
Test("Bloatware: each item gets a Portuguese name, a risk and the script's keep-list stays locked", () => {
    var json = """
        {"apps":[{"n":"Microsoft.BingWeather","f":"Microsoft.BingWeather_8wekyb3d8bbwe","fw":false,"nr":false,"s":"Store"},
        {"n":"Microsoft.Windows.Photos","f":"x","fw":false,"nr":false,"s":"Store"},{"n":"Microsoft.AAD.BrokerPlugin","f":"y","fw":false,"nr":false,"s":"System"},
        {"n":"Microsoft.VCLibs.140.00","f":"z","fw":true,"nr":false,"s":"Store"},{"n":"Contoso.SomeThing","f":"c","fw":false,"nr":false,"s":"Store"},
        {"n":"Microsoft.Windows.Search","f":"s","fw":false,"nr":true,"s":"System"}],
        "caps":["Language.Speech~~~pt-BR~0.0.1.0","WMIC~~~~","Browser.InternetExplorer~~~~0.0.11.0"],
        "feats":["Printing-PrintToPDFServices-Features","NetFx4-AdvSrvs","Microsoft-Hyper-V-All"],"progs":["onedrive","gameinput"]}
        """;
    var items = Bloatware.Parse(json);
    BloatItem Get(string id) => items.Single(i => i.Id == id);
    Assert(Get("Microsoft.BingWeather").Name == "Clima" && Get("Microsoft.BingWeather").Risk == ScriptRisk.Seguro && Get("Microsoft.BingWeather").Essential == null);
    Assert(Get("Microsoft.Windows.Photos").Essential != null && Get("Microsoft.Windows.Search").Essential != null);
    Assert(Get("Microsoft.AAD.BrokerPlugin").Risk == ScriptRisk.Perigoso && Get("Microsoft.VCLibs.140.00").Kind == BloatKind.Biblioteca);
    Assert(Get("Contoso.SomeThing").Name == "Contoso Some Thing" && Get("Contoso.SomeThing").Risk == ScriptRisk.Atencao && Bloatware.Pretty("Microsoft.WindowsFeedbackHub") == "Windows Feedback Hub");
    Assert(Bloatware.App("Claude", "c", false, false, "Developer").Risk == ScriptRisk.Atencao && Bloatware.App("1527c705-839a-4832-9118-54d4Bd6a0c89", "g", false, false, "System").Risk == ScriptRisk.Perigoso);
    Assert(Bloatware.App("MicrosoftWindows.61869720.Voiess", "v", false, false, "System").Name.Contains("IA") && Bloatware.App("Microsoft.BingNews", "b", false, false, "Store").Risk == ScriptRisk.Seguro);
    Assert(Get("Language.Speech~~~pt-BR~0.0.1.0").Name.Contains("voz (pt-BR)") && Get("Language.Speech~~~pt-BR~0.0.1.0").Risk == ScriptRisk.Atencao);
    Assert(Get("WMIC~~~~").Essential != null && Get("NetFx4-AdvSrvs").Essential != null && Get("Microsoft-Hyper-V-All").Risk == ScriptRisk.Atencao);
    Assert(items.Count(i => i.Kind == BloatKind.Programa) == 2 && items.First().Kind == BloatKind.App);
});
Test("Bloatware: commands quote ids, run one item at a time and only restore what has a way back", () => {
    var weird = Bloatware.App("Evil'; Remove-Item C:\\ -Recurse; '", "f", false, false, "Store");
    var cmd = Bloatware.RemoveCommand(weird);
    Assert(cmd.Contains("-Name 'Evil''; Remove-Item C:\\ -Recurse; '''") && cmd.Contains("Remove-AppxPackage -AllUsers"));
    var cap = Bloatware.Capability("Browser.InternetExplorer~~~~0.0.11.0");
    var feat = Bloatware.Feature("SMB1Protocol");
    Assert(Bloatware.RestoreCommand(cap)!.StartsWith("Add-WindowsCapability") && Bloatware.RestoreCommand(feat)!.Contains("Enable-WindowsOptionalFeature") && Bloatware.RestoreCommand(weird) == null);
    var batch = Bloatware.BatchScript(new[] { weird, cap, feat }, restore: false);
    Assert(batch.Split("try {").Length == 4 && batch.Contains("Removendo: Internet Explorer") && !batch.Contains("Read-Host"));
    foreach (var p in Bloatware.Programs) Assert(Bloatware.RemoveCommand(new BloatItem(BloatKind.Programa, p.Id, p.Name, "", p.Risk, null)).Length > 20);
    Assert(Bloatware.Programs.All(p => (Bloatware.ProgramReinstall(p.Id) != null) == p.CanReinstall));
    Assert(!Bloatware.ListScript.Contains("Remove-") && !Bloatware.ListScript.Contains("Disable-"));
});
Test("Friendly UI: all-caps button text becomes a sentence and keeps acronyms", () => {
    Assert(FriendlyText.Apply("REMOVER OS MARCADOS") == "Remover os marcados");
    Assert(FriendlyText.Apply("MOSTRAR OS 12 SCRIPTS") == "Mostrar os 12 scripts");
    Assert(FriendlyText.Apply("ATUALIZAR DRIVER NVIDIA") == "Atualizar driver NVIDIA");
    Assert(FriendlyText.Apply("DESINSTALAR APPS (NO APP)") == "Desinstalar apps (no app)");
    Assert(FriendlyText.Apply("REINSTALAR PELA STORE") == "Reinstalar pela Store" && FriendlyText.Apply("RODAR E REINICIAR") == "Rodar e reiniciar");
    Assert(FriendlyText.Apply("OK") == "OK" && FriendlyText.Apply("GPU") == "GPU" && FriendlyText.Apply("Já escrito") == "Já escrito" && FriendlyText.Apply("1 MIN") == "1 min");
    Assert(FriendlyText.Apply("LIMPAR O PC") == "Limpar o PC" && FriendlyText.Apply("ÚLTIMA VERSÃO") == "Última versão");
});
Test("Friendly UI: home tasks and search lead to real pages, and simple mode hides the technical ones", () => {
    string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml"));
    foreach (var p in PageCatalog.Pages) Assert(xaml.Contains($"x:Name=\"{p.Nav}\""));
    Assert(PageCatalog.Pages.Select(p => p.Nav).Distinct().Count() == PageCatalog.Pages.Length);
    foreach (var t in PageCatalog.Tasks) Assert(PageCatalog.ByNav(t.Nav) is { Advanced: false });
    Assert(PageCatalog.Search("bloatware", false).First().Nav == "NavOptDebloat");
    Assert(PageCatalog.Search("manutencao", false).Any(p => p.Nav == "NavMaint") && PageCatalog.Search("MANUTENÇÃO", false).Any(p => p.Nav == "NavMaint"));
    Assert(PageCatalog.Search("overclock", false).Count == 0 && PageCatalog.Search("overclock", true).First().Nav == "NavOc");
    Assert(PageCatalog.Search("  ", true).Count == 0 && PageCatalog.Search("driver", false).First().Nav == "NavDriver");
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

sealed class FakePs : IPsRunner
{
    public List<string> Scripts = new(); public string Read = "@@{}"; public string? Fail;
    public Task<string> RunAsync(string script, IProgress<string>? log, CancellationToken ct)
    {
        Scripts.Add(script);
        if (script.Contains("$r = @{}")) return Task.FromResult(Read);
        return Task.FromResult(Fail != null && script.Contains("@@FAIL " + Fail) ? $"@@FAIL {Fail} injetado" : "");
    }
}

sealed class FakeDisplay : IDisplayRefresh
{
    public Dictionary<string, int> Hz = new() { [@"\\.\DISPLAY1"] = 60 };
    public Dictionary<string, (int Current, int Max)> Read() => Hz.ToDictionary(kv => kv.Key, kv => (kv.Value, 144));
    public bool Set(string device, int hz) { Hz[device] = hz; return true; }
}
