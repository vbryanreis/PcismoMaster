using System.Management;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Lê a placa NVIDIA deste PC (WMI e registro) para montar o <see cref="GpuCard"/>.</summary>
public static class GpuDetect
{
    /// <summary>Lê a placa NVIDIA (a primeira) deste PC; null se não houver.</summary>
    public static GpuCard? DetectNvidia()
    {
        var gpus = DriverManager.ReadGpus();
        var nv = gpus.FirstOrDefault(g => g.Vendor == "NVIDIA");
        if (nv == null) return null;
        string arch = GpuCard.ArchOf(nv.DeviceId);
        bool laptop = IsLaptop(), hybrid = laptop && gpus.Any(g => g.Vendor is "Intel" or "AMD"); // Optimus: só notebooks
        return new GpuCard(nv.GpuName, nv.DeviceId, arch, GpuCard.IsRtx(arch, nv.GpuName, nv.DeviceId), VramOf("VEN_10DE"), laptop, hybrid);
    }

    /// <summary>Memória da placa pelo registro do driver (HardwareInformation.qwMemorySize); 0 se não der para ler (ex.: sem driver).</summary>
    private static long VramOf(string vendor)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            foreach (var name in root?.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)) ?? Enumerable.Empty<string>())
            {
                using var k = root!.OpenSubKey(name);
                if ((k?.GetValue("MatchingDeviceId") as string)?.Contains(vendor, StringComparison.OrdinalIgnoreCase) != true) continue;
                object? v = k!.GetValue("HardwareInformation.qwMemorySize") ?? k.GetValue("HardwareInformation.MemorySize");
                long bytes = v switch { long l => l, int i => (uint)i, byte[] b when b.Length >= 8 => BitConverter.ToInt64(b), byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b), _ => 0 };
                if (bytes > 0) return bytes / 1048576;
            }
        }
        catch { }
        return 0;
    }

    private static bool? _laptop;

    /// <summary>Notebook pelo tipo de gabinete do SMBIOS (ou, sem ele, por ter bateria).</summary>
    public static bool IsLaptop()
    {
        if (_laptop is { } known) return known;
        bool laptop = false;
        try
        {
            using var s = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
            foreach (ManagementObject o in s.Get())
                using (o)
                    if (o["ChassisTypes"] is ushort[] types && types.Any(t => t is 8 or 9 or 10 or 11 or 12 or 14 or 18 or 21 or 30 or 31 or 32)) laptop = true;
            if (!laptop)
            {
                using var b = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_Battery");
                laptop = b.Get().Count > 0;
            }
        }
        catch { }
        return (_laptop = laptop).Value;
    }
}
