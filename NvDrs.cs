using System.Runtime.InteropServices;
using System.Text;

namespace ResetExplode;

public enum DrsType { Dword = 0, Binary = 1, String = 2, WString = 3, Qword = 4 }
public enum DrsLocation { Current = 0, Global = 1, Base = 2, Default = 3 }

/// <summary>Uma configuração lida do driver para um perfil.</summary>
public sealed record DrsValue(uint Id, DrsType Type, DrsLocation Location, bool IsPredefined, ulong Number, string? Text);


public sealed class DrsException(string what, int status) : Exception($"{what}: {NvDrs.Describe(status)}")
{
    public int Status { get; } = status;
}

/// <summary>
/// Acesso às configurações de perfis do driver NVIDIA (DRS da NVAPI), o mesmo banco que o Painel de Controle NVIDIA e o
/// NVIDIA Profile Inspector usam. Os IDs das funções e o tamanho das estruturas seguem a NVAPI (os mesmos que o
/// nvidiaProfileInspector usa, licença MIT). As estruturas grandes são tratadas como bytes, sem marshalling automático.
/// Cada operação abre a sessão, carrega, executa e (se gravou) salva — sempre com dados frescos do driver.
/// </summary>
public static class NvDrs
{
    public const int OK = 0, SettingNotFound = -160, EndEnumeration = -7, ProfileNotFound = -163;

    // Tamanhos das estruturas (versão 1 de NVDRS_SETTING/NVDRS_PROFILE, versão 4 de NVDRS_APPLICATION)
    private const int StrBytes = 4096;                  // NvAPI_UnicodeString: 2048 caracteres UTF-16
    private const int SettingSize = 12320;
    private const int SetId = 4100, SetType = 4104, SetLocation = 4108, SetIsCurrentPredefined = 4112, SetIsPredefinedValid = 4116,
        SetPredefined = 4120, SetCurrent = 8220;
    private const int ProfileSize = 4116;
    private const int AppSize = 20492;      // 4 + 4 + 4 textos + bitvector + commandLine
    private static uint Ver(int size, int v) => (uint)(size | (v << 16));

    [DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnVoid();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrRef(ref IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtr(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrPtr(IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrRefPtr(IntPtr a, ref IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrPtrPtr(IntPtr a, IntPtr b, IntPtr c);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrPtrUint(IntPtr a, IntPtr b, uint c);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)] private delegate int FnPtrStrRefPtr(IntPtr a, string name, ref IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnGetSetting5(IntPtr s, IntPtr p, uint id, IntPtr setting, ref uint x);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnGetSetting4(IntPtr s, IntPtr p, uint id, IntPtr setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnSetSetting5(IntPtr s, IntPtr p, IntPtr setting, uint x, uint y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnSetSetting3(IntPtr s, IntPtr p, IntPtr setting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnEnum(IntPtr s, IntPtr p, uint start, ref uint count, IntPtr arr);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnErr(int status, StringBuilder text);

    private static T? Fn<T>(uint id, uint? fallback = null) where T : Delegate
    {
        IntPtr p = QueryInterface(id);
        if (p == IntPtr.Zero && fallback is uint f) p = QueryInterface(f);
        return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    private static bool _init;
    private static readonly object Gate = new();

    public static bool Available
    {
        get
        {
            try { lock (Gate) { if (!_init) _init = Fn<FnVoid>(0x0150E828)?.Invoke() == OK; return _init; } }
            catch { return false; }
        }
    }

    public static string Describe(int status)
    {
        try
        {
            var sb = new StringBuilder(64);
            if (Fn<FnErr>(0x6C2D048C)?.Invoke(status, sb) == OK) return $"{sb} ({status})";
        }
        catch { }
        return $"NVAPI {status}";
    }

    private static void Check(int status, string what) { if (status != OK) throw new DrsException(what, status); }

    /// <summary>Abre uma sessão, carrega o banco do driver, executa e fecha. Com <paramref name="save"/>, grava no fim.</summary>
    public static T Session<T>(Func<IntPtr, T> work, bool save = false)
    {
        if (!Available) throw new InvalidOperationException("A NVAPI não está disponível (placa ou driver NVIDIA não encontrados).");
        lock (Gate)
        {
            IntPtr s = IntPtr.Zero;
            Check(Fn<FnPtrRef>(0x0694D52E)!(ref s), "DRS_CreateSession");
            try
            {
                Check(Fn<FnPtr>(0x375DBD6B)!(s), "DRS_LoadSettings");
                T result = work(s);
                if (save) Check(Fn<FnPtr>(0xFCBC7E14)!(s), "DRS_SaveSettings");
                return result;
            }
            finally { Fn<FnPtr>(0xDAD9CFF8)?.Invoke(s); }
        }
    }

    // ---------------- Perfis ----------------

    public static IntPtr BaseProfile(IntPtr s) { IntPtr p = IntPtr.Zero; Check(Fn<FnPtrRefPtr>(0xDA8466A0)!(s, ref p), "DRS_GetBaseProfile"); return p; }

    public static IntPtr FindProfile(IntPtr s, string name)
    {
        IntPtr p = IntPtr.Zero;
        Check(Fn<FnPtrStrRefPtr>(0x7E4A9A0B)!(s, name, ref p), "DRS_FindProfileByName");
        return p;
    }

    public static IntPtr CreateProfile(IntPtr s, string name)
    {
        IntPtr buf = Alloc(ProfileSize);
        try
        {
            Marshal.WriteInt32(buf, (int)Ver(ProfileSize, 1));
            WriteStr(buf, 4, name);
            IntPtr p = IntPtr.Zero;
            Check(Fn<FnPtrPtrRef>(0xCC176068)!(s, buf, ref p), "DRS_CreateProfile");
            return p;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FnPtrPtrRef(IntPtr a, IntPtr b, ref IntPtr c);

    public static void RestoreProfile(IntPtr s, IntPtr p) => Check(Fn<FnPtrPtr>(0xFA5F6134)!(s, p), "DRS_RestoreProfileDefault");

    // ---------------- Aplicativos do perfil ----------------

    public static void AddApp(IntPtr s, IntPtr p, string exe)
    {
        IntPtr buf = Alloc(AppSize);
        try
        {
            Marshal.WriteInt32(buf, (int)Ver(AppSize, 4));
            WriteStr(buf, 8, exe);
            Check(Fn<FnPtrPtrPtr>(0x4347A9DE)!(s, p, buf), "DRS_CreateApplication");
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)] private delegate int FnFindApp(IntPtr s, string name, ref IntPtr p, IntPtr app);

    /// <summary>Perfil a que o executável (ex.: "game.exe") pertence; null se nenhum perfil o tem.</summary>
    public static IntPtr? FindAppProfile(IntPtr s, string exe)
    {
        IntPtr buf = Alloc(AppSize);
        try
        {
            Marshal.WriteInt32(buf, (int)Ver(AppSize, 4));
            IntPtr p = IntPtr.Zero;
            int st = Fn<FnFindApp>(0xEEE566B2)!(s, exe, ref p, buf);
            return st == OK && p != IntPtr.Zero ? p : null;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    // ---------------- Configurações ----------------

    private static DrsValue Decode(IntPtr buf)
    {
        uint id = (uint)Marshal.ReadInt32(buf, SetId);
        var type = (DrsType)Marshal.ReadInt32(buf, SetType);
        var loc = (DrsLocation)Marshal.ReadInt32(buf, SetLocation);
        bool predefined = Marshal.ReadInt32(buf, SetIsCurrentPredefined) == 1 && Marshal.ReadInt32(buf, SetIsPredefinedValid) == 1;
        int off = predefined ? SetPredefined : SetCurrent;
        return type switch
        {
            DrsType.Dword => new DrsValue(id, type, loc, predefined, (uint)Marshal.ReadInt32(buf, off), null),
            DrsType.Qword => new DrsValue(id, type, loc, predefined, (ulong)Marshal.ReadInt64(buf, off), null),
            DrsType.WString => new DrsValue(id, type, loc, predefined, 0, ReadStr(buf, off)),
            DrsType.String => new DrsValue(id, type, loc, predefined, 0, Marshal.PtrToStringAnsi(buf + off)),
            _ => new DrsValue(id, type, loc, predefined, 0, BinaryHex(buf, off)),
        };
    }

    private static string BinaryHex(IntPtr buf, int off)
    {
        int len = Math.Min(Marshal.ReadInt32(buf, off), 4096);
        var bytes = new byte[len];
        Marshal.Copy(buf + off + 4, bytes, 0, len);
        return Convert.ToHexString(bytes);
    }

    /// <summary>Valor efetivo da configuração no perfil (do próprio perfil, herdado do global ou o padrão predefinido). Null = não definida em lugar nenhum.</summary>
    public static DrsValue? Get(IntPtr s, IntPtr p, uint id)
    {
        IntPtr buf = Alloc(SettingSize);
        try
        {
            Marshal.WriteInt32(buf, (int)Ver(SettingSize, 1));
            int st;
            if (Fn<FnGetSetting5>(0xEA99498D) is { } g5) { uint x = 0; st = g5(s, p, id, buf, ref x); }
            else st = Fn<FnGetSetting4>(0x73BF8338)!(s, p, id, buf);
            if (st == SettingNotFound) return null;
            Check(st, "DRS_GetSetting");
            return Decode(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Configurações gravadas diretamente no perfil (inclusive as que não estão no catálogo).</summary>
    public static List<DrsValue> ProfileSettings(IntPtr s, IntPtr p)
    {
        var list = new List<DrsValue>();
        var fn = Fn<FnEnum>(0xCFD6983E, 0xAE3039DA)!;
        const int batch = 32;
        IntPtr buf = Alloc(SettingSize * batch);
        try
        {
            for (uint start = 0; ; start += batch)
            {
                uint count = batch;
                for (int i = 0; i < batch; i++) Marshal.WriteInt32(buf + i * SettingSize, (int)Ver(SettingSize, 1));
                int st = fn(s, p, start, ref count, buf);
                if (st == EndEnumeration || count == 0) break;
                Check(st, "DRS_EnumSettings");
                for (int i = 0; i < count; i++) list.Add(Decode(buf + i * SettingSize));
                if (count < batch) break;
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return list;
    }

    /// <summary>Grava um valor no perfil (número para DWORD/QWORD, texto para strings).</summary>
    public static void Set(IntPtr s, IntPtr p, uint id, DrsType type, ulong number, string? text)
    {
        IntPtr buf = Alloc(SettingSize);
        try
        {
            Marshal.WriteInt32(buf, (int)Ver(SettingSize, 1));
            Marshal.WriteInt32(buf, SetId, (int)id);
            Marshal.WriteInt32(buf, SetType, (int)type);
            switch (type)
            {
                case DrsType.Dword: Marshal.WriteInt32(buf, SetCurrent, (int)(uint)number); break;
                case DrsType.Qword: Marshal.WriteInt64(buf, SetCurrent, (long)number); break;
                case DrsType.WString: WriteStr(buf, SetCurrent, text ?? ""); break;
                case DrsType.String:
                    var ansi = Encoding.Default.GetBytes(text ?? "");
                    Marshal.Copy(ansi, 0, buf + SetCurrent, Math.Min(ansi.Length, 4095));
                    break;
                default: throw new NotSupportedException("Valores binários não podem ser editados.");
            }
            int st;
            if (Fn<FnSetSetting5>(0x8A2CF5F5) is { } s5) st = s5(s, p, buf, 0, 0);
            else st = Fn<FnSetSetting3>(0x577DD202)!(s, p, buf);
            Check(st, "DRS_SetSetting");
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Tira a configuração do perfil: ele volta a herdar o valor do perfil global ou o padrão do driver.</summary>
    public static void Delete(IntPtr s, IntPtr p, uint id)
    {
        int st = Fn<FnPtrPtrUint>(0xD20D29DF, 0xE4A26362)!(s, p, id);
        if (st != SettingNotFound) Check(st, "DRS_DeleteProfileSetting");
    }

    // ---------------- Auxiliares ----------------

    private static IntPtr Alloc(int size)
    {
        IntPtr p = Marshal.AllocHGlobal(size);
        unsafe { new Span<byte>((void*)p, size).Clear(); }
        return p;
    }

    private static string ReadStr(IntPtr buf, int off) => Marshal.PtrToStringUni(buf + off) ?? "";

    private static void WriteStr(IntPtr buf, int off, string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text.Length > 2047 ? text[..2047] : text);
        Marshal.Copy(bytes, 0, buf + off, bytes.Length);
    }
}
