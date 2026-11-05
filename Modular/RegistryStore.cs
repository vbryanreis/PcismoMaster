using Microsoft.Win32;
using System.Text.Json;

namespace ResetExplode.Modular;

public sealed class RegistryStore : ISettingsStore
{
    private static RegistryKey Root(Setting s) => RegistryKey.OpenBaseKey(s.Hive switch { "HKCU" => RegistryHive.CurrentUser, "HKU" => RegistryHive.Users, _ => RegistryHive.LocalMachine }, RegistryView.Registry64);
    public SavedValue Read(Setting s)
    {
        using var root = Root(s);
        using var key = root.OpenSubKey(s.Path);
        if (key == null || !key.GetValueNames().Contains(s.Name, StringComparer.OrdinalIgnoreCase)) return new(false, 0, "");
        var kind = key.GetValueKind(s.Name);
        var data = key.GetValue(s.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!;
        return new(true, (int)kind, JsonSerializer.Serialize(data, data.GetType()));
    }

    public void Write(Setting s, SavedValue v)
    {
        using var root = Root(s);
        if (!v.Exists)
        {
            using var existing = root.OpenSubKey(s.Path, true);
            existing?.DeleteValue(s.Name, false);
            return;
        }
        object data = (RegistryValueKind)v.Kind switch
        {
            RegistryValueKind.DWord => JsonSerializer.Deserialize<int>(v.Data),
            RegistryValueKind.QWord => JsonSerializer.Deserialize<long>(v.Data),
            RegistryValueKind.Binary or RegistryValueKind.None => JsonSerializer.Deserialize<byte[]>(v.Data)!,
            RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(v.Data)!,
            RegistryValueKind.String or RegistryValueKind.ExpandString => JsonSerializer.Deserialize<string>(v.Data)!,
            _ => throw new InvalidOperationException("Tipo de registro não suportado.")
        };
        using var key = root.CreateSubKey(s.Path, true);
        key.SetValue(s.Name, data, (RegistryValueKind)v.Kind);
        key.Flush();
    }
}
