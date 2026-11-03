using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Profundidade da varredura de sobras, como no Revo: Seguro só pega o que é claramente do app.</summary>
public enum ScanMode { Seguro, Moderado, Avancado }

/// <summary>
/// Uma "sobra" encontrada depois de desinstalar: pasta, arquivo, atalho, chave de registro, entrada de inicialização
/// ou tarefa agendada ligada ao app. <see cref="Strong"/> = ligação clara (vem marcada); fraca = só bate com o fabricante.
/// </summary>
public sealed record Leftover(string Kind, string Display, long SizeBytes, string? FilePath,
                              RegistryHive Hive, RegistryView View, string? RegPath)
{
    public string? ValueName { get; init; }
    public string? TaskName { get; init; }
    public bool Strong { get; init; } = true;
    /// <summary>Ex.: "120 arquivos · 3 ocultos · 8 pastas".</summary>
    public string Detail { get; init; } = "";
    /// <summary>Por que foi encontrada (nome do app, pasta de instalação, fabricante…).</summary>
    public string Reason { get; init; } = "";

    public string Group => Kind switch
    {
        "Pasta" or "Arquivo" or "Atalho" => "Pastas e arquivos",
        "Registro" => "Registro",
        _ => "Inicialização e tarefas",
    };

    public string SizeText => SizeBytes <= 0 ? ""
        : SizeBytes >= 1L << 30 ? $"{SizeBytes / (double)(1L << 30):0.0} GB"
        : SizeBytes >= 1L << 20 ? $"{SizeBytes / (double)(1L << 20):0} MB"
        : $"{Math.Max(1, SizeBytes / 1024.0):0} KB";
}

/// <summary>Resultado de uma remoção de sobras.</summary>
public sealed record RemoveResult(int Ok, int Fail, int Recycled, string BackupDir);

/// <summary>
/// Desinstala um programa pelo desinstalador OFICIAL dele e, depois, faz uma varredura profunda (no estilo do Revo/Geek
/// Uninstaller) de sobras: pastas e subpastas (inclusive com arquivos ocultos), arquivos soltos, atalhos, chaves de
/// registro, entradas de inicialização, App Paths, a entrada que sobra em "Programas e Recursos" e tarefas agendadas.
/// <para>Guardas de segurança: só apaga dentro de Program Files / AppData / ProgramData / perfil do usuário (fora das
/// pastas pessoais) e de ramos Software\ do registro — NUNCA pastas do Windows nem chaves do sistema (Microsoft, Classes,
/// Policies, Windows), com exceção de formatos exatos (a própria entrada de desinstalação, App Paths de um .exe e valores
/// das chaves Run). Nada é apagado sem o usuário marcar e confirmar; pastas e arquivos vão para a Lixeira e registro e
/// tarefas ganham backup que o botão Restaurar devolve.</para>
/// </summary>
public static class Uninstaller
{
    /// <summary>Nome do próprio app (do .exe), para o ponto de restauração e para nunca listar as pastas dele.</summary>
    private static string AppName => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "PcismoMaster";

    // ---------------- desinstalação oficial ----------------

    public static async Task<bool> RunOfficialAsync(InstalledProgram p, CancellationToken ct)
    {
        var (exe, args) = ParseCommand(p.UninstallCmd);
        if (exe.Length == 0) return false;

        // msiexec: /I (modificar) vira /X (remover) para garantir a desinstalação.
        if (Path.GetFileNameWithoutExtension(exe).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
            args = Regex.Replace(args, @"(?<=^|\s)/I(?=\s*\{)", "/X", RegexOptions.IgnoreCase);

        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true }; // deixa a UI do desinstalador aparecer, herdando a elevação
        using var proc = Process.Start(psi);
        if (proc == null) return false;
        await proc.WaitForExitAsync(ct);
        return true;
    }

    /// <summary>
    /// Muitos desinstaladores (NSIS, Inno) se copiam para o Temp e o processo que abrimos fecha na hora. Espera a entrada
    /// sair de "Programas e Recursos" (ou o usuário clicar em procurar agora, ou o tempo acabar).
    /// </summary>
    public static async Task WaitUninstallFinishedAsync(InstalledProgram p, TimeSpan max, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < max && !ct.IsCancellationRequested && UninstallKeyExists(p))
        {
            try { await Task.Delay(1500, ct); } catch (OperationCanceledException) { break; }
        }
    }

    public static bool UninstallKeyExists(InstalledProgram p)
    {
        try
        {
            using var bk = RegistryKey.OpenBaseKey(p.Hive, p.View);
            using var k = bk.OpenSubKey(p.KeyPath);
            return k != null;
        }
        catch { return false; }
    }

    internal static (string Exe, string Args) ParseCommand(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.Length == 0) return ("", "");
        if (cmd[0] == '"')
        {
            int end = cmd.IndexOf('"', 1);
            if (end > 0) return (cmd.Substring(1, end - 1), cmd[(end + 1)..].Trim());
        }
        int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) { int cut = i + 4; return (cmd[..cut].Trim(), cmd[cut..].Trim()); }
        int sp = cmd.IndexOf(' ');
        return sp > 0 ? (cmd[..sp], cmd[(sp + 1)..].Trim()) : (cmd, "");
    }

    /// <summary>Ponto de restauração do Windows antes de desinstalar (como o Revo). Devolve a mensagem para a tela.</summary>
    public static async Task<string> TryRestorePointAsync(string appName)
    {
        try
        {
            string desc = (AppName + " - antes de desinstalar " + appName).Replace("'", "").Replace("\"", "");
            if (desc.Length > 60) desc = desc[..60];
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"Checkpoint-Computer -Description '{desc}' -RestorePointType APPLICATION_UNINSTALL -ErrorAction Stop\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using var proc = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode == 0 ? "Ponto de restauração criado." : "Ponto de restauração não criado (proteção do sistema desligada ou já criado hoje).";
        }
        catch { return "Ponto de restauração não criado."; }
    }

    // ---------------- reconhecimento do app pelo nome ----------------

    /// <summary>Diz o quanto um nome de pasta/chave/atalho bate com o app: 2 = claro, 1 = só o fabricante, 0 = nada.</summary>
    internal sealed class Matcher
    {
        public string Compact { get; private init; } = "";
        public List<string> StrongTokens { get; private init; } = new();
        public List<string> WeakTokens { get; private init; } = new();
        /// <summary>Pasta de instalação já normalizada (sem \ no fim), ou "" se não houver ou não for segura.</summary>
        public string InstallDir { get; private init; } = "";

        public static Matcher For(string name, string publisher, string installLocation)
        {
            string clean = CleanName(name);
            var nameTokens = Words(clean).Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToList();
            var pubTokens = Words(publisher).Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToList();
            var strong = nameTokens.Where(t => !pubTokens.Contains(t)).ToList();
            if (strong.Count == 0) strong = nameTokens; // nome = fabricante (Discord, Steam…)
            string dir = "";
            try
            {
                if (installLocation.Trim().Trim('"') is { Length: > 3 } loc && IsSafeFolder(loc))
                    dir = Path.GetFullPath(loc.Trim().Trim('"')).TrimEnd('\\');
            }
            catch { }
            return new Matcher
            {
                Compact = CompactOf(clean),
                StrongTokens = strong,
                WeakTokens = pubTokens.Where(t => !strong.Contains(t)).ToList(),
                InstallDir = dir,
            };
        }

        public bool IsEmpty => Compact.Length < 2 && StrongTokens.Count == 0;

        public int Level(string candidate)
        {
            string cc = CompactOf(candidate);
            if (cc.Length < 2 || IsEmpty) return 0;
            if (cc == Compact || (Compact.Length >= 4 && cc.StartsWith(Compact))) return 2;
            var words = Words(candidate).ToList();
            if (StrongTokens.Any(t => Hit(t, cc, words))) return 2;
            if (WeakTokens.Any(t => Hit(t, cc, words))) return 1;
            return 0;
        }

        /// <summary>Caminho dentro da pasta de instalação do app.</summary>
        public bool InInstallDir(string? path)
        {
            if (InstallDir.Length == 0 || string.IsNullOrWhiteSpace(path)) return false;
            string p = path.Trim().Trim('"');
            return p.StartsWith(InstallDir + "\\", StringComparison.OrdinalIgnoreCase) || p.Equals(InstallDir, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Hit(string token, string compact, List<string> words) =>
            token.Length >= 4 ? compact.Contains(token) : words.Contains(token) || compact == token;
    }

    /// <summary>Tira versão, arquitetura e idioma do nome ("Mozilla Firefox (x64 pt-BR) 120.0" → "Mozilla Firefox").</summary>
    internal static string CleanName(string name)
    {
        string s = Regex.Replace(name, @"\(.*?\)|\[.*?\]", " ");
        s = Regex.Replace(s, @"\bv?\d+(\.\d+)+\b|\b(x64|x86|amd64|arm64|64-bit|32-bit|64 bits|32 bits)\b", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '_');
    }

    internal static string CompactOf(string s) => new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static IEnumerable<string> Words(string s) =>
        Regex.Split(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length > 0 && !w.All(char.IsDigit));

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "software", "inc", "corp", "corporation", "ltd", "llc", "the", "com", "technologies", "technology",
        "systems", "group", "limited", "gmbh", "team", "project", "free", "windows", "microsoft", "for", "and",
        "update", "version", "edition", "application", "program", "install", "installer", "setup", "uninstall",
        "app", "apps", "client", "desktop", "launcher", "player", "studio", "reader", "tools", "tool", "driver",
        "drivers", "service", "services", "helper", "plugin", "runtime", "sdk", "pro", "plus", "lite", "portable",
        "beta", "games", "game", "media", "online", "data", "files", "common", "user", "system", "foundation",
        "community", "labs", "studios", "entertainment", "interactive", "open", "source", "x64", "x86", "bit",
    };

    // ---------------- varredura de sobras ----------------

    /// <summary>
    /// Varre o PC atrás de sobras do programa. <paramref name="includeUninstallEntry"/> = remoção forçada (o desinstalador
    /// oficial não rodou ou falhou): a entrada de "Programas e Recursos" entra na lista.
    /// </summary>
    public static List<Leftover> ScanLeftovers(InstalledProgram p, ScanMode mode = ScanMode.Moderado, bool includeUninstallEntry = false)
    {
        var m = Matcher.For(p.Name, p.Publisher, GuessInstallDir(p));
        var found = new List<Leftover>();
        if (m.IsEmpty) return found;

        // 1) A própria pasta de instalação, se o desinstalador deixou para trás.
        if (m.InstallDir.Length > 0 && Directory.Exists(m.InstallDir))
            found.Add(FolderItem(m.InstallDir, true, "pasta de instalação do app"));

        // 2) Pastas (e subpastas Fabricante\App) nos locais de programas e de dados do usuário.
        found.AddRange(ScanFolders(FolderRoots(mode), m, mode));

        // 3) Arquivos soltos com o nome do app (config/log direto em AppData, ProgramData e no perfil).
        if (mode >= ScanMode.Moderado) found.AddRange(ScanLooseFiles(LooseFileRoots(), m));

        // 4) Atalhos na Área de Trabalho e no Menu Iniciar que apontam para o app.
        if (mode >= ScanMode.Moderado) found.AddRange(ScanShortcuts(m, mode));

        // 5) Registro: Software\App e Software\Fabricante\App (usuário, máquina 64 e 32 bits).
        found.AddRange(ScanRegistry(m, mode));

        // 6) A entrada de desinstalação que ficou (ou remoção forçada).
        if ((includeUninstallEntry || UninstallKeyExists(p)) && IsSafeRegPath(p.KeyPath))
            found.Add(new Leftover("Registro", $"{HiveLabel(p.Hive, p.View)}\\{p.KeyPath}", 0, null, p.Hive, p.View, p.KeyPath)
                      { Reason = "entrada em Programas e Recursos" });

        if (mode >= ScanMode.Moderado)
        {
            // 7) App Paths (o "Executar" do Windows achar o .exe) e entradas de inicialização (Run/RunOnce).
            found.AddRange(ScanAppPaths(m));
            found.AddRange(ScanRunValues(m, mode));
            // 8) Tarefas agendadas que rodam o app (atualizadores, telemetria).
            found.AddRange(ScanTasks(m));
        }

        // Sem repetição e sem item dentro de outro já listado (a pasta pai já leva tudo).
        var folders = found.Where(f => f.Kind == "Pasta" && f.FilePath != null).Select(f => f.FilePath!.TrimEnd('\\') + "\\").ToList();
        return found
            .GroupBy(f => f.Kind + "|" + f.Display + "|" + f.ValueName, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.Strong).First())
            .Where(f => f.FilePath == null || !folders.Any(d => f.FilePath.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(f => f.Strong).ThenBy(f => f.Group).ThenBy(f => f.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pasta de instalação: a do registro; sem ela, a pasta do desinstalador ou do ícone, se tiver o nome do app
    /// (C:\Program Files\App\uninstall.exe → C:\Program Files\App).
    /// </summary>
    internal static string GuessInstallDir(InstalledProgram p)
    {
        if (p.InstallLocation.Trim().Trim('"').Length > 3) return p.InstallLocation;
        var m = Matcher.For(p.Name, p.Publisher, "");
        foreach (var spec in new[] { ParseCommand(p.UninstallCmd).Exe, p.DisplayIcon.Split(',')[0].Trim().Trim('"') })
        {
            try
            {
                string? dir = Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(spec));
                // uninstall.exe às vezes fica numa subpasta (App\uninst\unins000.exe)
                for (int up = 0; up < 2 && dir != null; up++, dir = Path.GetDirectoryName(dir))
                    if (m.Level(Path.GetFileName(dir)) == 2 && IsSafeFolder(dir)) return dir;
            }
            catch { }
        }
        return "";
    }

    /// <summary>Pastas filhas das raízes que batem com o app; numa pasta do fabricante, olha um nível para dentro.</summary>
    internal static List<Leftover> ScanFolders(IEnumerable<string> roots, Matcher m, ScanMode mode)
    {
        var found = new List<Leftover>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string[] dirs;
            try { if (!Directory.Exists(root)) continue; dirs = Directory.GetDirectories(root); }
            catch { continue; }
            foreach (var dir in dirs)
            {
                string nm = Path.GetFileName(dir);
                if (IsDeniedName(nm) || !IsSafeFolder(dir) || IsReparse(dir)) continue;
                int lvl = m.Level(nm);
                if (lvl == 2) { found.Add(FolderItem(dir, true, "nome do app")); continue; }
                if (lvl != 1) continue;

                // Pasta do fabricante (ex.: AppData\Roaming\Mozilla): procura o app dentro dela.
                string[] kids;
                try { kids = Directory.GetDirectories(dir); } catch { continue; }
                var appKids = kids.Where(k => !IsReparse(k) && m.Level(Path.GetFileName(k)) == 2 && IsSafeFolder(k)).ToList();
                bool onlyApp = appKids.Count > 0 && appKids.Count == kids.Length && SafeFileCount(dir) == 0;
                if (onlyApp) found.Add(FolderItem(dir, true, "pasta do fabricante só com o app"));
                else if (appKids.Count > 0) found.AddRange(appKids.Select(k => FolderItem(k, true, "nome do app (dentro da pasta do fabricante)")));
                else if (mode == ScanMode.Avancado) found.Add(FolderItem(dir, false, "só o nome do fabricante — confira antes"));
            }
        }
        return found;
    }

    private static List<Leftover> ScanLooseFiles(IEnumerable<string> roots, Matcher m)
    {
        var found = new List<Leftover>();
        foreach (var root in roots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var f in Directory.GetFiles(root))
                {
                    string nm = Path.GetFileNameWithoutExtension(f).TrimStart('.');
                    if (nm.Length < 3 || m.Level(nm) != 2 || !IsSafeFile(f)) continue;
                    var info = new FileInfo(f);
                    bool hidden = (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
                    found.Add(new Leftover("Arquivo", f, info.Length, f, default, default, null)
                              { Reason = "nome do app", Detail = hidden ? "arquivo oculto" : "" });
                }
            }
            catch { }
        }
        return found;
    }

    private static List<Leftover> ScanShortcuts(Matcher m, ScanMode mode)
    {
        var found = new List<Leftover>();
        var places = new List<(string Dir, bool Recurse)>
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), false),
            (Environment.GetFolderPath(Environment.SpecialFolder.Programs), true),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), true),
            (Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), false),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), false),
        };
        places.Add((Environment.GetFolderPath(Environment.SpecialFolder.Startup), false));
        places.Add((Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), false));
        dynamic? shell = null;
        try { if (Type.GetTypeFromProgID("WScript.Shell") is { } t) shell = Activator.CreateInstance(t); } catch { }
        try
        {
        foreach (var (dir, recurse) in places.Distinct())
        {
            IEnumerable<string> links;
            try
            {
                if (dir.Length == 0 || !Directory.Exists(dir)) continue;
                links = Directory.EnumerateFiles(dir, "*.lnk", new EnumerationOptions { RecurseSubdirectories = recurse, IgnoreInaccessible = true, AttributesToSkip = 0 }).ToList();
            }
            catch { continue; }
            foreach (var lnk in links)
            {
                bool nameHit = m.Level(Path.GetFileNameWithoutExtension(lnk)) == 2;
                if ((!nameHit && m.InstallDir.Length == 0) || !IsSafeFile(lnk)) continue; // só abre o atalho quando pode interessar
                string target = ShortcutTarget(shell, lnk);
                bool inDir = m.InInstallDir(target);
                bool dangling = target.Length > 0 && !File.Exists(target) && !Directory.Exists(target);
                if (inDir || (nameHit && dangling))
                    found.Add(new Leftover("Atalho", lnk, 0, lnk, default, default, null)
                              { Reason = inDir ? "aponta para a pasta do app" : "atalho quebrado com o nome do app" });
                else if (nameHit && mode == ScanMode.Avancado)
                    found.Add(new Leftover("Atalho", lnk, 0, lnk, default, default, null) { Strong = false, Reason = "nome parecido — confira o destino" });
            }
        }
        }
        finally { if (shell != null) try { Marshal.FinalReleaseComObject(shell); } catch { } }
        return found;
    }

    private static readonly (RegistryHive Hive, RegistryView View)[] RegViews =
    {
        (RegistryHive.CurrentUser, RegistryView.Default),
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
    };

    private static List<Leftover> ScanRegistry(Matcher m, ScanMode mode)
    {
        var found = new List<Leftover>();
        foreach (var (hive, view) in RegViews)
        {
            try
            {
                using var bk = RegistryKey.OpenBaseKey(hive, view);
                using var sw = bk.OpenSubKey("Software");
                if (sw == null) continue;
                foreach (var name in sw.GetSubKeyNames())
                {
                    if (IsDeniedName(name)) continue;
                    string path = "Software\\" + name;
                    int lvl = m.Level(name);
                    if (lvl == 2 && IsSafeRegPath(path)) { found.Add(RegItem(hive, view, path, true, "nome do app")); continue; }
                    if (lvl != 1) continue;

                    // Chave do fabricante: Software\Fabricante\App.
                    using var pub = sw.OpenSubKey(name);
                    if (pub == null) continue;
                    var kids = pub.GetSubKeyNames();
                    var appKids = kids.Where(k => m.Level(k) == 2).ToList();
                    if (appKids.Count > 0 && appKids.Count == kids.Length && pub.ValueCount == 0 && IsSafeRegPath(path))
                        found.Add(RegItem(hive, view, path, true, "chave do fabricante só com o app"));
                    else if (appKids.Count > 0)
                        found.AddRange(appKids.Select(k => path + "\\" + k).Where(IsSafeRegPath)
                                              .Select(k => RegItem(hive, view, k, true, "nome do app (dentro da chave do fabricante)")));
                    else if (mode == ScanMode.Avancado && IsSafeRegPath(path))
                        found.Add(RegItem(hive, view, path, false, "só o nome do fabricante — confira antes"));
                }
            }
            catch { }
        }
        return found;
    }

    private const string CurrentVersion = @"Software\Microsoft\Windows\CurrentVersion";

    private static List<Leftover> ScanAppPaths(Matcher m)
    {
        var found = new List<Leftover>();
        foreach (var (hive, view) in RegViews)
        {
            try
            {
                using var bk = RegistryKey.OpenBaseKey(hive, view);
                using var ap = bk.OpenSubKey(CurrentVersion + @"\App Paths");
                if (ap == null) continue;
                foreach (var exe in ap.GetSubKeyNames())
                {
                    using var k = ap.OpenSubKey(exe);
                    string target = Environment.ExpandEnvironmentVariables((k?.GetValue("") as string ?? "").Trim().Trim('"'));
                    bool inDir = m.InInstallDir(target);
                    bool dangling = target.Length > 0 && !File.Exists(target);
                    if (!inDir && !(dangling && m.Level(Path.GetFileNameWithoutExtension(exe)) == 2)) continue;
                    string path = CurrentVersion + @"\App Paths\" + exe;
                    if (IsSafeRegPath(path)) found.Add(RegItem(hive, view, path, true, inDir ? "App Paths do app" : "App Paths para um .exe que não existe mais"));
                }
            }
            catch { }
        }
        return found;
    }

    private static List<Leftover> ScanRunValues(Matcher m, ScanMode mode)
    {
        var found = new List<Leftover>();
        foreach (var (hive, view) in RegViews)
            foreach (var sub in new[] { "Run", "RunOnce" })
            {
                string path = CurrentVersion + "\\" + sub;
                try
                {
                    using var bk = RegistryKey.OpenBaseKey(hive, view);
                    using var k = bk.OpenSubKey(path);
                    if (k == null) continue;
                    foreach (var vn in k.GetValueNames())
                    {
                        if (vn.Length == 0) continue;
                        string data = k.GetValue(vn, "", RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
                        string exe = ParseCommand(Environment.ExpandEnvironmentVariables(data)).Exe;
                        bool inDir = m.InInstallDir(exe);
                        bool nameHit = m.Level(vn) == 2 || m.Level(Path.GetFileNameWithoutExtension(exe)) == 2;
                        bool dangling = exe.Length > 0 && !File.Exists(exe);
                        bool strong = inDir || (nameHit && dangling);
                        if (!strong && !(nameHit && mode == ScanMode.Avancado)) continue;
                        if (!IsSafeRegValue(path, vn)) continue;
                        found.Add(new Leftover("Inicialização", $"{HiveLabel(hive, view)}\\{path} → {vn}", 0, null, hive, view, path)
                        {
                            ValueName = vn, Strong = strong, Detail = data,
                            Reason = inDir ? "abre o app ao ligar o PC" : strong ? "abre um .exe que não existe mais" : "nome parecido — confira o comando",
                        });
                    }
                }
                catch { }
            }
        return found;
    }

    private static List<Leftover> ScanTasks(Matcher m)
    {
        var found = new List<Leftover>();
        string root = TasksRoot;
        try
        {
            if (!Directory.Exists(root)) return found;
            foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                string rel = file[root.Length..].Replace('/', '\\');
                if (!rel.StartsWith('\\')) rel = "\\" + rel;
                if (rel.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase)) continue; // tarefas do Windows: nunca
                string xml;
                try { xml = File.ReadAllText(file); } catch { continue; }
                var cmds = Regex.Matches(xml, @"<Command>(.*?)</Command>", RegexOptions.Singleline)
                                .Select(x => Environment.ExpandEnvironmentVariables(System.Net.WebUtility.HtmlDecode(x.Groups[1].Value).Trim().Trim('"'))).ToList();
                bool inDir = cmds.Any(m.InInstallDir);
                bool nameHit = m.Level(Path.GetFileName(file)) == 2;
                bool dangling = cmds.Count > 0 && cmds.All(c => c.Length > 0 && Path.IsPathRooted(c) && !File.Exists(c));
                if (!inDir && !(nameHit && (dangling || cmds.Any(c => m.Level(Path.GetFileNameWithoutExtension(c)) == 2)))) continue;
                found.Add(new Leftover("Tarefa", "Agendador de Tarefas: " + rel, 0, null, default, default, null)
                {
                    TaskName = rel, Detail = string.Join("  ", cmds),
                    Reason = inDir ? "roda um programa da pasta do app" : "tarefa com o nome do app",
                });
            }
        }
        catch { }
        return found;
    }

    private static string TasksRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks");

    private static Leftover FolderItem(string dir, bool strong, string reason)
    {
        var (files, hidden, dirs, size) = DirStats(dir);
        string detail = $"{files} arquivo(s)" + (hidden > 0 ? $" · {hidden} oculto(s)" : "") + (dirs > 0 ? $" · {dirs} subpasta(s)" : "");
        return new Leftover("Pasta", dir, size, dir, default, default, null) { Strong = strong, Reason = reason, Detail = detail };
    }

    private static Leftover RegItem(RegistryHive hive, RegistryView view, string path, bool strong, string reason)
    {
        int keys = 0, values = 0;
        try
        {
            using var bk = RegistryKey.OpenBaseKey(hive, view);
            using var k = bk.OpenSubKey(path);
            if (k != null) CountReg(k, ref keys, ref values, 0);
        }
        catch { }
        return new Leftover("Registro", $"{HiveLabel(hive, view)}\\{path}", 0, null, hive, view, path)
        {
            Strong = strong, Reason = reason, Detail = $"{values} valor(es)" + (keys > 0 ? $" · {keys} subchave(s)" : ""),
        };
    }

    private static void CountReg(RegistryKey k, ref int keys, ref int values, int depth)
    {
        values += k.ValueCount;
        if (depth > 12 || keys > 5000) return;
        foreach (var s in k.GetSubKeyNames())
        {
            keys++;
            try { using var c = k.OpenSubKey(s); if (c != null) CountReg(c, ref keys, ref values, depth + 1); } catch { }
        }
    }

    /// <summary>Conta arquivos (inclusive ocultos e de sistema), subpastas e tamanho, sem seguir junções.</summary>
    internal static (int Files, int Hidden, int Dirs, long Size) DirStats(string dir)
    {
        int files = 0, hidden = 0, dirs = 0; long size = 0;
        try
        {
            var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var fsi in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", opt))
            {
                if (fsi is FileInfo fi)
                {
                    files++; size += fi.Length;
                    if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) hidden++;
                }
                else dirs++;
                if (files + dirs > 300_000) break; // pasta gigante: o número fica aproximado
            }
        }
        catch { }
        return (files, hidden, dirs, size);
    }

    private static int SafeFileCount(string dir)
    {
        try { return Directory.GetFiles(dir).Length; } catch { return 1; }
    }

    private static bool IsReparse(string dir)
    {
        try { return (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0; } catch { return true; }
    }

    private static string ShortcutTarget(dynamic? shell, string lnk)
    {
        if (shell == null) return "";
        try
        {
            dynamic sc = shell.CreateShortcut(lnk);
            string target = sc.TargetPath ?? "";
            Marshal.FinalReleaseComObject(sc);
            return target;
        }
        catch { return ""; }
    }

    // ---------------- remoção (Lixeira + backup) e restauração ----------------

    public static string BackupsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcismoMaster", "UninstallBackups");

    private sealed record BackupEntry(string Kind, string File, string? Task, int View);

    /// <summary>
    /// Remove as sobras recebidas, revalidando as guardas em cada uma. Antes, guarda backup do registro (.reg) e das
    /// tarefas (.xml) numa pasta própria; pastas e arquivos vão para a Lixeira (<paramref name="recycle"/>). Atributos de
    /// somente leitura/oculto/sistema são tirados quando é preciso apagar direto.
    /// </summary>
    public static RemoveResult DeleteLeftovers(IEnumerable<Leftover> items, string appName, bool recycle = true, string? backupRoot = null)
    {
        var list = items.ToList();
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backup = Path.Combine(backupRoot ?? BackupsRoot, stamp + " " + SafeFileName(appName));
        var manifest = new List<BackupEntry>();
        int ok = 0, fail = 0, recycled = 0, n = 0;

        // Ordem: tarefas e inicialização → registro (mais fundo primeiro) → atalhos/arquivos → pastas.
        int Order(Leftover l) => l.Kind switch { "Tarefa" => 0, "Inicialização" => 1, "Registro" => 2, "Atalho" or "Arquivo" => 3, _ => 4 };
        foreach (var it in list.OrderBy(Order).ThenByDescending(l => (l.RegPath ?? l.FilePath ?? "").Length))
        {
            n++;
            try
            {
                switch (it.Kind)
                {
                    case "Pasta" when it.FilePath is { } dir && IsSafeFolder(dir) && Directory.Exists(dir):
                        // Na Lixeira não apagamos de vez por conta própria: se não deu (arquivo em uso, recusado), conta como falha.
                        if (!recycle) ForceDeleteDir(dir);
                        else if (Recycle(dir)) recycled++;
                        else throw new IOException("não foi para a Lixeira");
                        break;
                    case "Arquivo" or "Atalho" when it.FilePath is { } file && IsSafeFile(file) && File.Exists(file):
                        if (!recycle) { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
                        else if (Recycle(file)) recycled++;
                        else throw new IOException("não foi para a Lixeira");
                        break;
                    case "Registro" when it.RegPath is { } reg && IsSafeRegPath(reg):
                    {
                        Directory.CreateDirectory(backup);
                        string file = Path.Combine(backup, $"{n:000}-registro.reg");
                        if (!ExportKey(it.Hive, it.View, reg, file)) throw new IOException("backup do registro falhou");
                        manifest.Add(new BackupEntry("reg", Path.GetFileName(file), null, (int)it.View));
                        using var bk = RegistryKey.OpenBaseKey(it.Hive, it.View);
                        bk.DeleteSubKeyTree(reg, throwOnMissingSubKey: false);
                        break;
                    }
                    case "Inicialização" when it.RegPath is { } reg && it.ValueName is { } vn && IsSafeRegValue(reg, vn):
                    {
                        using var bk = RegistryKey.OpenBaseKey(it.Hive, it.View);
                        using var k = bk.OpenSubKey(reg, writable: true);
                        if (k == null || k.GetValue(vn) == null) break;
                        Directory.CreateDirectory(backup);
                        string file = Path.Combine(backup, $"{n:000}-inicializacao.reg");
                        File.WriteAllText(file, ValueRegFile(it.Hive, reg, vn, k.GetValueKind(vn), k.GetValue(vn, null, RegistryValueOptions.DoNotExpandEnvironmentNames)), Encoding.Unicode);
                        manifest.Add(new BackupEntry("reg", Path.GetFileName(file), null, (int)it.View));
                        k.DeleteValue(vn, throwOnMissingValue: false);
                        break;
                    }
                    case "Tarefa" when it.TaskName is { } task && !task.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase):
                    {
                        string src = TasksRoot + task;
                        Directory.CreateDirectory(backup);
                        string file = Path.Combine(backup, $"{n:000}-tarefa.xml");
                        File.Copy(src, file, true);
                        manifest.Add(new BackupEntry("task", Path.GetFileName(file), task, 0));
                        if (Run("schtasks.exe", $"/Delete /TN \"{task}\" /F") != 0) throw new IOException("schtasks falhou");
                        break;
                    }
                    default:
                        fail++; continue;
                }
                ok++;
            }
            catch { fail++; }
        }

        if (manifest.Count > 0)
            File.WriteAllText(Path.Combine(backup, "backup.json"), JsonSerializer.Serialize(new { App = appName, Date = DateTime.Now, Items = manifest }));
        return new RemoveResult(ok, fail, recycled, manifest.Count > 0 ? backup : "");
    }

    /// <summary>A sobra ainda existe? (para tirar da lista o que já foi removido)</summary>
    public static bool StillThere(Leftover l)
    {
        try
        {
            switch (l.Kind)
            {
                case "Pasta": return l.FilePath != null && Directory.Exists(l.FilePath);
                case "Arquivo" or "Atalho": return l.FilePath != null && File.Exists(l.FilePath);
                case "Tarefa": return l.TaskName != null && File.Exists(TasksRoot + l.TaskName);
                case "Registro" or "Inicialização":
                {
                    using var bk = RegistryKey.OpenBaseKey(l.Hive, l.View);
                    using var k = l.RegPath == null ? null : bk.OpenSubKey(l.RegPath);
                    return k != null && (l.ValueName == null || k.GetValue(l.ValueName) != null);
                }
                default: return false;
            }
        }
        catch { return true; }
    }

    /// <summary>Caminho do registro como o Regedit mostra (com WOW6432Node na visão de 32 bits).</summary>
    public static string RegeditPath(Leftover l)
    {
        string root = l.Hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        string path = l.RegPath ?? "";
        if (l.Hive == RegistryHive.LocalMachine && l.View == RegistryView.Registry32 && path.StartsWith("Software\\", StringComparison.OrdinalIgnoreCase))
            path = @"SOFTWARE\WOW6432Node\" + path[9..];
        return root + "\\" + path;
    }

    /// <summary>Backup mais recente de uma remoção (pasta com backup.json), ou null.</summary>
    public static string? LatestBackup()
    {
        try
        {
            if (!Directory.Exists(BackupsRoot)) return null;
            return Directory.GetDirectories(BackupsRoot).Where(d => File.Exists(Path.Combine(d, "backup.json")))
                            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal).FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Devolve o registro e as tarefas de um backup. Pastas e arquivos voltam pela Lixeira do Windows.</summary>
    public static (int Ok, int Fail) RestoreBackup(string backupDir)
    {
        int ok = 0, fail = 0;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(backupDir, "backup.json")));
        foreach (var e in doc.RootElement.GetProperty("Items").EnumerateArray())
        {
            string kind = e.GetProperty("Kind").GetString() ?? "";
            string file = Path.Combine(backupDir, e.GetProperty("File").GetString() ?? "");
            int code = kind switch
            {
                "reg" => Run("reg.exe", $"import \"{file}\" /reg:{((RegistryView)e.GetProperty("View").GetInt32() == RegistryView.Registry32 ? 32 : 64)}"),
                "task" => Run("schtasks.exe", $"/Create /TN \"{e.GetProperty("Task").GetString()}\" /XML \"{file}\" /F"),
                _ => -1,
            };
            if (code == 0) ok++; else fail++;
        }
        return (ok, fail);
    }

    private static bool ExportKey(RegistryHive hive, RegistryView view, string path, string file)
    {
        string root = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
        int bits = view == RegistryView.Registry32 ? 32 : 64;
        return Run("reg.exe", $"export \"{root}\\{path}\" \"{file}\" /y /reg:{bits}") == 0 && File.Exists(file);
    }

    /// <summary>Arquivo .reg com um valor só (para devolver uma entrada de inicialização).</summary>
    internal static string ValueRegFile(RegistryHive hive, string keyPath, string name, RegistryValueKind kind, object? value)
    {
        string root = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string Hex(byte[] b) => string.Join(",", b.Select(x => x.ToString("x2")));
        string data = kind switch
        {
            RegistryValueKind.String => $"\"{Esc(value?.ToString() ?? "")}\"",
            RegistryValueKind.ExpandString => "hex(2):" + Hex(Encoding.Unicode.GetBytes((value?.ToString() ?? "") + "\0")),
            RegistryValueKind.DWord => "dword:" + Convert.ToUInt32(Convert.ToInt64(value ?? 0) & 0xFFFFFFFF).ToString("x8"),
            RegistryValueKind.Binary => "hex:" + Hex(value as byte[] ?? Array.Empty<byte>()),
            _ => $"\"{Esc(value?.ToString() ?? "")}\"",
        };
        return $"Windows Registry Editor Version 5.00\r\n\r\n[{root}\\{keyPath}]\r\n\"{Esc(name)}\"={data}\r\n";
    }

    private static int Run(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe), args)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            if (!p.WaitForExit(30_000)) { try { p.Kill(); } catch { } return -1; }
            return p.ExitCode;
        }
        catch { return -1; }
    }

    /// <summary>Apaga direto tirando somente leitura/oculto/sistema de tudo dentro (é o que trava o Directory.Delete).</summary>
    internal static void ForceDeleteDir(string dir)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var fsi in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", opt))
            try { if ((fsi.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0) fsi.Attributes = FileAttributes.Normal; } catch { }
        try { new DirectoryInfo(dir).Attributes = FileAttributes.Directory; } catch { }
        Directory.Delete(dir, true);
    }

    /// <summary>Manda para a Lixeira (sem perguntas na tela). True se o item sumiu.</summary>
    private static bool Recycle(string path)
    {
        try
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path.TrimEnd('\\') + "\0\0",
                // WANTNUKEWARNING: se não couber na Lixeira, o Windows pergunta antes de apagar de vez
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_WANTNUKEWARNING,
            };
            int r = SHFileOperation(ref op);
            return r == 0 && !op.fAnyOperationsAborted && !File.Exists(path) && !Directory.Exists(path);
        }
        catch { return false; }
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400, FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    private static string SafeFileName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        string r = new string(s.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return r.Length > 40 ? r[..40] : r;
    }

    // ---------------- guardas de segurança ----------------

    private static string Folder(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
    private static string LocalLow => Path.Combine(Folder(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");

    private static IEnumerable<string> FolderRoots(ScanMode mode)
    {
        yield return Folder(Environment.SpecialFolder.ApplicationData);        // AppData\Roaming
        yield return Folder(Environment.SpecialFolder.LocalApplicationData);   // AppData\Local
        yield return Path.Combine(Folder(Environment.SpecialFolder.LocalApplicationData), "Programs");
        yield return LocalLow;
        yield return Folder(Environment.SpecialFolder.CommonApplicationData);  // ProgramData
        yield return Folder(Environment.SpecialFolder.ProgramFiles);
        yield return Folder(Environment.SpecialFolder.ProgramFilesX86);
        yield return Folder(Environment.SpecialFolder.CommonProgramFiles);
        yield return Folder(Environment.SpecialFolder.CommonProgramFilesX86);
        yield return Folder(Environment.SpecialFolder.Programs);               // Menu Iniciar (pastas de atalhos)
        yield return Folder(Environment.SpecialFolder.CommonPrograms);
        if (mode < ScanMode.Moderado) yield break;
        yield return Folder(Environment.SpecialFolder.MyDocuments);            // Documentos\App
        yield return Path.Combine(Folder(Environment.SpecialFolder.MyDocuments), "My Games");
        yield return Folder(Environment.SpecialFolder.UserProfile);            // C:\Users\voce\.app
        yield return Path.GetTempPath().TrimEnd('\\');
    }

    private static IEnumerable<string> LooseFileRoots()
    {
        yield return Folder(Environment.SpecialFolder.ApplicationData);
        yield return Folder(Environment.SpecialFolder.LocalApplicationData);
        yield return Folder(Environment.SpecialFolder.CommonApplicationData);
        yield return Folder(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Locais onde uma pasta (de pelo menos um nível abaixo) pode ser apagada.</summary>
    private static IEnumerable<string> AllowedRoots()
    {
        foreach (var r in FolderRoots(ScanMode.Avancado)) yield return r;
        yield return Folder(Environment.SpecialFolder.DesktopDirectory);
        yield return Folder(Environment.SpecialFolder.CommonDesktopDirectory);
        yield return Folder(Environment.SpecialFolder.StartMenu);
        yield return Folder(Environment.SpecialFolder.CommonStartMenu);
        yield return Folder(Environment.SpecialFolder.Startup);                // atalho de inicialização (só o arquivo)
        yield return Folder(Environment.SpecialFolder.CommonStartup);
    }

    /// <summary>Pastas pessoais e do sistema que nunca são apagadas, mesmo dentro de um local permitido.</summary>
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppData", "Desktop", "Documents", "Downloads", "Pictures", "Music", "Videos", "Favorites", "Links", "Contacts",
        "Saved Games", "Searches", "3D Objects", "OneDrive", "Local", "LocalLow", "Roaming", "Programs", "Temp",
        ".ssh", ".gnupg", "Start Menu", "Startup", "Inicializar", "My Games", "PcismoMaster", AppName,
        "Microsoft", "Windows", "WindowsApps", "ModifiableWindowsApps", "Common Files", "Packages", "Package Cache",
        "Windows Defender", "Windows NT", "WindowsPowerShell", "Internet Explorer", "Windows Mail", "Windows Media Player",
        "Windows Photo Viewer", "Windows Sidebar", "Microsoft.NET", "dotnet", "Reference Assemblies", "Administrative Tools",
        "System Tools", "Accessibility", "Accessories", "Maintenance",
    };

    /// <summary>Só é seguro apagar uma pasta DENTRO (ao menos um nível) de um dos locais permitidos, fora do Windows e das pastas pessoais.</summary>
    internal static bool IsSafeFolder(string path)
    {
        try
        {
            string full = Path.GetFullPath(path.Trim().Trim('"')).TrimEnd('\\');
            string win = Folder(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (full.Length <= 3) return false; // raiz de disco
            if (full.Equals(win, StringComparison.OrdinalIgnoreCase) || full.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
                return false;
            var roots = AllowedRoots().Select(r => r.TrimEnd('\\')).Where(r => r.Length > 3).ToList();
            if (roots.Any(r => full.Equals(r, StringComparison.OrdinalIgnoreCase))) return false; // um dos próprios locais, nunca

            // O local permitido mais fundo que contém a pasta (AppData\Local\Temp ganha de AppData\Local e do perfil).
            string? root = roots.Where(r => full.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase)).MaxBy(r => r.Length);
            if (root == null) return false;
            // Nada dentro de uma pasta pessoal ou do sistema (Documentos, Downloads, Microsoft, Common Files…)
            return !full[(root.Length + 1)..].Split('\\').Any(ProtectedNames.Contains);
        }
        catch { return false; }
    }

    /// <summary>Arquivo seguro: fica numa pasta segura ou é filho direto de um local permitido, e não é do sistema.</summary>
    internal static bool IsSafeFile(string path)
    {
        try
        {
            string full = Path.GetFullPath(path.Trim().Trim('"'));
            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext is ".sys" || Path.GetFileName(full).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
            string dir = Path.GetDirectoryName(full)?.TrimEnd('\\') ?? "";
            if (IsSafeFolder(dir)) return true;
            string win = Folder(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (dir.StartsWith(win, StringComparison.OrdinalIgnoreCase)) return false;
            return AllowedRoots().Any(r => r.Length > 3 && dir.Equals(r.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private static readonly Regex[] RegExceptions =
    {
        new(@"^software\\(wow6432node\\)?microsoft\\windows\\currentversion\\uninstall\\[^\\]+$", RegexOptions.IgnoreCase),
        new(@"^software\\(wow6432node\\)?microsoft\\windows\\currentversion\\app paths\\[^\\]+\.exe$", RegexOptions.IgnoreCase),
    };

    /// <summary>
    /// Só é seguro apagar chaves sob Software\, com profundidade mínima e fora dos ramos críticos do sistema — com exceção
    /// de formatos exatos: uma entrada de desinstalação e o App Paths de um .exe.
    /// </summary>
    internal static bool IsSafeRegPath(string sub)
    {
        string s = sub.Replace('/', '\\').Trim('\\');
        if (s.Contains("\\\\") || s.Contains("..")) return false;
        if (RegExceptions.Any(r => r.IsMatch(s))) return true;
        string lower = s.ToLowerInvariant();
        if (!lower.StartsWith("software\\")) return false;
        if (s.Split('\\').Length < 2) return false; // nunca a raiz "Software"
        string[] critical = { "software\\microsoft", "software\\classes", "software\\policies", "software\\windows",
                              "software\\wow6432node", "software\\clients", "software\\registeredapplications",
                              "software\\intel", "software\\nvidia corporation", "software\\amd", "software\\realtek" };
        if (lower == "software\\wow6432node") return false;
        if (lower.StartsWith("software\\wow6432node\\")) return IsSafeRegPath("Software\\" + s[("software\\wow6432node\\".Length)..]);
        return !critical.Any(c => lower == c || lower.StartsWith(c + "\\"));
    }

    /// <summary>Valores apagáveis: só nas chaves Run/RunOnce (entradas de inicialização).</summary>
    internal static bool IsSafeRegValue(string keyPath, string valueName) =>
        valueName.Length > 0 &&
        Regex.IsMatch(keyPath.Trim('\\'), @"^software\\(wow6432node\\)?microsoft\\windows\\currentversion\\(run|runonce)$", RegexOptions.IgnoreCase);

    private static readonly string[] Denied =
    {
        "windows", "microsoft", "common files", "windowsapps", "packages", "windowspowershell",
        "temp", "intel", "nvidia corporation", "realtek", "amd", "defender", "internetexplorer",
        "classes", "policies", "clients", "pcismomaster", "desktop.ini",
    };
    private static bool IsDeniedName(string name)
    {
        string n = name.ToLowerInvariant();
        return Denied.Any(d => n == d || n.Contains(d)) || n.Contains(AppName.ToLowerInvariant()) || ProtectedNames.Contains(name);
    }

    private static string HiveLabel(RegistryHive hive, RegistryView view) =>
        (hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM") + (view == RegistryView.Registry32 ? " (32 bits)" : "");
}
