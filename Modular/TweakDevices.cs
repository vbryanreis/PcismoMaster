using Microsoft.Win32;

namespace ResetExplode.Modular;

/// <summary>
/// Ajustes do TWEAKING que dependem do que existe no PC (placas de rede, dispositivos, discos, placa AMD). Só leem o
/// registro para montar a lista de valores; a gravação é do ModuleEngine (com backup e desfazer), quando o usuário aplica.
/// </summary>
public static class TweakDevices
{
    private const string NetClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private const string GpuClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string Enum = @"SYSTEM\CurrentControlSet\Enum";

    private static RegistryKey Lm() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    /// <summary>Caminhos (a partir de HKLM) das subchaves com este nome debaixo de root, até a profundidade dada.</summary>
    public static List<string> FindKeys(string root, string name, int depth)
    {
        var found = new List<string>();
        using var lm = Lm();
        void Walk(string path, int left)
        {
            string[] subs;
            try { using var k = lm.OpenSubKey(path); if (k == null) return; subs = k.GetSubKeyNames(); }
            catch { return; }
            foreach (var s in subs)
            {
                string p = path + "\\" + s;
                if (s.Equals(name, StringComparison.OrdinalIgnoreCase)) found.Add(p);
                else if (left > 0) Walk(p, left - 1);
            }
        }
        Walk(root, depth);
        return found;
    }

    /// <summary>Placas de rede: sem economia de energia (EEE, Green Ethernet…) e sem acordar o PC. Só os valores que o driver da placa tem.</summary>
    public static SetupTweak NetworkPower()
    {
        var list = new List<Setting>();
        using var lm = Lm();
        using var cls = lm.OpenSubKey(NetClass);
        foreach (var sub in cls?.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)) ?? Enumerable.Empty<string>())
        {
            string path = NetClass + "\\" + sub;
            string[] names;
            try { using var k = cls!.OpenSubKey(sub); if (k?.GetValue("NetCfgInstanceId") == null) continue; names = k.GetValueNames(); }
            catch { continue; }
            list.Add(new("HKLM", path, "PnPCapabilities", 24));
            foreach (var v in new[] { "AdvancedEEE", "*EEE", "EEELinkAdvertisement", "SipsEnabled", "ULPMode", "GigaLite", "EnableGreenEthernet",
                                      "PowerSavingMode", "S5WakeOnLan", "*WakeOnMagicPacket", "*ModernStandbyWoLMagicPacket", "*WakeOnPattern", "WakeOnLink" })
                if (names.Contains(v, StringComparer.OrdinalIgnoreCase)) list.Add(new("HKLM", path, v, 0, "0"));
        }
        return new("tw-win-net-power", "Placas de rede sem economia de energia e sem acordar o PC",
            "Desliga a economia de energia das placas de rede (Energy Efficient Ethernet, Green Ethernet, modo de baixo consumo) e o Wake-on-LAN. Menos quedas de ping; o PC não liga mais pela rede. Vale depois de reiniciar (ou desativar e ativar a placa).",
            false, true, "Windows/26 Network Adapter Power Savings & Wake.ps1", list.ToArray());
    }

    /// <summary>Dispositivos (ACPI, HID, PCI e USB): sem suspensão seletiva/economia de energia e sem acordar o PC.</summary>
    public static SetupTweak DevicePower()
    {
        var list = new List<Setting>();
        foreach (var bus in new[] { "ACPI", "HID", "PCI", "USB" })
        {
            foreach (var p in FindKeys(Enum + "\\" + bus, "Device Parameters", 2))
            {
                list.Add(new("HKLM", p, "EnhancedPowerManagementEnabled", 0));
                list.Add(new("HKLM", p, "SelectiveSuspendEnabled", 0, Hex: "00"));
                list.Add(new("HKLM", p, "SelectiveSuspendOn", 0));
                list.Add(new("HKLM", p, "WaitWakeEnabled", 0));
                using var lm = Lm();
                using var wdf = lm.OpenSubKey(p + "\\WDF");
                if (wdf != null) list.Add(new("HKLM", p + "\\WDF", "IdleInWorkingState", 0));
            }
        }
        return new("tw-win-device-power", "Dispositivos sem economia de energia e sem acordar o PC",
            "Mouse, teclado, controles e placas USB/PCI deixam de entrar em suspensão (sem atraso ao voltar a mexer) e não acordam mais o PC. Vale depois de reiniciar. Notebooks gastam um pouco mais de bateria.",
            false, true, "Windows/25 Device Manager Power Savings & Wake.ps1", list.Where(RegistryStore.CanWrite).ToArray());
    }

    /// <summary>SSDs NVMe/SATA: sem esvaziar o cache de gravação a cada escrita.</summary>
    public static SetupTweak WriteCache()
    {
        var list = new List<Setting>();
        foreach (var bus in new[] { "SCSI", "NVME" })
            foreach (var p in FindKeys(Enum + "\\" + bus, "Device Parameters", 2))
                list.Add(new("HKLM", p + "\\Disk", "CacheIsPowerProtected", 1));
        return new("tw-win-write-cache", "Sem esvaziar o cache de gravação dos discos",
            "Os discos confirmam a gravação pelo cache sem esperar o esvaziamento. Mais rápido, mas numa queda de energia sem nobreak dá para perder o que estava sendo gravado.",
            true, false, "Windows/28 Write Cache Buffer Flushing.ps1", list.Where(RegistryStore.CanWrite).ToArray());
    }

    /// <summary>Placa AMD: sem ULPS (estado de consumo ultrabaixo), só nas chaves de placa de vídeo que têm o valor.</summary>
    public static SetupTweak AmdUlps()
    {
        var list = new List<Setting>();
        using var lm = Lm();
        using var cls = lm.OpenSubKey(GpuClass);
        foreach (var sub in cls?.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)) ?? Enumerable.Empty<string>())
        {
            try
            {
                using var k = cls!.OpenSubKey(sub);
                if (k?.GetValueNames().Contains("EnableUlps", StringComparer.OrdinalIgnoreCase) == true)
                    list.Add(new("HKLM", GpuClass + "\\" + sub, "EnableUlps", 0));
            }
            catch { }
        }
        return new("tw-adv-ulps", "AMD ULPS desligado",
            "Placas AMD (principalmente com duas placas ou CrossFire) param de entrar no modo de consumo ultrabaixo, que pode causar travadas ao acordar a placa. Gasta um pouco mais de energia parado.",
            true, false, "Advanced/14 Ulps.ps1", list.ToArray());
    }
}
