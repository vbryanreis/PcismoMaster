using ResetExplode.Modular;

namespace ResetExplode;

public sealed class NvApiException(string message) : Exception(message);

/// <summary>Uma leitura ao vivo da GPU para o monitoramento da aba Overclock.</summary>
public sealed record OcSample(DateTime Time, uint? CoreMhz, uint? MemMhz, uint? TempC, double? PowerW, double? PowerLimitW,
    uint? FanPercent, int? CoreMv, uint? UsagePercent, ulong? Limiters);

/// <summary>
/// Overclock de placas NVIDIA no estilo do MSI Afterburner. NVAPI (camada portada do simple-nvidia-undervolt)
/// cuida da curva V/F, dos offsets de clock e memória e da tensão; NVML cuida de energia, temperatura e fans.
/// Tudo exige administrador e volta ao padrão ao reiniciar o PC (salvo quando o perfil é reaplicado ao abrir o app).
/// </summary>
public sealed class GpuOc
{
    private static bool _nvapiReady;
    public IntPtr NvApiGpu { get; }
    public IntPtr NvmlGpu { get; }
    public bool CurveSupported { get; }
    public (int Min, int Max) CoreRangeMhz { get; } = (-1000, 1000);
    public (int Min, int Max) MemRangeMhz { get; } = (-1000, 3000);
    public (uint MinW, uint MaxW, uint DefaultW)? Power { get; }
    public (int Min, int Max, int Current)? TempLimit { get; }
    public uint Fans { get; }
    public (uint Min, uint Max) FanRange { get; } = (0, 100);
    public string? Problem { get; }

    private GpuOc(IntPtr nvapi, IntPtr nvml)
    {
        NvApiGpu = nvapi; NvmlGpu = nvml;
        if (nvml != IntPtr.Zero)
        {
            if (Nvml.PowerConstraintsMw(nvml) is { } pc && Nvml.PowerDefaultMw(nvml) is uint def)
                Power = (pc.Min / 1000, pc.Max / 1000, def / 1000);
            if (Nvml.TempThreshold(nvml, Nvml.TempThresholdAcousticMin) is uint tmin
                && Nvml.TempThreshold(nvml, Nvml.TempThresholdAcousticMax) is uint tmax
                && Nvml.TempThreshold(nvml, Nvml.TempThresholdAcousticCurr) is uint tcur && tmax > tmin)
                TempLimit = ((int)tmin, (int)tmax, (int)tcur);
            Fans = Nvml.FanCount(nvml);
            if (Nvml.FanRange(nvml) is { } fr) FanRange = fr;
        }
        if (nvapi == IntPtr.Zero) { Problem = "NVAPI indisponível: offsets de clock, memória e curva V/F desativados."; return; }
        try
        {
            var p0 = NvApi.GetPstates20(nvapi);
            if (P0Clock(p0, NvApi.CLOCK_DOMAIN_GRAPHICS) is { } g && g.FreqDeltaKhz.ValueMax > 0)
                CoreRangeMhz = (g.FreqDeltaKhz.ValueMin / 1000, g.FreqDeltaKhz.ValueMax / 1000);
            if (P0Clock(p0, NvApi.CLOCK_DOMAIN_MEMORY) is { } m && m.FreqDeltaKhz.ValueMax > 0)
                MemRangeMhz = (m.FreqDeltaKhz.ValueMin / 1000, m.FreqDeltaKhz.ValueMax / 1000);
        }
        catch (Exception) { }
        try { CurveSupported = VoltsPlausible(NvApi.GetVfCurve(nvapi)); }
        catch (Exception ex) { Problem = ex.Message; }
        if (!CurveSupported) Problem ??= "A curva V/F desta placa não foi reconhecida; o editor de curva fica desativado.";
    }

    /// <summary>Abre a GPU NVIDIA correspondente ao handle NVML (pelo ID PCI). Null se não houver NVIDIA.</summary>
    public static GpuOc? Open(IntPtr nvmlGpu, uint pciDeviceId)
    {
        IntPtr nvapi = IntPtr.Zero;
        try
        {
            if (!_nvapiReady) { NvApi.Initialize(); _nvapiReady = true; }
            var gpus = NvApi.EnumeratePhysicalGpus();
            nvapi = gpus.FirstOrDefault(g => { try { return NvApi.GetPciIdentifiers(g).DeviceId == pciDeviceId; } catch { return false; } });
            if (nvapi == IntPtr.Zero && gpus.Length == 1) nvapi = gpus[0];
        }
        catch (Exception) { }
        return nvmlGpu == IntPtr.Zero && nvapi == IntPtr.Zero ? null : new GpuOc(nvapi, nvmlGpu);
    }

    // ---------------- Leitura ----------------

    /// <summary>Curva de fábrica e curva atual (ambas em (mV, MHz)). Repete por até 3 s se o driver estiver trocando de estado.</summary>
    public (IReadOnlyList<(int Mv, int Mhz)> Stock, IReadOnlyList<(int Mv, int Mhz)> Live)? ReadCurve()
    {
        if (!CurveSupported) return null;
        for (int waited = 0; ; waited += 50)
        {
            var live = NvApi.GetVfCurve(NvApiGpu);
            var deltas = NvApi.GetCurveFreqDeltasKhz(NvApiGpu, live.Count);
            var stock = VfCurveMath.SubtractDeltas(live, deltas);
            if (VfCurveMath.Readable(stock)) { if (deltas.All(d => d == 0)) StockCurve = stock; return (StockCurve ?? stock, live); }
            // Com offsets ativos o driver remodela a curva (pontos baixos presos no piso), e "atual menos deltas"
            // não volta à de fábrica: usa a de fábrica lida antes, sem esperar.
            if (StockCurve != null && StockCurve.Count == live.Count) return (StockCurve, live);
            if (waited >= 3000) return null;
            Thread.Sleep(50);
        }
    }

    /// <summary>Curva de fábrica lida com a tabela zerada (a referência confiável para calcular os deltas).</summary>
    public IReadOnlyList<(int Mv, int Mhz)>? StockCurve { get; set; }

    /// <summary>Offsets aplicados hoje (para mostrar nos sliders ao abrir a aba).</summary>
    public (int CoreMhz, int MemMhz, int VoltagePercent) ReadOffsets()
    {
        var a = ReadApplied();
        return (a.CoreMhz, a.MemMhz, a.VoltagePercent);
    }

    /// <summary>O ajuste que está na placa agora, seja do PcismoMaster ou do Afterburner: offset do core (o delta mais
    /// comum abaixo do teto), teto de undervolt (onde a curva efetiva fica plana até o fim), memória e tensão.</summary>
    public (int CoreMhz, int MemMhz, int VoltagePercent, int? CapMv, int? CapMhz) ReadApplied()
    {
        int core = 0, mem = 0, volt = 0;
        int? capMv = null, capMhz = null;
        try
        {
            mem = EffectiveMemOffsetMhz() ?? 0;
            volt = (int)NvApi.GetCoreVoltageBoostPercent(NvApiGpu);
            if (CurveSupported)
            {
                var live = NvApi.GetVfCurve(NvApiGpu);
                var d = NvApi.GetCurveFreqDeltasKhz(NvApiGpu, live.Count);
                int end = live.Count;
                if (VfCurveMath.FlatStart(live) is int f && d.Skip(f + 1).Any(x => x != 0))
                {
                    capMv = live[f].Mv; capMhz = live[f].Mhz; end = f;
                }
                core = d.Skip(1).Take(Math.Max(1, end - 1)).GroupBy(x => (int)Math.Round(x / 1000.0))
                    .OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? 0;
            }
        }
        catch (Exception) { }
        return (core, mem, volt, capMv, capMhz);
    }

    /// <summary>Offset de memória que está valendo de fato: clock absoluto da P0 menos o clock base de fábrica
    /// (o campo de delta guarda o que foi pedido, mesmo quando o driver não ativou).</summary>
    public int? EffectiveMemOffsetMhz()
    {
        try
        {
            uint baseKhz = NvApi.GetClockFrequencyKhz(NvApiGpu, NvApi.CLOCK_FREQ_TYPE_BASE, NvApi.CLOCK_DOMAIN_MEMORY);
            return P0Clock(NvApi.GetPstates20(NvApiGpu), NvApi.CLOCK_DOMAIN_MEMORY) is { Data0: > 0 } e && baseKhz > 0
                ? ((int)e.Data0 - (int)baseKhz) / 1000 : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Uma operação por vez na placa: APLICAR, REDEFINIR e a leitura da curva rodam fora da thread da janela,
    /// e o monitoramento de 1 s pula a amostra enquanto uma delas está em andamento.</summary>
    public object Gate { get; } = new();

    public OcSample Sample()
    {
        IntPtr h = NvmlGpu;
        int? mv = null;
        if (NvApiGpu != IntPtr.Zero) try { mv = (int)(NvApi.GetCoreVoltageUv(NvApiGpu) / 1000); } catch (Exception) { }
        return new OcSample(DateTime.Now, Nvml.Clock(h, Nvml.ClockGraphics), Nvml.Clock(h, Nvml.ClockMem), Nvml.Temperature(h),
            Nvml.PowerUsageMw(h) / 1000.0, Nvml.PowerLimitMw(h) / 1000.0, Fans > 0 ? Nvml.FanPercent(h, 0) : null, mv,
            Nvml.Util(h)?.Gpu, Nvml.ThrottleReasons(h));
    }

    /// <summary>O que está segurando o clock agora, em português (motivos de limitação da NVML).</summary>
    public static string Limiter(ulong? reasons)
    {
        if (reasons is not ulong r) return "—";
        var list = new List<string>();
        if ((r & 0x4) != 0) list.Add("energia");
        if ((r & 0x20) != 0 || (r & 0x40) != 0) list.Add("temperatura");
        if ((r & 0x8) != 0 || (r & 0x80) != 0) list.Add("hardware");
        if ((r & 0x1) != 0 && list.Count == 0) list.Add("ociosa");
        if (list.Count == 0) list.Add("tensão / nenhum");
        return string.Join(", ", list);
    }

    // ---------------- Escrita ----------------

    /// <summary>Aplica o perfil inteiro: tensão, limpeza da tabela da curva, offset de memória da P0 e por último
    /// os pontos da curva. A tabela da curva também guarda a entrada da memória (depois dos pontos do core), então
    /// zerá-la inteira depois de gravar a memória desfaz o offset; e regravar a P0 apaga os deltas da curva.</summary>
    public List<string> Apply(OcProfile p)
    {
        var report = new List<string>();
        if (NvApiGpu != IntPtr.Zero)
        {
            int mem = Math.Clamp(p.MemOffsetMhz, MemRangeMhz.Min, MemRangeMhz.Max);
            Step(report, "Core Voltage", () => NvApi.SetCoreVoltageBoostPercent(NvApiGpu, (uint)Math.Clamp(p.VoltagePercent, 0, 100)), $"{p.VoltagePercent}%");
            if (CurveSupported)
            {
                try { NvApi.SetCurveFreqDeltasKhz(NvApiGpu, new int[NvApi.MaxCurveAnchors]); } catch (Exception) { }
            }
            Step(report, "Memory Clock", () => NvApi.SetPstate0Offsets(NvApiGpu, 0, mem * 1000, 0), $"{mem:+0;-0;0} MHz");
            int memLine = report.Count - 1;
            if (CurveSupported)
            {
                // Só os pontos do core: a gravação lê a tabela e troca apenas esses índices, preservando a memória.
                string what = p.UvCapMv is int cap ? $"Core {p.CoreOffsetMhz:+0;-0;0} MHz, undervolt {cap} mV" : $"Core {p.CoreOffsetMhz:+0;-0;0} MHz";
                Step(report, "Curva V/F", () =>
                {
                    var curve = ReadCurve() ?? throw new NvApiException("a curva não pôde ser lida (tente de novo em instantes)");
                    int[] deltas = VfCurveMath.DeltasKhz(curve.Stock, p);
                    NvApi.SetCurveFreqDeltasKhz(NvApiGpu, deltas);
                    int[] back = NvApi.GetCurveFreqDeltasKhz(NvApiGpu, deltas.Length);
                    int off = deltas.Select((d, i) => Math.Abs(d - back[i])).Max();
                    if (off > 1000) throw new NvApiException($"o driver não manteve os pontos gravados (diferença de até {off / 1000} MHz)");
                }, what + " (confirmado no driver)");
            }
            // Sem curva V/F o offset do core vai na P0. Com curva ele já está nos pontos (como o "Curve" do Afterburner):
            // gravá-lo também na P0 somaria duas vezes e subiria a parte plana depois do teto, desfazendo o undervolt.
            else if (p.CoreOffsetMhz != 0)
            {
                Step(report, "Core Clock", () => NvApi.SetPstate0Offsets(NvApiGpu, p.CoreOffsetMhz * 1000, mem * 1000, 0), $"{p.CoreOffsetMhz:+0;-0;0} MHz");
            }
            if (EffectiveMemOffsetMhz() is int got && Math.Abs(got - mem) > 1)
                report[memLine] = $"Memory Clock: o driver aceitou {mem:+0;-0;0} MHz, mas o clock efetivo ficou em {got:+0;-0;0} MHz.";
        }
        if (Power is { } pw)
        {
            uint w = (uint)Math.Clamp(pw.DefaultW * p.PowerPercent / 100.0, pw.MinW, pw.MaxW);
            NvmlStep(report, "Power Limit", Nvml.SetPowerLimitMw(NvmlGpu, w * 1000), $"{p.PowerPercent}% ({w} W)");
        }
        if (TempLimit is { } tl && p.TempLimitC is int t)
            NvmlStep(report, "Temp. Limit", Nvml.SetTempThreshold(NvmlGpu, Nvml.TempThresholdAcousticCurr, Math.Clamp(t, tl.Min, tl.Max)), $"{t} °C");
        return report;
    }

    /// <summary>Volta tudo ao padrão de fábrica (curva, offsets, tensão, energia, temperatura e fans no automático).</summary>
    public List<string> Reset()
    {
        var report = new List<string>();
        if (NvApiGpu != IntPtr.Zero)
        {
            Step(report, "Curva e offsets", () =>
            {
                NvApi.SetCoreVoltageBoostPercent(NvApiGpu, 0);
                NvApi.SetPstate0Offsets(NvApiGpu, 0, 0, 0);
                if (CurveSupported) NvApi.SetCurveFreqDeltasKhz(NvApiGpu, new int[NvApi.MaxCurveAnchors]);
            }, "padrão");
        }
        if (Power is { } pw) NvmlStep(report, "Power Limit", Nvml.SetPowerLimitMw(NvmlGpu, pw.DefaultW * 1000), "100%");
        if (TempLimit is { } tl) NvmlStep(report, "Temp. Limit", Nvml.SetTempThreshold(NvmlGpu, Nvml.TempThresholdAcousticCurr, tl.Current), $"{tl.Current} °C");
        FansAuto();
        report.Add("Fans: automático.");
        return report;
    }

    public int SetFans(uint percent)
    {
        int code = 0;
        for (uint f = 0; f < Fans; f++) code = Math.Max(code, Nvml.SetFanPercent(NvmlGpu, f, Math.Clamp(percent, FanRange.Min, FanRange.Max)));
        return code;
    }

    public void FansAuto() { for (uint f = 0; f < Fans; f++) Nvml.SetFanAuto(NvmlGpu, f); }

    private static void Step(List<string> report, string what, Action action, string value)
    {
        try { action(); report.Add($"{what}: {value}."); }
        catch (Exception ex) { report.Add($"{what}: não aplicado ({ex.Message})."); }
    }

    private static void NvmlStep(List<string> report, string what, int code, string value)
        => report.Add(ResetExplode.Nvml.Explain(code) is { } err ? $"{what}: não aplicado ({err})." : $"{what}: {value}.");

    private static bool VoltsPlausible(IReadOnlyList<(int Mv, int Mhz)> c)
        => c.Count >= 16 && c.Zip(c.Skip(1)).All(x => x.Second.Mv >= x.First.Mv) && c[^1].Mv - c[0].Mv >= 200 && c[^1].Mv >= 850;

    private static Pstate20ClockEntry? P0Clock(Pstates20InfoV1 info, uint domain)
    {
        for (int i = 0; i < Math.Min(info.NumPstates, (uint)Pstates20InfoV1.MaxPstates); i++)
            if (info.Pstates[i].PstateId == 0)
                foreach (var c in info.Pstates[i].Clocks.Take((int)Math.Min(info.NumClocks, (uint)Pstate20.MaxClocks)))
                    if (c.DomainId == domain) return c;
        return null;
    }
}
