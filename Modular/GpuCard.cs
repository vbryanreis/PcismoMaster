
using System.Text.RegularExpressions;


namespace ResetExplode;

/// <summary>
/// A placa NVIDIA deste PC, para escolher o driver certo e ajustar as configurações do Ultimate (feitas numa RTX 4090 de mesa)
/// a ela: geração pelo código PCI (funciona sem driver), RTX ou não, memória, notebook e gráfico híbrido (Optimus).
/// </summary>
public sealed record GpuCard(string Name, int DeviceId, string Arch, bool Rtx, long VramMb, bool Laptop, bool Hybrid)
{
    /// <summary>Série na busca de drivers da NVIDIA ("RTX 30 Series", "10 Series"…), pela geração; null se desconhecida.</summary>
    public string? DriverSeries => Arch switch
    {
        "Blackwell" => "RTX 50 Series", "Ada" => "RTX 40 Series", "Ampere" => "RTX 30 Series",
        "Turing" => Rtx ? "RTX 20 Series" : "16 Series", "Volta" or "Pascal" => "10 Series", "Maxwell" => "900 Series", "Kepler" => "700 Series",
        _ => null,
    };

    /// <summary>Até 8 GB: sem o "Sysmem Fallback" o CUDA/IA trava por falta de memória em vez de só ficar lento.</summary>
    public bool SmallVram => VramMb is > 0 and <= 8192;

    public string Summary => $"{Name} · {(Arch.Length > 0 ? Arch : "geração desconhecida")}" + (VramMb > 0 ? $" · {Math.Round(VramMb / 1024.0)} GB" : "")
                             + (Laptop ? Hybrid ? " · notebook com gráfico híbrido" : " · notebook" : " · PC de mesa");

    /// <summary>Geração pelo código PCI (DEV_xxxx) da placa.</summary>
    public static string ArchOf(int dev) => dev switch
    {
        >= 0x2B80 and <= 0x2FFF => "Blackwell",
        >= 0x2680 and <= 0x28FF => "Ada",
        >= 0x2200 and <= 0x25FF => "Ampere",
        >= 0x1E00 and <= 0x21FF => "Turing",
        >= 0x1D80 and <= 0x1DFF => "Volta",
        >= 0x1340 and <= 0x14FF or >= 0x17C0 and <= 0x17FF => "Maxwell", // GM107/108/204/206 e GM200 (980 Ti, TITAN X)
        >= 0x15F0 and <= 0x15FF or >= 0x1B00 and <= 0x1D7F => "Pascal",
        >= 0x0FC0 and < 0x1340 => "Kepler",
        _ => "",
    };

    /// <summary>Turing com Tensor Cores (RTX 20) e tudo depois; GTX 16 é Turing sem eles.</summary>
    public static bool IsRtx(string arch, string name, int dev)
        => arch is "Ampere" or "Ada" or "Blackwell"
           || arch == "Turing" && (name.Contains("GTX", StringComparison.OrdinalIgnoreCase) ? false
                                   : name.Contains("RTX", StringComparison.OrdinalIgnoreCase) || dev is >= 0x1E00 and < 0x1F80); // TU102/104/106; TU116/117 = GTX 16

    public static int DeviceIdOf(string pnp)
    {
        var m = Regex.Match(pnp, @"DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16) : 0;
    }

}

/// <summary>Um componente/limpeza da instalação enxuta. <paramref name="Default"/> = já vem marcado para remover.</summary>
public sealed record DebloatItem(string Id, string Vendor, string Title, string Description, bool Default, bool Warn);

/// <summary>As configurações e a instalação enxuta do Ultimate (feitas numa RTX 4090 de mesa) ajustadas à placa deste PC.</summary>
public static class GpuRules
{
    public const uint PowerMode = 274197361, SysmemFallback = 283962569, OptimusCompat = 0x10F9DC80, OptimusRendering = 0x10F9DC81;

    /// <summary>
    /// O .nip ajustado: notebook mantém o gerenciamento de energia do driver (desempenho máximo gasta bateria e esquenta parado);
    /// placa de até 8 GB mantém o "Sysmem Fallback" (sem ele, CUDA/IA trava por falta de memória); notebook com gráfico híbrido
    /// usa a NVIDIA como processador gráfico preferido — o equivalente do "Preferred OpenGL GPU" do script, que só serve com
    /// mais de uma placa e tinha o código da 4090 dele.
    /// </summary>
    public static (uint Id, uint Value, string Name)[] Adapt((uint Id, uint Value, string Name)[] nip, GpuCard? card)
    {
        var list = nip.Where(x => card == null || !(card.Laptop && x.Id == PowerMode) && !(card.SmallVram && x.Id == SysmemFallback)).ToList();
        if (card is { Hybrid: true })
        {
            list.Add((OptimusCompat, 1, "Optimus - Compatibilidade (NVIDIA preferida)"));
            list.Add((OptimusRendering, 1, "Optimus - Processador gráfico preferido: NVIDIA de alto desempenho"));
        }
        return list.ToArray();
    }

    /// <summary>O que muda em relação ao script para esta placa (texto para a tela).</summary>
    public static List<string> Adaptations(GpuCard? card)
    {
        var notes = new List<string>();
        if (card == null) return notes;
        if (card.Laptop) notes.Add("energia no padrão do driver (notebook)");
        if (card.SmallVram) notes.Add("Sysmem Fallback mantido (até 8 GB)");
        if (card.Hybrid) notes.Add("NVIDIA como placa preferida no lugar do \"Preferred OpenGL GPU\" da 4090");
        if (!card.Rtx && card.Arch.Length > 0) notes.Add("DLDSR sai na instalação enxuta (só existe em RTX)");
        return notes;
    }

    /// <summary>
    /// O item da instalação enxuta ajustado: DLDSR só existe em RTX (numa GTX sai sem perda), o Platform Controller só faz algo
    /// em notebook, e a porta USB-C (VirtualLink) só existe em RTX 20 de mesa.
    /// </summary>
    public static DebloatItem ForCard(DebloatItem i, GpuCard? card) => card == null ? i : i.Id switch
    {
        "NvDLISR" when !card.Rtx && card.Arch.Length > 0 => i with { Default = true, Warn = false, Description = "O DLDSR só funciona em placas RTX; nesta placa ele não é usado." },
        "NVPCF" when !card.Laptop => i with { Default = true, Warn = false, Description = "Controla o Dynamic Boost de notebooks; neste PC de mesa não faz nada." },
        "NVPCF" => i with { Description = "Neste notebook controla o Dynamic Boost (divide energia entre CPU e GPU): sem ele a placa pode render menos." },
        "PPC" when card.Arch == "Turing" && card.Rtx && !card.Laptop => i with { Default = false, Warn = true, Description = "Sua RTX 20 pode ter porta USB-C (VirtualLink): sem ele, ela para de funcionar." },
        _ => i,
    };
}
