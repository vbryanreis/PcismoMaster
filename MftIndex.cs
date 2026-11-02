using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ResetExplode;

/// <summary>
/// Índice de arquivos e pastas no estilo do Everything. Lê a MFT (Master File Table) de cada volume NTFS direto
/// pelo FSCTL_ENUM_USN_DATA (precisa de administrador — o PcismoMaster já roda elevado) e guarda em memória o nome
/// e o pai de cada registro. O caminho completo é remontado só na hora de mostrar, e a busca por trecho do nome é
/// instantânea. Só indexa volumes fixos NTFS prontos.
/// </summary>
public sealed class MftIndex
{
    public readonly record struct Hit(string Name, string FullPath, bool IsDir);

    // Registro cru da MFT: nome do arquivo/pasta e referência do pai (para remontar o caminho).
    private readonly record struct Node(string Name, long ParentFrn, bool IsDir, char Drive);

    private readonly List<Node> _nodes = new();
    private readonly Dictionary<(char Drive, long Frn), int> _index = new(); // (volume, frn) -> posição em _nodes
    private const int MaxNodes = 4_000_000; // teto de segurança para memória

    public int Count => _nodes.Count;
    public bool Ready { get; private set; }

    /// <summary>Volumes fixos NTFS prontos para indexar.</summary>
    public static IEnumerable<char> NtfsDrives() =>
        DriveInfo.GetDrives()
                 .Where(d => d.DriveType == DriveType.Fixed && d.IsReady &&
                             string.Equals(d.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                 .Select(d => char.ToUpperInvariant(d.Name[0]));

    /// <summary>Monta (ou remonta) o índice lendo a MFT de todos os volumes NTFS. Rode fora da thread da UI.</summary>
    public void Build()
    {
        _nodes.Clear();
        _index.Clear();
        Ready = false;
        foreach (char drive in NtfsDrives())
        {
            try { ReadVolume(drive); }
            catch { /* um volume que falha não impede os outros */ }
            if (_nodes.Count >= MaxNodes) break;
        }
        Ready = true;
    }

    /// <summary>Busca por trecho do nome (sem diferenciar maiúsculas/acentos de caixa). Retorna até <paramref name="max"/> itens.</summary>
    public List<Hit> Search(string query, bool filesOnly, bool foldersOnly, int max)
    {
        var hits = new List<Hit>(max);
        if (string.IsNullOrWhiteSpace(query)) return hits;
        foreach (var node in _nodes)
        {
            if (filesOnly && node.IsDir) continue;
            if (foldersOnly && !node.IsDir) continue;
            if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new Hit(node.Name, BuildPath(node), node.IsDir));
                if (hits.Count >= max) break;
            }
        }
        return hits;
    }

    /// <summary>Remonta o caminho completo subindo pelos pais até a raiz do volume.</summary>
    private string BuildPath(Node node)
    {
        var parts = new List<string> { node.Name };
        long cur = node.ParentFrn;
        char drive = node.Drive;
        for (int guard = 0; guard < 512; guard++)
        {
            if (!_index.TryGetValue((drive, cur), out int i)) break;
            var parent = _nodes[i];
            if (parent.Name is "." or "") break; // raiz do volume
            parts.Add(parent.Name);
            cur = parent.ParentFrn;
        }
        parts.Reverse();
        return drive + ":\\" + string.Join('\\', parts);
    }

    // ---------------- leitura da MFT via FSCTL_ENUM_USN_DATA ----------------

    private void ReadVolume(char drive)
    {
        using SafeFileHandle h = CreateFile($@"\\.\{drive}:", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                                            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) return;

        const int bufSize = 1 << 18; // 256 KB
        IntPtr outBuf = Marshal.AllocHGlobal(bufSize);
        IntPtr inBuf = Marshal.AllocHGlobal(24);
        try
        {
            var med = new MFT_ENUM_DATA_V0 { StartFileReferenceNumber = 0, LowUsn = 0, HighUsn = long.MaxValue };
            while (true)
            {
                Marshal.StructureToPtr(med, inBuf, false);
                if (!DeviceIoControl(h, FSCTL_ENUM_USN_DATA, inBuf, 24, outBuf, bufSize, out int ret, IntPtr.Zero))
                    break; // ERROR_HANDLE_EOF: acabou
                if (ret <= 8) break;

                long next = Marshal.ReadInt64(outBuf, 0); // próxima referência inicial
                int offset = 8;
                while (offset < ret)
                {
                    int recLen = Marshal.ReadInt32(outBuf, offset + 0);
                    if (recLen <= 0) break;
                    long frn = Marshal.ReadInt64(outBuf, offset + 8);
                    long parent = Marshal.ReadInt64(outBuf, offset + 16);
                    uint attrs = unchecked((uint)Marshal.ReadInt32(outBuf, offset + 52));
                    int nameLen = (ushort)Marshal.ReadInt16(outBuf, offset + 56);
                    int nameOff = (ushort)Marshal.ReadInt16(outBuf, offset + 58);
                    string name = Marshal.PtrToStringUni(outBuf + offset + nameOff, nameLen / 2) ?? "";
                    bool isDir = (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0;

                    _index[(drive, frn)] = _nodes.Count;
                    _nodes.Add(new Node(name, parent, isDir, drive));

                    if (_nodes.Count >= MaxNodes) return;
                    offset += recLen;
                }
                med.StartFileReferenceNumber = next;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(outBuf);
            Marshal.FreeHGlobal(inBuf);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_ENUM_DATA_V0
    {
        public long StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x1, FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FSCTL_ENUM_USN_DATA = 0x000900B3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec,
                                                    uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inBuf, int inSize,
                                               IntPtr outBuf, int outSize, out int returned, IntPtr overlapped);
}
