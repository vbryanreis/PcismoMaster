using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ResetExplode;

/// <summary>
/// Índice de arquivos e pastas no estilo do Everything. Lê a MFT (Master File Table) de cada volume NTFS direto
/// pelo FSCTL_ENUM_USN_DATA (precisa de administrador — o PcismoMaster já roda elevado) e guarda em memória o nome
/// e o pai de cada registro. O caminho completo é remontado só na hora de mostrar, e a busca por trecho do nome é
/// instantânea. Só indexa volumes fixos NTFS prontos.
/// <para>Leve para a RAM e para o GC: em vez de milhões de <see cref="string"/> e de um dicionário, os nomes ficam
/// num buffer único de <c>char</c> (início/tamanho por item) e cada item guarda a posição do pai num <c>int[]</c>,
/// resolvida uma vez só com busca binária. São poucos arrays grandes, ~10 bytes por item além do nome.</para>
/// </summary>
public sealed class MftIndex
{
    public readonly record struct Hit(string Name, string FullPath, bool IsDir);

    /// <summary>Índice pronto e imutável; a busca usa o retrato atual, então <see cref="Release"/> pode rodar a qualquer hora.</summary>
    private sealed class Data
    {
        public required char[] Names;          // todos os nomes, um atrás do outro
        public required int[] NameStart;       // início do nome de cada item em Names
        public required ushort[] NameLen;      // tamanho do nome (até 255) | DirFlag
        public required int[] Parent;          // posição do pai, ou -1 (raiz/desconhecido)
        public required (char Drive, int End)[] Volumes; // itens [fim do anterior, End) são deste volume
        public int Count => NameStart.Length;
    }

    private const ushort DirFlag = 0x8000;
    private const int MaxNodes = 4_000_000; // teto de segurança para memória
    private const long RecordMask = 0x0000_FFFF_FFFF_FFFF; // FRN = nº do registro (48 bits) + sequência (16 bits)

    private volatile Data? _data;

    public int Count => _data?.Count ?? 0;
    public bool Ready => _data != null;

    /// <summary>Volumes fixos NTFS prontos para indexar.</summary>
    public static IEnumerable<char> NtfsDrives() =>
        DriveInfo.GetDrives()
                 .Where(d => d.DriveType == DriveType.Fixed && d.IsReady &&
                             string.Equals(d.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                 .Select(d => char.ToUpperInvariant(d.Name[0]));

    /// <summary>Monta (ou remonta) o índice lendo a MFT de todos os volumes NTFS. Rode fora da thread da UI.</summary>
    public void Build()
    {
        _data = null;
        var b = new Builder();
        foreach (char drive in NtfsDrives())
        {
            try { ReadVolume(drive, b); }
            catch { b.DropVolume(); /* um volume que falha não impede os outros */ }
            b.EndVolume(drive);
            if (b.Count >= MaxNodes) break;
        }
        _data = b.Finish();
        CompactMemory(); // os arrays temporários da leitura (centenas de MB em discos cheios) voltam ao Windows já
    }

    /// <summary>Solta o índice da memória (a próxima busca indexa de novo).</summary>
    public void Release()
    {
        if (_data == null) return;
        _data = null;
        CompactMemory();
    }

    private static void CompactMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    /// <summary>Busca por trecho do nome (sem diferenciar maiúsculas/acentos de caixa). Retorna até <paramref name="max"/> itens.</summary>
    public List<Hit> Search(string query, bool filesOnly, bool foldersOnly, int max)
    {
        var hits = new List<Hit>(max);
        var d = _data;
        if (d == null || string.IsNullOrWhiteSpace(query)) return hits;
        ReadOnlySpan<char> q = query;
        char[] names = d.Names; int[] start = d.NameStart; ushort[] len = d.NameLen;
        int vol = 0;
        for (int i = 0; i < start.Length; i++)
        {
            bool isDir = (len[i] & DirFlag) != 0;
            if (filesOnly && isDir) continue;
            if (foldersOnly && !isDir) continue;
            var name = new ReadOnlySpan<char>(names, start[i], len[i] & ~DirFlag);
            if (!name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            while (i >= d.Volumes[vol].End) vol++;
            hits.Add(new Hit(name.ToString(), BuildPath(d, i, d.Volumes[vol].Drive), isDir));
            if (hits.Count >= max) break;
        }
        return hits;
    }

    /// <summary>Remonta o caminho completo subindo pelos pais até a raiz do volume.</summary>
    private static string BuildPath(Data d, int node, char drive)
    {
        Span<int> chain = stackalloc int[512];
        int n = 0;
        chain[n++] = node;
        for (int cur = d.Parent[node]; cur >= 0 && n < chain.Length; cur = d.Parent[cur])
        {
            int len = d.NameLen[cur] & ~DirFlag;
            if (len == 0 || (len == 1 && d.Names[d.NameStart[cur]] == '.')) break; // raiz do volume
            chain[n++] = cur;
        }
        var sb = new StringBuilder(64).Append(drive).Append(":\\");
        for (int k = n - 1; k >= 0; k--)
        {
            int i = chain[k];
            sb.Append(d.Names, d.NameStart[i], d.NameLen[i] & ~DirFlag);
            if (k > 0) sb.Append('\\');
        }
        return sb.ToString();
    }

    /// <summary>Monta o índice a partir de registros prontos (testes, sem tocar no disco).</summary>
    internal static MftIndex FromRecords(IEnumerable<(char Drive, long Frn, long Parent, bool IsDir, string Name)> records)
    {
        var b = new Builder();
        char? drive = null;
        foreach (var r in records)
        {
            if (drive != r.Drive) { if (drive is char prev) b.EndVolume(prev); drive = r.Drive; }
            int at = b.ReserveName(r.Name.Length);
            r.Name.CopyTo(0, b.Names, at, r.Name.Length);
            b.Add(r.Frn, r.Parent, r.IsDir, at, r.Name.Length);
        }
        if (drive is char last) b.EndVolume(last);
        return new MftIndex { _data = b.Finish() };
    }

    /// <summary>Acumula os itens em arrays que crescem; os FRN (8 bytes cada) só existem durante a montagem.</summary>
    private sealed class Builder
    {
        public char[] Names = new char[1 << 20];
        private int _nameUsed;
        private int[] _start = new int[1 << 16];
        private ushort[] _len = new ushort[1 << 16];
        private long[] _rec = new long[1 << 16], _parentRec = new long[1 << 16];
        private int[] _parent = Array.Empty<int>();
        private readonly List<(char, int)> _volumes = new();
        private int _volStart;

        public int Count { get; private set; }

        public int ReserveName(int len)
        {
            if (_nameUsed + len > Names.Length) Array.Resize(ref Names, Math.Max(Names.Length * 2, _nameUsed + len));
            return _nameUsed;
        }

        public void Add(long frn, long parent, bool isDir, int nameStart, int nameLen)
        {
            if (Count == _start.Length)
            {
                int cap = Math.Min(_start.Length * 2, MaxNodes);
                Array.Resize(ref _start, cap); Array.Resize(ref _len, cap);
                Array.Resize(ref _rec, cap); Array.Resize(ref _parentRec, cap);
            }
            nameLen = Math.Min(nameLen, 0x7FFF);
            _start[Count] = nameStart;
            _len[Count] = (ushort)(nameLen | (isDir ? DirFlag : 0));
            _rec[Count] = frn & RecordMask;
            _parentRec[Count] = parent & RecordMask;
            _nameUsed = nameStart + nameLen;
            Count++;
        }

        /// <summary>Descarta os itens do volume atual (leitura que falhou no meio).</summary>
        public void DropVolume() { Count = _volStart; _nameUsed = Count == 0 ? 0 : _start[Count - 1] + (_len[Count - 1] & ~DirFlag); }

        /// <summary>Fecha o volume: liga cada item ao pai por busca binária no nº do registro (só dentro do volume).</summary>
        public void EndVolume(char drive)
        {
            int s = _volStart, e = Count;
            if (e == s) return;
            if (_parent.Length < _start.Length) Array.Resize(ref _parent, _start.Length);

            // O FSCTL_ENUM_USN_DATA entrega em ordem de registro; se não vier, ordena uma cópia com a posição.
            long[] keys; int[] pos;
            bool sorted = true;
            for (int i = s + 1; i < e && sorted; i++) sorted = _rec[i - 1] < _rec[i];
            if (sorted) { keys = _rec; pos = Array.Empty<int>(); }
            else
            {
                keys = _rec[s..e]; pos = new int[e - s];
                for (int i = 0; i < pos.Length; i++) pos[i] = s + i;
                Array.Sort(keys, pos);
            }
            int lo = sorted ? s : 0, n = e - s;
            for (int i = s; i < e; i++)
            {
                int k = Array.BinarySearch(keys, lo, n, _parentRec[i]);
                int p = k < 0 ? -1 : sorted ? k : pos[k - lo];
                _parent[i] = p == i ? -1 : p;
            }
            _volumes.Add((drive, e));
            _volStart = e;
        }

        public Data Finish()
        {
            Array.Resize(ref Names, _nameUsed);
            Array.Resize(ref _start, Count); Array.Resize(ref _len, Count); Array.Resize(ref _parent, Count);
            return new Data { Names = Names, NameStart = _start, NameLen = _len, Parent = _parent, Volumes = _volumes.ToArray() };
        }
    }

    // ---------------- leitura da MFT via FSCTL_ENUM_USN_DATA ----------------

    private static void ReadVolume(char drive, Builder b)
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
                    int nameLen = (ushort)Marshal.ReadInt16(outBuf, offset + 56) / 2;
                    int nameOff = (ushort)Marshal.ReadInt16(outBuf, offset + 58);

                    // o nome vai direto do buffer do Windows para o buffer único, sem criar string
                    int at = b.ReserveName(nameLen);
                    if (nameLen > 0) Marshal.Copy(outBuf + offset + nameOff, b.Names, at, nameLen);
                    b.Add(frn, parent, (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0, at, nameLen);

                    if (b.Count >= MaxNodes) return;
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
