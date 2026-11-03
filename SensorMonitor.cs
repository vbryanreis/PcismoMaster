using System.Collections.ObjectModel;
using LibreHardwareMonitor.Hardware;

namespace ResetExplode;

/// <summary>Encapsula o LibreHardwareMonitorLib e mantém a lista de sensores atualizada.</summary>
public sealed class SensorMonitor : IDisposable
{
    private readonly Computer _computer;
    private readonly Dictionary<string, SensorRow> _index = new();
    private volatile bool _open;

    public ObservableCollection<SensorRow> Rows { get; } = new();
    public bool IsOpen => _open;

    public SensorMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            // Controladoras (hubs de fan/RGB via HID): enumerar a cada segundo custa CPU e disputa o dispositivo com
            // o iCUE e similares; a integração de iluminação fala com eles direto.
            IsControllerEnabled = false
        };
    }

    /// <summary>Detecta o hardware (leva de 0,5 a alguns segundos) fora da thread da janela. Até terminar, Refresh não faz nada.</summary>
    public Task OpenAsync() => Task.Run(() => { _computer.Open(); _open = true; });

    private readonly record struct Reading(IHardware Hw, ISensor Sensor, float? Value, float? Min, float? Max);
    private readonly Dictionary<ISensor, SensorRow> _bySensor = new();
    private int _busy;

    /// <summary>Lê o hardware fora da thread da janela (as leituras do LHM levam de alguns a dezenas de ms) e depois
    /// aplica os valores na tela de uma vez. Se a leitura anterior ainda não terminou, esta é pulada.</summary>
    public async Task RefreshAsync()
    {
        if (!_open || Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var readings = await Task.Run(Collect);
            foreach (var r in readings) Apply(r);
        }
        finally { Volatile.Write(ref _busy, 0); }
    }

    /// <summary>Com uma página de detalhes aberta (Sensores, Sistema, Discos, Rede) tudo é lido a cada segundo. Fora delas, discos (SMART)
    /// e rede, que ninguém está vendo, são lidos a cada <see cref="SlowEvery"/> leituras: o painel e a barra lateral só usam CPU, GPU e memória.</summary>
    public bool FullDetail { get; set; }
    public const int SlowEvery = 10;
    private int _tick;

    /// <summary>Se este tipo de hardware é lido nesta passada (ver <see cref="FullDetail"/>).</summary>
    public static bool ShouldUpdate(HardwareType type, bool fullDetail, int tick) =>
        fullDetail || tick % SlowEvery == 0 || type is not (HardwareType.Storage or HardwareType.Network);

    private List<Reading> Collect()
    {
        var list = new List<Reading>(_bySensor.Count + 32);
        int tick = _tick++;
        bool full = FullDetail;
        foreach (var hw in _computer.Hardware) Walk(hw, list, ShouldUpdate(hw.HardwareType, full, tick));
        return list;
    }

    private static void Walk(IHardware hw, List<Reading> list, bool update)
    {
        if (update) hw.Update();
        foreach (var s in hw.Sensors) list.Add(new Reading(hw, s, s.Value, s.Min, s.Max));
        foreach (var sub in hw.SubHardware) Walk(sub, list, update);
    }

    private void Apply(Reading r)
    {
        if (!_bySensor.TryGetValue(r.Sensor, out var row))
        {
            string id = r.Sensor.Identifier.ToString();
            if (!_index.TryGetValue(id, out row))
            {
                row = new SensorRow
                {
                    Id = id,
                    Hardware = r.Hw.Name,
                    Category = SensorRow.CategoryName(r.Sensor.SensorType),
                    Name = r.Sensor.Name,
                    Type = r.Sensor.SensorType,
                    HardwareType = r.Hw.HardwareType,
                    HwIndex = _hwOrder.TryGetValue(r.Hw.Name, out int hi) ? hi : (_hwOrder[r.Hw.Name] = _hwOrder.Count),
                    Index = _index.Count
                };
                _index[id] = row;
                Rows.Add(row);
            }
            _bySensor[r.Sensor] = row;
        }
        row.Update(r.Value);
    }

    private readonly Dictionary<string, int> _hwOrder = new();

    /// <summary>Zera mínimo, máximo e média de todos os sensores.</summary>
    public void ResetStats() { foreach (var row in Rows) row.Reset(); }

    /// <summary>Temperatura principal da CPU: Package (Intel) / Tctl-Tdie (AMD), senão a maior entre os núcleos.</summary>
    public float? CpuTemperature
    {
        get
        {
            var temps = Rows.Where(r => r.HardwareType == HardwareType.Cpu
                                     && r.Type == SensorType.Temperature
                                     && r.RawValue is > 0).ToList();
            var main = temps.FirstOrDefault(r => r.Name.Contains("Package") || r.Name.Contains("Tctl") || r.Name.Contains("Tdie"));
            return main?.RawValue ?? temps.Max(r => r.RawValue);
        }
    }

    /// <summary>Carga total da CPU em %.</summary>
    public float? CpuLoad => Rows.FirstOrDefault(r => r.HardwareType == HardwareType.Cpu
                                                   && r.Type == SensorType.Load
                                                   && r.Name == "CPU Total")?.RawValue;

    /// <summary>Consumo do pacote da CPU em W.</summary>
    public float? CpuPower => Rows.FirstOrDefault(r => r.HardwareType == HardwareType.Cpu
                                                    && r.Type == SensorType.Power
                                                    && r.Name.Contains("Package"))?.RawValue;

    /// <summary>
    /// Driver PawnIO (necessário para temperatura/consumo da CPU via MSR/SMN).
    /// A biblioteca procura exatamente esta chave de desinstalação.
    /// </summary>
    public static bool IsPawnIOInstalled
    {
        get
        {
            using var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                                                                     Microsoft.Win32.RegistryView.Registry64);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key != null;
        }
    }

    /// <summary>Só muda reabrindo o app, então é lido uma vez (antes criava um token do Windows por segundo).</summary>
    public static bool IsElevated { get; } = ReadElevated();

    private static bool ReadElevated()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static bool? _pawnIo;
    private static bool PawnIoCached => _pawnIo ??= IsPawnIOInstalled;

    public enum CpuSensorProblem { None, NotElevated, PawnIOMissing, ReadFailed }

    /// <summary>Por que a temperatura/consumo da CPU não aparecem (o motivo mais provável primeiro).</summary>
    public CpuSensorProblem DiagnoseCpuSensors()
    {
        if (CpuTemperature != null) return CpuSensorProblem.None;
        if (!IsElevated) return CpuSensorProblem.NotElevated;
        if (!PawnIoCached) return CpuSensorProblem.PawnIOMissing;
        return CpuSensorProblem.ReadFailed;
    }

    public static string Explain(CpuSensorProblem p) => p switch
    {
        CpuSensorProblem.NotElevated =>
            "O PcismoMaster não está rodando como administrador, e o Windows só libera os sensores internos da CPU " +
            "nesse modo. Feche e abra de novo aceitando o pedido de administrador.",
        CpuSensorProblem.PawnIOMissing =>
            "Falta o driver PawnIO, que lê a temperatura e o consumo da CPU. Ele é gratuito, assinado e usado também " +
            "pelo LibreHardwareMonitor e pelo FanControl. Depois de instalar, feche e abra o PcismoMaster.",
        CpuSensorProblem.ReadFailed =>
            "O PawnIO está instalado, mas a leitura falhou. Se acabou de instalar, reinicie o PC. Se continuar, " +
            "confira se o antivírus não bloqueou o driver ou se a CPU é muito antiga/recente para a biblioteca de sensores.",
        _ => ""
    };

    public void Dispose()
    {
        if (_open) _computer.Close();
    }
}
