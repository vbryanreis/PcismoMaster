using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// TWEAKING · Windows, Hardware e Avançado: as pastas do Ultimate (MIT) em cartões. Os ajustes de registro passam pelo
/// ModuleEngine (backup do valor anterior e desfazer); o que não é registro (plano de energia, ligações de rede, DEP,
/// MMAgent, perfil NVIDIA) tem botão de desfazer próprio. Nada é aplicado ao abrir as páginas.
/// </summary>
public partial class MainWindow
{
    /// <summary>Um cartão de interruptores com APLICAR / MARCAR RECOMENDADOS / DESFAZER TUDO.</summary>
    private sealed class TweakSection
    {
        public required StackPanel Rows;
        public required TextBlock Status;
        public required Button Apply;
        public required Func<SetupTweak[]> Build;
        public SetupTweak[] Items = Array.Empty<SetupTweak>();
        public readonly Dictionary<string, CheckBox> Toggles = new();
        public bool Busy;
    }

    private bool _tweakWinInit, _tweakHwInit, _tweakAdvInit;

    private static string TweakBackupDir => Path.GetDirectoryName(TweakBackupFile("x"))!;

    // ---------------- Montagem de cartões ----------------

    private Border TweakCard(Panel host, string title, string? intro, out StackPanel body)
    {
        body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Label"), Margin = new Thickness(0, 0, 0, 6) });
        if (intro != null)
            body.Children.Add(new TextBlock { Text = intro, Foreground = (Brush)FindResource("Faint"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var card = new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 22), Child = body };
        host.Children.Add(card);
        return card;
    }

    private Button TweakButton(string text, bool primary, RoutedEventHandler click, string? tag = null)
    {
        var b = new Button { Content = text, Style = (Style)FindResource(primary ? "PrimaryButton" : "OutlineButton"), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 8), Tag = tag };
        b.Click += click;
        return b;
    }

    private TextBlock TweakStatusText(string text = "") =>
        new() { Text = text, Foreground = (Brush)FindResource("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    private TweakSection AddTweakSection(Panel host, string title, string intro, Func<SetupTweak[]> build)
    {
        TweakCard(host, title, intro, out var body);
        var rows = new StackPanel();
        rows.Children.Add(new TextBlock { Text = "Lendo o que existe neste PC…", Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 10) });
        body.Children.Add(rows);
        var status = TweakStatusText();
        TweakSection sec = null!;
        var apply = TweakButton("APLICAR", true, (_, _) => _ = ApplySectionAsync(sec));
        apply.IsEnabled = false;
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(apply);
        buttons.Children.Add(TweakButton("MARCAR RECOMENDADOS", false, (_, _) => { foreach (var t in sec.Items) sec.Toggles[t.Id].IsChecked = t.DefaultOn; }));
        buttons.Children.Add(TweakButton("DESFAZER TUDO", false, (_, _) => { foreach (var c in sec.Toggles.Values) c.IsChecked = false; _ = ApplySectionAsync(sec); }));
        body.Children.Add(buttons);
        body.Children.Add(status);
        sec = new TweakSection { Rows = rows, Status = status, Apply = apply, Build = build };
        _ = LoadSectionAsync(sec);
        return sec;
    }

    private async Task LoadSectionAsync(TweakSection sec)
    {
        try { sec.Items = await Task.Run(sec.Build); }
        catch (Exception ex) { sec.Items = Array.Empty<SetupTweak>(); sec.Status.Text = "Não deu para ler: " + ex.Message; }
        // Some o que não existe neste PC (ex.: ULPS sem placa AMD), a menos que já tenha sido aplicado.
        sec.Items = sec.Items.Where(t => t.Settings.Length > 0 || t.DeleteKeys is { Length: > 0 } || File.Exists(TweakBackupFile(t.Id))).ToArray();
        sec.Rows.Children.Clear();
        sec.Toggles.Clear();
        foreach (var t in sec.Items)
            sec.Toggles[t.Id] = ToggleRow(t.Title, t.Advanced, t.Description, File.Exists(TweakBackupFile(t.Id)), t.DefaultOn, sec.Rows);
        if (sec.Items.Length == 0) sec.Rows.Children.Add(new TextBlock { Text = "Nada desta parte se aplica a este PC.", Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 10) });
        sec.Apply.IsEnabled = sec.Items.Length > 0;
    }

    private async Task ApplySectionAsync(TweakSection sec)
    {
        if (sec.Busy || !RequireAdmin()) return;
        var turnOn = sec.Items.Where(t => sec.Toggles[t.Id].IsChecked == true && sec.Toggles[t.Id].Tag is false).ToList();
        var turnOff = sec.Items.Where(t => sec.Toggles[t.Id].IsChecked != true && sec.Toggles[t.Id].Tag is true).ToList();
        if (turnOn.Count + turnOff.Count == 0) { sec.Status.Text = "Nada mudou: os marcados já estão aplicados."; return; }
        var risky = turnOn.Where(t => t.Advanced).Select(t => "• " + t.Title).ToList();
        string msg = $"Aplicar {turnOn.Count} ajuste(s) e desfazer {turnOff.Count}? O valor anterior de cada um é guardado."
                     + (risky.Count > 0 ? "\n\nAVANÇADOS marcados (leia a descrição de cada um):\n" + string.Join("\n", risky) : "");
        if (MessageBox.Show(this, msg, "Tweaking", MessageBoxButton.OKCancel, risky.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question) != MessageBoxResult.OK) return;

        sec.Busy = true;
        sec.Apply.IsEnabled = false;
        sec.Status.Text = "Aplicando…";
        var errors = await Task.Run(() => RunTweaks(turnOn, turnOff));
        sec.Busy = false;
        await LoadSectionAsync(sec);
        sec.Status.Text = (errors.Count == 0
            ? $"Pronto: {turnOn.Count} aplicado(s)" + (turnOff.Count > 0 ? $", {turnOff.Count} desfeito(s)." : ".")
            : $"Concluído com {errors.Count} erro(s): " + string.Join(" · ", errors) + ".")
            + " Alguns só valem depois de sair da conta ou reiniciar o PC.";
    }

    /// <summary>Desfaz e aplica (fora da thread da janela). Devolve os erros por ajuste; um erro não impede os outros.</summary>
    private static List<string> RunTweaks(IEnumerable<SetupTweak> turnOn, IEnumerable<SetupTweak> turnOff)
    {
        var errors = new List<string>();
        foreach (var t in turnOff) Try(t, () =>
        {
            RestoreDeletedKeys(t.Id);
            GraphicsTweaks.Engine.Restore(TweakModule(t));
            TweakAfter(t.Id);
        });
        foreach (var t in turnOn) Try(t, () =>
        {
            TweakBefore(t.Id);
            var m = TweakModule(t);
            if (m.Settings.Length == 0) throw new InvalidOperationException("nada deste ajuste neste PC");
            GraphicsTweaks.Engine.Enable(m);
            if (t.DeleteKeys is { Length: > 0 }) DeleteKeysWithBackup(t);
            TweakAfter(t.Id);
        });
        return errors;
        void Try(SetupTweak t, Action a) { try { a(); } catch (Exception ex) { errors.Add($"{t.Title}: {ex.Message}"); } }
    }

    // ---------------- Chaves inteiras apagadas (menu de contexto, .reg com [-chave]) ----------------

    private static string KeysDir(string id) => Path.Combine(TweakBackupDir, id + ".keys");
    private static string RegExe => Path.Combine(Environment.SystemDirectory, "reg.exe");

    private static bool RegKeyExists(string full)
    {
        int i = full.IndexOf('\\');
        var hive = full[..i] == "HKCU" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var k = root.OpenSubKey(full[(i + 1)..]);
        return k != null;
    }

    private static void RunReg(string args)
    {
        using var p = Process.Start(new ProcessStartInfo(RegExe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })!;
        string err = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException("reg.exe: " + (err.Trim().Length > 0 ? err.Trim().Split('\n')[0] : "código " + p.ExitCode));
    }

    /// <summary>Exporta cada chave que existe (reg export) e só então apaga; o desfazer importa de volta.</summary>
    private static void DeleteKeysWithBackup(SetupTweak t)
    {
        string dir = KeysDir(t.Id);
        Directory.CreateDirectory(dir);
        int n = Directory.GetFiles(dir, "*.reg").Length;
        foreach (var key in t.DeleteKeys!)
        {
            if (!RegKeyExists(key)) continue;
            string file = Path.Combine(dir, $"{n++:D3}.reg");
            RunReg($"export \"{key}\" \"{file}\" /y");
            RunReg($"delete \"{key}\" /f");
        }
    }

    private static void RestoreDeletedKeys(string id)
    {
        string dir = KeysDir(id);
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.reg").OrderBy(f => f)) RunReg($"import \"{file}\"");
        Directory.Delete(dir, true);
    }

    // ---------------- Passos extras de alguns ajustes ----------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, string? value, uint flags);

    private static void TweakBefore(string id)
    {
        if (id == "tw-win-lockscreen-black" && !File.Exists(TweakCatalog.BlackImage))
        {
            // Imagem preta pequena (o Windows estica para a tela).
            var bmp = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgr24, null, new byte[64 * 64 * 3], 64 * 3);
            var enc = new JpegBitmapEncoder { QualityLevel = 90 };
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(TweakCatalog.BlackImage);
            enc.Save(fs);
        }
    }

    private static void TweakAfter(string id)
    {
        if (id == "tw-win-lockscreen-black")
        {
            // Recarrega o papel de parede gravado (o preto ou o anterior, no desfazer).
            using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (k?.GetValue("Wallpaper") is string wp) SystemParametersInfo(0x14, 0, wp, 0x01 | 0x02);
        }
    }

    // ================= WINDOWS =================

    private void InitTweakWin()
    {
        if (_tweakWinInit) return;
        _tweakWinInit = true;
        var host = TweakWinHost;

        TweakCard(host, "PONTO DE RESTAURAÇÃO", "Recomendado antes de aplicar muita coisa: se algo der errado, dá para voltar o Windows inteiro.", out var rp);
        var rpStatus = TweakStatusText();
        var rpWrap = new WrapPanel();
        rpWrap.Children.Add(TweakButton("CRIAR PONTO DE RESTAURAÇÃO", true, async (s, _) =>
        {
            if (!RequireAdmin()) return;
            var b = (Button)s; b.IsEnabled = false;
            string? why = await Maintenance.CreateRestorePoint(new Maintenance.Ctx(new Progress<string>(t => rpStatus.Text = t), CancellationToken.None), "Tweaking");
            rpStatus.Text = why == null ? "Ponto de restauração criado." : "Não foi criado: " + why;
            b.IsEnabled = true;
        }));
        rp.Children.Add(rpWrap);
        rp.Children.Add(rpStatus);

        AddTweakSection(host, "AJUSTES DO WINDOWS",
            "Marque o que quer e clique em APLICAR; o valor anterior de cada um fica guardado e desmarcar + APLICAR devolve. " +
            "Os AVANÇADOS vêm desligados. Tema, barra e menu valem depois de sair da conta (ou reiniciar o Explorador).",
            BuildWindowsTweaks);

        BuildPowerPlanCard(host);
        BuildIpv4Card(host);
        BuildCleanupCard(host);

        TweakCard(host, "ATALHOS DO WINDOWS", "O que o script só abre para você conferir e ajustar à mão.", out var sc);
        var wrap = new WrapPanel();
        foreach (var (text, tag) in new[]
        {
            ("MODO DE JOGO", "ms-settings:gaming-gamemode"), ("PRECISÃO DO PONTEIRO", "main.cpl|,2"), ("ESCALA E VÍDEO", "ms-settings:display"),
            ("VÍDEO AVANÇADO", "ms-settings:display-advanced"), ("GRÁFICOS", "ms-settings:display-advancedgraphics"),
            ("SOM", "mmsys.cpl"), ("ISOLAMENTO DE NÚCLEO", "windowsdefender://coreisolation"), ("INFORMAÇÕES DO SISTEMA", "msinfo32.exe"),
            ("APPS INSTALADOS", "ms-settings:appsfeatures"), ("PROGRAMAS E RECURSOS", "appwiz.cpl"), ("RECURSOS DO WINDOWS", "optionalfeatures.exe"),
            ("RECURSOS OPCIONAIS", "ms-settings:optionalfeatures"), ("GERENCIADOR DE TAREFAS", "taskmgr.exe"), ("PERSONALIZAR INICIAR", "ms-settings:personalization-start"),
        })
            wrap.Children.Add(TweakButton(text, false, BtnTweakOpen_Click, tag));
        wrap.Children.Add(TweakButton("DESINSTALAR APPS (NO APP)", false, (_, _) => NavUninstall.IsChecked = true));
        wrap.Children.Add(TweakButton("INICIALIZAÇÃO (NO APP)", false, (_, _) => NavStartup.IsChecked = true));
        sc.Children.Add(wrap);

        // Bloatware item a item; depois os scripts inteiros (Bloatware completo, Edge/WebView, Defender, Autoruns…)
        AddBloatSection(host);
        AddScriptSection(host, "win");
    }

    private static SetupTweak[] BuildWindowsTweaks()
    {
        var list = new List<SetupTweak>();
        // A escala da tela (o script põe 100%) fica só no cartão de escala da sub-aba Hardware: não muda a escala de quem usa outra.
        var reg = TweakCatalog.WindowsRegTweaks(s => !TweakCatalog.IsDisplayScale(s) && RegistryStore.CanWrite(s)).ToList();
        list.Add(reg.First(t => t.Id == "tw-win-control-panel"));
        list.AddRange(TweakCatalog.Windows.Where(t => !t.Advanced));
        list.AddRange(reg.Where(t => t.Id != "tw-win-control-panel"));
        list.Add(TweakDevices.NetworkPower());
        list.Add(TweakDevices.DevicePower());
        list.Add(TweakDevices.WriteCache());
        list.AddRange(TweakCatalog.Windows.Where(t => t.Advanced));
        // O que a sub-aba Iniciar já cuida fica só lá.
        var iniciar = SetupTweaks.Static.SelectMany(t => t.Settings);
        return TweakCatalog.Dedupe(list, iniciar);
    }

    // ---------------- Plano de energia ----------------

    private static string PowerCfg => Path.Combine(Environment.SystemDirectory, "powercfg.exe");
    private static string PowerPlanBackup => Path.Combine(TweakBackupDir, "tw-power-plan.txt");

    private static (int Code, string Output) RunPowerCfg(string args)
    {
        using var p = Process.Start(new ProcessStartInfo(PowerCfg, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, o.Trim());
    }

    private static string ActiveScheme()
    {
        var o = RunPowerCfg("/getactivescheme").Output;
        var m = System.Text.RegularExpressions.Regex.Match(o, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return m.Success ? m.Value.ToLowerInvariant() : "";
    }

    private void BuildPowerPlanCard(Panel host)
    {
        TweakCard(host, "PLANO DE ENERGIA ULTIMATE",
            "Cria uma cópia do Desempenho Máximo com os valores do script (processador sempre a 100%, sem suspender/hibernar, sem economia " +
            "no USB, PCI Express e Wi-Fi, tela desliga em 10 min) e ativa. Os seus outros planos não são apagados; desfazer volta o plano anterior.",
            out var body);
        var status = TweakStatusText(File.Exists(PowerPlanBackup) ? "Agora: plano Ultimate do PcismoMaster aplicado." : "");
        var wrap = new WrapPanel();
        Button apply = null!, undo = null!;
        apply = TweakButton("CRIAR E ATIVAR", true, async (_, _) =>
        {
            if (!RequireAdmin()) return;
            if (MessageBox.Show(this, "Criar e ativar o plano de energia Ultimate? O plano ativo agora fica guardado para o desfazer.", "Plano de energia",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            apply.IsEnabled = undo.IsEnabled = false;
            status.Text = "Aplicando…";
            status.Text = await Task.Run(ApplyPowerPlan);
            apply.IsEnabled = undo.IsEnabled = true;
        });
        undo = TweakButton("DESFAZER", false, async (_, _) =>
        {
            if (!RequireAdmin()) return;
            apply.IsEnabled = undo.IsEnabled = false;
            status.Text = await Task.Run(UndoPowerPlan);
            apply.IsEnabled = undo.IsEnabled = true;
        });
        wrap.Children.Add(apply);
        wrap.Children.Add(undo);
        wrap.Children.Add(TweakButton("OPÇÕES DE ENERGIA", false, BtnTweakOpen_Click, "powercfg.cpl"));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    private static string ApplyPowerPlan()
    {
        string guid = TweakCatalog.PowerPlanGuid;
        string before = ActiveScheme();
        bool existed = RunPowerCfg("/query " + guid).Code == 0;
        if (!existed && RunPowerCfg($"/duplicatescheme {TweakCatalog.UltimatePerformance} {guid}") is { Code: not 0 } dup)
            return "Não deu para criar o plano: " + dup.Output;
        if (!File.Exists(PowerPlanBackup))
        {
            Directory.CreateDirectory(TweakBackupDir);
            File.WriteAllText(PowerPlanBackup, before + "|" + (existed ? "existia" : "criado"));
        }
        RunPowerCfg($"/changename {guid} \"Ultimate (PcismoMaster)\"");
        int skipped = 0;
        foreach (var (sub, setting, value) in TweakCatalog.PowerPlan)
        {
            if (RunPowerCfg($"/setacvalueindex {guid} {sub} {setting} {value}").Code != 0) skipped++;
            RunPowerCfg($"/setdcvalueindex {guid} {sub} {setting} {value}");
        }
        var act = RunPowerCfg("/setactive " + guid);
        return act.Code != 0 ? "Plano criado, mas não ativou: " + act.Output
            : "Pronto: plano Ultimate ativo." + (skipped > 0 ? $" {skipped} configuração(ões) não existem neste PC e ficaram de fora." : "");
    }

    private static string UndoPowerPlan()
    {
        if (!File.Exists(PowerPlanBackup)) return "Não há o que desfazer: o plano não foi aplicado pelo PcismoMaster.";
        var parts = File.ReadAllText(PowerPlanBackup).Split('|');
        string before = parts[0];
        if (before.Length > 0 && before != TweakCatalog.PowerPlanGuid) RunPowerCfg("/setactive " + before);
        else RunPowerCfg("/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Equilibrado
        if (parts.ElementAtOrDefault(1) == "criado") RunPowerCfg("/delete " + TweakCatalog.PowerPlanGuid);
        File.Delete(PowerPlanBackup);
        return "Desfeito: o plano anterior voltou a ser o ativo.";
    }

    // ---------------- IPv4 apenas ----------------

    private static readonly string[] Ipv4OffBindings = { "ms_lldp", "ms_lltdio", "ms_implat", "ms_rspndr", "ms_tcpip6", "ms_server", "ms_msclient", "ms_pacer" };

    private void BuildIpv4Card(Panel host)
    {
        TweakCard(host, "REDE SÓ COM IPV4", "Desliga nas placas de rede tudo menos o IPv4: IPv6, descoberta de rede (LLDP/LLTD), QoS, " +
            "compartilhamento de arquivos e impressoras (cliente e servidor SMB). Deixa de ver e compartilhar pastas com outros PCs da rede. " +
            "AVANÇADO: se algum site ou jogo parar de conectar, clique em RELIGAR TUDO.", out var body);
        var status = TweakStatusText();
        var wrap = new WrapPanel();
        foreach (var (text, primary, on) in new[] { ("SÓ IPV4", true, false), ("RELIGAR TUDO", false, true) })
            wrap.Children.Add(TweakButton(text, primary, async (s, _) =>
            {
                if (!RequireAdmin()) return;
                if (!on && MessageBox.Show(this, "Desligar IPv6, descoberta de rede, QoS e compartilhamento de arquivos em todas as placas de rede?",
                        "Rede só com IPv4", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                var b = (Button)s; b.IsEnabled = false;
                var ids = on ? Ipv4OffBindings.Append("ms_tcpip") : Ipv4OffBindings;
                string cmd = string.Join("; ", ids.Select(id => $"{(on ? "Enable" : "Disable")}-NetAdapterBinding -Name '*' -ComponentID {id} -ErrorAction SilentlyContinue"));
                try { await PowerShellAsync("$ProgressPreference='SilentlyContinue'; " + cmd); status.Text = on ? "Pronto: tudo religado." : "Pronto: só IPv4 nas placas de rede."; }
                catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
                b.IsEnabled = true;
            }));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    // ---------------- Limpeza ----------------

    private void BuildCleanupCard(Panel host)
    {
        TweakCard(host, "LIMPEZA", "Apaga arquivos temporários (seus e do Windows), o cache de downloads do Windows Update, o DumpStack.log e a pasta PerfLogs, " +
            "e abre a Limpeza de Disco. Arquivos em uso são pulados. Pastas com coisas suas (Windows.old, XboxGames, inetpub) não são apagadas.", out var body);
        var status = TweakStatusText();
        var wrap = new WrapPanel();
        wrap.Children.Add(TweakButton("LIMPAR", true, async (s, _) =>
        {
            if (!RequireAdmin()) return;
            if (MessageBox.Show(this, "Apagar os arquivos temporários e o cache do Windows Update?", "Limpeza", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            var b = (Button)s; b.IsEnabled = false;
            status.Text = "Limpando…";
            long freed = await Task.Run(CleanTemp);
            status.Text = $"Pronto: {freed / 1048576.0:0} MB liberados.";
            try { Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true }); } catch { }
            b.IsEnabled = true;
        }));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    private static long CleanTemp()
    {
        string sysDrive = Path.GetPathRoot(Environment.SystemDirectory)!;
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        long freed = 0;
        foreach (var dir in new[] { Path.GetTempPath(), Path.Combine(win, "Temp"), Path.Combine(win, "SoftwareDistribution", "Download") })
            freed += EmptyFolder(dir);
        foreach (var file in new[] { Path.Combine(sysDrive, "DumpStack.log"), Path.Combine(sysDrive, "DumpStack.log.tmp") })
            try { var fi = new FileInfo(file); if (fi.Exists) { long l = fi.Length; fi.Delete(); freed += l; } } catch { }
        string perf = Path.Combine(sysDrive, "PerfLogs");
        freed += EmptyFolder(perf);
        return freed;
    }

    /// <summary>Apaga o conteúdo da pasta (não a pasta), pulando o que estiver em uso. Não segue links/junções.</summary>
    private static long EmptyFolder(string dir)
    {
        long freed = 0;
        if (!Directory.Exists(dir)) return 0;
        var root = new DirectoryInfo(dir);
        foreach (var f in Safe(() => root.GetFiles()))
            try { long l = f.Length; f.Attributes = FileAttributes.Normal; f.Delete(); freed += l; } catch { }
        foreach (var d in Safe(() => root.GetDirectories()))
        {
            if (d.Attributes.HasFlag(FileAttributes.ReparsePoint)) { try { d.Delete(); } catch { } continue; }
            freed += EmptyFolder(d.FullName);
            try { d.Delete(); } catch { }
        }
        return freed;
        static T[] Safe<T>(Func<T[]> f) { try { return f(); } catch { return Array.Empty<T>(); } }
    }

    // ================= HARDWARE =================

    private void InitTweakHw()
    {
        if (_tweakHwInit) return;
        _tweakHwInit = true;
        var host = TweakHwHost;

        AddTweakSection(host, "MOUSE", "Marque e clique em APLICAR; desmarcar + APLICAR devolve o valor anterior.", () => TweakCatalog.Hardware);
        BuildScalingCard(host);

        TweakCard(host, "TESTES E GUIAS", "Abrem no navegador.", out var links);
        var wrap = new WrapPanel();
        foreach (var (text, url) in new[]
        {
            ("TESTE DO MONITOR (UFO)", "https://www.testufo.com/framerates#count=6&background=none&pps=1920"),
            ("TESTE DE BUFFERBLOAT DA REDE", "https://www.waveform.com/tools/bufferbloat"),
            ("TESTE DE POLLING DO CONTROLE", "https://gamepadla.com/"),
            ("GUIA DE PEÇAS (PCPARTPICKER)", "https://pcpartpicker.com/user/fr33thy/saved"),
        })
            wrap.Children.Add(TweakButton(text, false, BtnTweakOpen_Click, url));
        wrap.Children.Add(TweakButton("MONITOR NAS CONFIGURAÇÕES", false, BtnTweakOpen_Click, "ms-settings:display-advanced"));
        links.Children.Add(wrap);

        TweakCard(host, "DICAS", null, out var tips);
        tips.Children.Add(new TextBlock
        {
            Foreground = (Brush)FindResource("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Text = "Mouse: use 1000 Hz ou mais no software do mouse e ajuste a sensibilidade pelo DPI, deixando o Windows em 6/11 sem aceleração. " +
                   "Monitor: escolha a maior taxa de atualização em Vídeo avançado, ligue o overdrive no menu do monitor no nível sem rastro " +
                   "(confira no teste UFO) e use cabo DisplayPort. Rede: se o teste de bufferbloat der nota baixa, ligue o SQM/QoS no roteador. " +
                   "Controle: o overclock de polling (hidusbf) está nos scripts completos abaixo.",
        });

        AddScriptSection(host, "hw");
    }

    private void BuildScalingCard(Panel host)
    {
        TweakCard(host, "ESCALA MAIOR SEM ACELERAÇÃO",
            "Para quem usa a escala do Windows acima de 100%: grava a escala e uma curva de mouse (MarkC) que mantém o movimento 1:1, sem aceleração, " +
            "nessa escala. Vale depois de sair da conta. Desfazer volta a escala e o mouse de antes.", out var body);
        var status = TweakStatusText();
        var combo = new ComboBox { Width = 120, Margin = new Thickness(0, 0, 10, 8), VerticalAlignment = VerticalAlignment.Top };
        foreach (var pct in TweakCatalog.Scaling.Keys) combo.Items.Add(pct + "%");
        combo.SelectedIndex = 0;
        var wrap = new WrapPanel();
        wrap.Children.Add(combo);
        void Refresh()
        {
            var m = File.Exists(TweakBackupFile("tw-hw-scaling")) ? TweakModule(ScalingTweak(100)) : null;
            int? applied = m?.Settings.FirstOrDefault(s => s.Name == "LogPixels")?.Value * 100 / 96;
            status.Text = applied is int a ? $"Agora: escala de {a}% aplicada pelo PcismoMaster." : "Nada aplicado.";
            if (applied is int a2 && TweakCatalog.Scaling.Keys.ToList().IndexOf(a2) is int i and >= 0) combo.SelectedIndex = i;
        }
        wrap.Children.Add(TweakButton("APLICAR", true, async (s, _) =>
        {
            if (!RequireAdmin()) return;
            int pct = TweakCatalog.Scaling.Keys.ElementAt(combo.SelectedIndex);
            if (MessageBox.Show(this, $"Aplicar a escala de {pct}% sem aceleração do mouse? Vale depois de sair da conta.", "Escala",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            var b = (Button)s; b.IsEnabled = false;
            var errors = await Task.Run(() =>
            {
                var off = File.Exists(TweakBackupFile("tw-hw-scaling")) ? new[] { ScalingTweak(pct) } : Array.Empty<SetupTweak>();
                var e1 = RunTweaks(Array.Empty<SetupTweak>(), off);
                return e1.Count > 0 ? e1 : RunTweaks(new[] { ScalingTweak(pct) }, Array.Empty<SetupTweak>());
            });
            Refresh();
            status.Text += errors.Count > 0 ? " Erro: " + string.Join(" · ", errors) : " Saia da conta e entre de novo para valer.";
            b.IsEnabled = true;
        }));
        wrap.Children.Add(TweakButton("DESFAZER", false, async (s, _) =>
        {
            if (!RequireAdmin() || !File.Exists(TweakBackupFile("tw-hw-scaling"))) { status.Text = "Nada aplicado."; return; }
            var b = (Button)s; b.IsEnabled = false;
            var errors = await Task.Run(() => RunTweaks(Array.Empty<SetupTweak>(), new[] { ScalingTweak(100) }));
            Refresh();
            status.Text += errors.Count > 0 ? " Erro: " + string.Join(" · ", errors) : " Desfeito; saia da conta e entre de novo.";
            b.IsEnabled = true;
        }));
        body.Children.Add(wrap);
        body.Children.Add(status);
        Refresh();
    }

    private static SetupTweak ScalingTweak(int pct) => new("tw-hw-scaling", $"Escala {pct}% sem aceleração", "", false, false,
        "Hardware/1 Scaling Higher No Accel.ps1", TweakCatalog.Scaling[pct]);

    // ================= AVANÇADO =================

    private void InitTweakAdv()
    {
        if (_tweakAdvInit) return;
        _tweakAdvInit = true;
        var host = TweakAdvHost;

        AddTweakSection(host, "AJUSTES AVANÇADOS",
            "Todos vêm desligados. Cada um troca segurança, compatibilidade ou estabilidade por desempenho: leia a descrição antes. " +
            "Desmarcar + APLICAR devolve o valor anterior. A maioria vale depois de reiniciar.",
            () => TweakCatalog.Dedupe(TweakCatalog.Advanced.Append(TweakDevices.AmdUlps()),
                SetupTweaks.Static.Concat(TweakCatalog.Windows).SelectMany(t => t.Settings)));

        BuildRebarCard(host);
        BuildMmAgentCard(host);
        BuildDepCard(host);
        BuildPerGameCard(host);

        // Defender desligado, serviços em massa, Iniciar/Pesquisa/Shell e os outros scripts inteiros.
        AddScriptSection(host, "adv");
    }

    private void BuildRebarCard(Panel host)
    {
        TweakCard(host, "RESIZABLE BAR FORÇADO (NVIDIA)",
            "Liga o Resizable BAR para todos os jogos no perfil global da NVIDIA (o driver só liga nos jogos da lista dele). Precisa do " +
            "ReBAR ligado na BIOS. Em alguns jogos pode diminuir o FPS: teste e desfaça se piorar.", out var body);
        var status = TweakStatusText(GraphicsTweaks.DrsApplied("tw-adv-rebar") ? "Agora: forçado pelo PcismoMaster." : "");
        var wrap = new WrapPanel();
        wrap.Children.Add(TweakButton("FORÇAR", true, async (s, _) =>
        {
            if (!RequireAdmin()) return;
            var b = (Button)s; b.IsEnabled = false;
            try
            {
                if (await Task.Run(GpuDetect.DetectNvidia) == null) { status.Text = "Nenhuma placa NVIDIA encontrada."; return; }
                await Task.Run(() => GraphicsTweaks.ApplyDrs("tw-adv-rebar", new[] { (TweakCatalog.RebarEnable, 1u, "rBAR - Enable") }));
                status.Text = "Pronto: Resizable BAR forçado. Reinicie os jogos abertos.";
            }
            catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
            finally { b.IsEnabled = true; }
        }));
        wrap.Children.Add(TweakButton("DESFAZER", false, async (s, _) =>
        {
            if (!GraphicsTweaks.DrsApplied("tw-adv-rebar")) { status.Text = "Nada aplicado."; return; }
            if (!RequireAdmin()) return;
            var b = (Button)s; b.IsEnabled = false;
            try { await Task.Run(() => GraphicsTweaks.RestoreDrs("tw-adv-rebar")); status.Text = "Desfeito."; }
            catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
            finally { b.IsEnabled = true; }
        }));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    private void BuildMmAgentCard(Panel host)
    {
        TweakCard(host, "RECURSOS DE MEMÓRIA (MMAGENT)",
            "Desliga o Prefetch/SysMain de apps (pré-carregar programas), o pré-lançamento de apps, a combinação de páginas e a API de operações. " +
            "Programas podem demorar um pouco mais para abrir pela primeira vez. PADRÃO DO WINDOWS religa (a compressão de memória fica na sub-aba Iniciar).",
            out var body);
        var status = TweakStatusText();
        var wrap = new WrapPanel();
        foreach (var (text, primary, off) in new[] { ("DESLIGAR", true, true), ("PADRÃO DO WINDOWS", false, false) })
            wrap.Children.Add(TweakButton(text, primary, async (s, _) =>
            {
                if (!RequireAdmin()) return;
                if (MessageBox.Show(this, (off ? "Desligar" : "Religar") + " os recursos de memória do Windows? Vale depois de reiniciar.", "MMAgent",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                var b = (Button)s; b.IsEnabled = false;
                string v = off ? "Disable" : "Enable";
                string cmd = $"Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters' -Name EnablePrefetcher -Value {(off ? 0 : 3)} -Type DWord; " +
                             $"{v}-MMAgent -ApplicationLaunchPrefetching -ErrorAction SilentlyContinue; {v}-MMAgent -ApplicationPreLaunch -ErrorAction SilentlyContinue; " +
                             $"Set-MMAgent -MaxOperationAPIFiles {(off ? 1 : 512)} -ErrorAction SilentlyContinue; {v}-MMAgent -OperationAPI -ErrorAction SilentlyContinue; " +
                             $"{v}-MMAgent -PageCombining -ErrorAction SilentlyContinue";
                try { await PowerShellAsync(cmd); status.Text = (off ? "Desligados." : "Padrão do Windows.") + " Reinicie o PC para valer."; }
                catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
                b.IsEnabled = true;
            }));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    private void BuildDepCard(Panel host)
    {
        TweakCard(host, "PREVENÇÃO DE EXECUÇÃO DE DADOS (DEP)",
            "Desliga a DEP no boot (bcdedit nx AlwaysOff). Alguns jogos antigos ou com anti-cheat antigo precisam. Deixa o PC mais exposto a " +
            "ataques de memória e alguns anti-cheats modernos (Vanguard, FACEIT) não abrem sem ela. Vale depois de reiniciar.", out var body);
        var status = TweakStatusText();
        var wrap = new WrapPanel();
        foreach (var (text, primary, off) in new[] { ("DESLIGAR DEP", true, true), ("PADRÃO DO WINDOWS", false, false) })
            wrap.Children.Add(TweakButton(text, primary, async (s, _) =>
            {
                if (!RequireAdmin()) return;
                if (off && MessageBox.Show(this, "Desligar a DEP? Alguns anti-cheats deixam de abrir e o PC fica mais exposto. Dá para voltar aqui.",
                        "DEP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                var b = (Button)s; b.IsEnabled = false;
                var r = await Task.Run(() =>
                {
                    using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "bcdedit.exe"), off ? "/set nx AlwaysOff" : "/deletevalue nx")
                        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
                    string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return (p.ExitCode, o.Trim());
                });
                status.Text = r.ExitCode == 0 || !off ? (off ? "DEP desligada." : "Padrão do Windows (DEP ligada).") + " Reinicie o PC para valer." : "Não deu: " + r.Item2;
                b.IsEnabled = true;
            }));
        body.Children.Add(wrap);
        body.Children.Add(status);
    }

    // ---------------- Por jogo (teste) ----------------

    private sealed record ProcItem(string Text, int Id) { public override string ToString() => Text; }

    private void BuildPerGameCard(Panel host)
    {
        TweakCard(host, "POR JOGO (TESTE)",
            "Muda só o processo escolhido e só enquanto ele está aberto (nada fica gravado): prioridade, sem SMT/Hyper-Threading " +
            "(só um thread por núcleo) ou sem o núcleo 0 (onde o Windows roda mais coisa). Abra o jogo, clique em ATUALIZAR e escolha. " +
            "Teste com e sem: pode piorar. Tempo real pode travar o PC.", out var body);
        var status = TweakStatusText();
        var combo = new ComboBox { Width = 320, Margin = new Thickness(0, 0, 10, 8) };
        var prio = new ComboBox { Width = 160, Margin = new Thickness(0, 0, 10, 8) };
        foreach (var p in new[] { "Alta", "Acima do normal", "Normal", "Tempo real" }) prio.Items.Add(p);
        prio.SelectedIndex = 0;
        void Fill()
        {
            combo.Items.Clear();
            foreach (var p in Process.GetProcesses().Where(p => { try { return p.WorkingSet64 > 300L << 20 && p.Id != Environment.ProcessId; } catch { return false; } })
                         .OrderByDescending(p => p.WorkingSet64))
                combo.Items.Add(new ProcItem($"{p.ProcessName} ({p.WorkingSet64 >> 20} MB, PID {p.Id})", p.Id));
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            status.Text = combo.Items.Count == 0 ? "Nenhum programa grande aberto." : "";
        }
        Process? Selected() { try { return combo.SelectedItem is ProcItem t ? Process.GetProcessById(t.Id) : null; } catch { return null; } }
        void Do(Func<Process, string> a)
        {
            if (!RequireAdmin()) return;
            using var p = Selected();
            if (p == null) { status.Text = "Escolha um processo aberto (ATUALIZAR)."; return; }
            try { status.Text = a(p); } catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
        }
        int n = Environment.ProcessorCount;
        long all = n >= 64 ? -1L : (1L << n) - 1;
        var row1 = new WrapPanel();
        row1.Children.Add(combo);
        row1.Children.Add(TweakButton("ATUALIZAR", false, (_, _) => Fill()));
        var row2 = new WrapPanel();
        row2.Children.Add(prio);
        row2.Children.Add(TweakButton("MUDAR PRIORIDADE", true, (_, _) => Do(p =>
        {
            p.PriorityClass = prio.SelectedIndex switch { 0 => ProcessPriorityClass.High, 1 => ProcessPriorityClass.AboveNormal, 3 => ProcessPriorityClass.RealTime, _ => ProcessPriorityClass.Normal };
            return $"{p.ProcessName}: prioridade {prio.SelectedItem}.";
        })));
        row2.Children.Add(TweakButton("SEM SMT/HT", false, (_, _) => Do(p =>
        {
            long mask = 0;
            for (int i = 0; i < Math.Min(n, 63); i += 2) mask |= 1L << i;
            p.ProcessorAffinity = (IntPtr)mask;
            return $"{p.ProcessName}: só um thread por núcleo ({n / 2} de {n}).";
        })));
        row2.Children.Add(TweakButton("SEM NÚCLEO 0", false, (_, _) => Do(p =>
        {
            if (n < 4) return "Este processador tem poucos núcleos para isso.";
            p.ProcessorAffinity = (IntPtr)(all & ~3L);
            return $"{p.ProcessName}: sem o núcleo 0 (threads 0 e 1).";
        })));
        row2.Children.Add(TweakButton("VOLTAR AO NORMAL", false, (_, _) => Do(p =>
        {
            p.ProcessorAffinity = (IntPtr)all;
            p.PriorityClass = ProcessPriorityClass.Normal;
            return $"{p.ProcessName}: todos os núcleos e prioridade normal.";
        })));
        // "Startup" dos scripts Priority / Smt Ht / Core 1 Thread 1: abre o jogo já com a prioridade e os núcleos escolhidos.
        var noSmt = new CheckBox { Content = "Sem SMT/HT", Margin = new Thickness(0, 0, 14, 8), VerticalAlignment = VerticalAlignment.Center };
        var noCore0 = new CheckBox { Content = "Sem núcleo 0", Margin = new Thickness(0, 0, 14, 8), VerticalAlignment = VerticalAlignment.Center };
        var closeLaunchers = new CheckBox { Content = "Fechar launchers antes (Steam, Epic, Battle.net…)", Margin = new Thickness(0, 0, 14, 8), VerticalAlignment = VerticalAlignment.Center };
        var row3 = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        row3.Children.Add(noSmt);
        row3.Children.Add(noCore0);
        row3.Children.Add(closeLaunchers);
        row3.Children.Add(TweakButton("ABRIR UM JOGO ASSIM…", false, (_, _) =>
        {
            if (!RequireAdmin()) return;
            var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe", Title = "Escolha o .exe do jogo ou do launcher" };
            if (dlg.ShowDialog(this) != true) return;
            if (closeLaunchers.IsChecked == true)
            {
                if (MessageBox.Show(this, "Fechar à força Steam, Epic, EA, Battle.net, GOG, Riot, Ubisoft, Roblox e Escape from Tarkov (o que estiver aberto)? Downloads em andamento param.",
                        "Por jogo", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                foreach (var name in new[] { "Battle.net", "BsgLauncher", "EADesktop", "EpicGamesLauncher", "GalaxyClient", "RobloxPlayerBeta", "RiotClientServices", "Launcher", "steam", "upc" })
                    foreach (var lp in Process.GetProcessesByName(name)) { try { lp.Kill(); } catch { } finally { lp.Dispose(); } }
            }
            try
            {
                using var p = Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(dlg.FileName)! })!;
                long mask = all;
                if (noSmt.IsChecked == true) { mask = 0; for (int i = 0; i < Math.Min(n, 63); i += 2) mask |= 1L << i; }
                if (noCore0.IsChecked == true && n >= 4) mask &= ~3L;
                if (mask != all) p.ProcessorAffinity = (IntPtr)mask;
                p.PriorityClass = prio.SelectedIndex switch { 0 => ProcessPriorityClass.High, 1 => ProcessPriorityClass.AboveNormal, 3 => ProcessPriorityClass.RealTime, _ => ProcessPriorityClass.Normal };
                status.Text = $"{p.ProcessName} aberto com prioridade {prio.SelectedItem}" + (mask != all ? " e núcleos limitados" : "") + ". Programas que ele abrir herdam os núcleos.";
            }
            catch (Exception ex) { status.Text = "Não deu: " + ex.Message; }
        }));
        body.Children.Add(row1);
        body.Children.Add(row2);
        body.Children.Add(row3);
        body.Children.Add(status);
    }
}
