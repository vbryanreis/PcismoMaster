using System.IO;
using System.Runtime.InteropServices;

namespace ResetExplode.Modular;

/// <summary>Um tipo de lixo da Limpeza: pastas (com curinga * em um nível), idade mínima dos arquivos e serviço a parar antes.</summary>
public sealed record CleanCategory(string Id, string Title, string Description, bool DefaultOn, string[] Roots, double MinAgeHours = 0,
    string? FileMask = null, string[]? StopServices = null, bool RecycleBin = false, bool WholeFolder = false);

/// <summary>
/// Limpeza: mede e apaga arquivos que o Windows e os programas recriam sozinhos. Nunca segue atalhos de pasta
/// (junction/symlink), pula arquivos em uso e não mexe em nada fora das pastas listadas.
/// </summary>
public static class Cleanup
{
    private static string L(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), rel);
    private static string Win(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), rel);
    private static string Pd(string rel) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), rel);
    private static string LocalLow(string rel) => Path.Combine(Path.GetDirectoryName(L("x"))!, "..", "LocalLow", rel);

    public static readonly CleanCategory[] Categories =
    {
        new("temp_user", "Temporários do usuário", "Arquivos que os programas deixam na pasta TEMP (só os com mais de 2 horas).", true, new[] { Path.GetTempPath() }, 2),
        new("temp_win", "Temporários do Windows", "A pasta Windows\\Temp (só os com mais de 2 horas).", true, new[] { Win("Temp") }, 2),
        new("recycle", "Lixeira", "Esvazia a Lixeira de todas as unidades.", true, Array.Empty<string>(), RecycleBin: true),
        new("wu_cache", "Downloads do Windows Update", "Atualizações já instaladas que ficaram guardadas. O serviço de atualização é pausado durante a limpeza.", true,
            new[] { Win(@"SoftwareDistribution\Download") }, StopServices: new[] { "wuauserv", "bits" }),
        new("delivery", "Cache de Otimização de Entrega", "Pedaços de atualizações guardados para compartilhar com outros PCs.", true,
            new[] { Win(@"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") }, StopServices: new[] { "dosvc" }),
        new("wer", "Relatórios de erros", "Relatórios de travamentos já enviados ou na fila, e despejos de memória de programas.", true,
            new[] { Pd(@"Microsoft\Windows\WER\ReportArchive"), Pd(@"Microsoft\Windows\WER\ReportQueue"), L("CrashDumps") }),
        new("dumps", "Despejos de memória do Windows", "Arquivos gerados quando o Windows dá tela azul (minidumps e MEMORY.DMP).", true,
            new[] { Win("Minidump"), Win("LiveKernelReports") }),
        new("shaders", "Cache de shaders", "DirectX, NVIDIA e AMD. Os jogos recriam na primeira partida: ela pode engasgar um pouco.", false,
            new[] { L("D3DSCache"), L(@"NVIDIA\DXCache"), L(@"NVIDIA\GLCache"), L(@"AMD\DxCache"), L(@"AMD\DxcCache"), L(@"AMD\GLCache"), L(@"AMD\VkCache"),
                    LocalLow(@"NVIDIA\PerDriverVersion\DXCache") }),
        new("browsers", "Cache dos navegadores", "Só o cache de páginas de Chrome, Edge, Brave, Opera e Firefox. Senhas, histórico e logins não são tocados. Feche os navegadores antes.", false,
            new[] { L(@"Google\Chrome\User Data\*\Cache"), L(@"Google\Chrome\User Data\*\Code Cache"), L(@"Google\Chrome\User Data\*\GPUCache"),
                    L(@"Microsoft\Edge\User Data\*\Cache"), L(@"Microsoft\Edge\User Data\*\Code Cache"), L(@"Microsoft\Edge\User Data\*\GPUCache"),
                    L(@"BraveSoftware\Brave-Browser\User Data\*\Cache"), L(@"BraveSoftware\Brave-Browser\User Data\*\Code Cache"), L(@"BraveSoftware\Brave-Browser\User Data\*\GPUCache"),
                    L(@"Opera Software\Opera Stable\Cache"), L(@"Opera Software\Opera GX Stable\Cache"), L(@"Mozilla\Firefox\Profiles\*\cache2") }),
        new("thumbs", "Miniaturas do Explorer", "O cache de miniaturas de fotos e vídeos. O Explorer recria ao abrir as pastas.", false,
            new[] { L(@"Microsoft\Windows\Explorer") }, FileMask: "thumbcache_*.db"),
        new("prefetch", "Prefetch", "Dados que o Windows usa para abrir programas mais rápido. Ele recria, mas os primeiros dias podem ficar mais lentos.", false,
            new[] { Win("Prefetch") }, FileMask: "*.pf"),
        new("windows_old", "Windows antigo (Windows.old)", "A cópia do Windows anterior que fica depois de uma atualização grande ou reinstalação. Costuma ter de 10 a 30 GB. " +
            "AVISO: depois de apagar não dá mais para voltar para a versão anterior do Windows (Configurações > Recuperação > Voltar).", false,
            new[] { Path.Combine(Path.GetPathRoot(Win(""))!, "Windows.old") }, WholeFolder: true),
    };

    /// <summary>As pastas reais de um tipo (expande o * de perfis de navegador).</summary>
    public static IEnumerable<string> Expand(string root)
    {
        int star = root.IndexOf('*');
        if (star < 0) { if (Directory.Exists(root)) yield return root; yield break; }
        string parent = root[..star].TrimEnd('\\');
        string rest = root[(star + 1)..].TrimStart('\\');
        if (!Directory.Exists(parent)) yield break;
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(parent).ToList(); } catch { yield break; }
        foreach (var d in dirs)
        {
            string p = Path.Combine(d, rest);
            if (Directory.Exists(p)) yield return p;
        }
    }

    private static bool IsLink(FileSystemInfo f) => (f.Attributes & FileAttributes.ReparsePoint) != 0;

    /// <summary>Arquivos que sairiam desta pasta (sem entrar em atalhos de pasta).</summary>
    public static IEnumerable<FileInfo> Files(string dir, double minAgeHours, string? mask)
    {
        var cutoff = DateTime.UtcNow.AddHours(-minAgeHours);
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(dir));
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            FileSystemInfo[] items;
            try { items = d.GetFileSystemInfos(); } catch { continue; }
            foreach (var i in items)
            {
                if (IsLink(i)) continue;
                if (i is DirectoryInfo sub) { if (mask == null) stack.Push(sub); }
                else if (i is FileInfo f && (mask == null || FileSystemName.Matches(mask, f.Name)) && (minAgeHours <= 0 || f.LastWriteTimeUtc < cutoff)) yield return f;
            }
        }
    }

    private static class FileSystemName
    {
        public static bool Matches(string mask, string name) => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(mask, name, ignoreCase: true);
    }

    public static (long Bytes, int Count) Measure(CleanCategory c)
    {
        if (c.RecycleBin) return RecycleSize();
        long bytes = 0; int n = 0;
        foreach (var root in c.Roots.SelectMany(Expand))
            foreach (var f in Files(root, c.MinAgeHours, c.FileMask))
            {
                try { bytes += f.Length; n++; } catch { }
            }
        return (bytes, n);
    }

    /// <summary>Apaga os arquivos do tipo; devolve quanto liberou e quantos ficaram (em uso).</summary>
    public static (long Freed, int Skipped) Clean(CleanCategory c, CancellationToken ct = default)
    {
        if (c.RecycleBin)
        {
            var (size, _) = RecycleSize();
            SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4); // sem confirmação, sem barra, sem som
            return (size - RecycleSize().Bytes, 0);
        }
        if (c.WholeFolder) return CleanWholeFolder(c);
        long freed = 0; int skipped = 0;
        foreach (var root in c.Roots.SelectMany(Expand))
        {
            foreach (var f in Files(root, c.MinAgeHours, c.FileMask).ToList())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    long len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len;
                }
                catch { skipped++; }
            }
            if (c.FileMask == null) RemoveEmptyDirs(new DirectoryInfo(root));
        }
        return (freed, skipped);
    }

    /// <summary>Pastas protegidas pelo Windows (Windows.old): toma posse, dá acesso aos Administradores e apaga a pasta inteira.</summary>
    private static (long Freed, int Skipped) CleanWholeFolder(CleanCategory c)
    {
        var (before, _) = Measure(c);
        foreach (var root in c.Roots.Where(Directory.Exists))
        {
            string q = "\"" + root.TrimEnd('\\') + "\"";
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c takeown /f {q} /r /d y >nul 2>&1 & icacls {q} /grant *S-1-5-32-544:F /t /c /q >nul 2>&1 & rd /s /q {q}")
                { CreateNoWindow = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit();
        }
        var (after, left) = Measure(c);
        return (Math.Max(0, before - after), left);
    }

    private static void RemoveEmptyDirs(DirectoryInfo root)
    {
        DirectoryInfo[] subs;
        try { subs = root.GetDirectories(); } catch { return; }
        foreach (var d in subs)
        {
            if (IsLink(d)) continue;
            RemoveEmptyDirs(d);
            try { if (!d.EnumerateFileSystemInfos().Any()) d.Delete(); } catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHQueryRecycleBin(string? root, ref SHQUERYRBINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

    private static (long Bytes, int Count) RecycleSize()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? (info.i64Size, (int)Math.Min(int.MaxValue, info.i64NumItems)) : (0, 0);
    }

    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
