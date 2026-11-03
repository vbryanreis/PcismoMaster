using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ResetExplode;

public sealed record BenchRun(DateTime Date, double? Multi, double? Single, bool Sustained, string Cpu);

/// <summary>Resultado dos benchmarks de jogo (Test = "gpu", "sim" ou "mem"). Score é usado para a barra do histórico.</summary>
public sealed record BenchRecord(DateTime Date, string Test, double Score, string Summary);

/// <summary>Programa extra que abre antes do jogo (ex.: ferramenta de comando/monitoramento) e pode fechar junto com ele.</summary>
public sealed class HelperProgram
{
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public bool CloseWithGame { get; set; } = true;
    public string Display => System.IO.Path.GetFileName(Path) + (string.IsNullOrWhiteSpace(Args) ? "" : " " + Args);
}

/// <summary>Perfil de um jogo na aba RESET JOGOS: desempenho, argumentos de gráficos e programas auxiliares.</summary>
public sealed class GameProfile
{
    public string Priority { get; set; } = "normal";   // normal | above | high
    public bool HighPerfPlan { get; set; }
    public bool GpuHigh { get; set; }
    public bool DisableFso { get; set; }
    public string? ExePath { get; set; }
    public string WinMode { get; set; } = "default";   // default | full | borderless | window
    public string Res { get; set; } = "default";       // default | 1280x720 | 1920x1080 | 2560x1440 | 3840x2160
    public int Hz { get; set; }                        // 0 = não definir
    public string Style { get; set; } = "source";      // source | unity | unreal
    public string Extra { get; set; } = "";
    public List<HelperProgram> Helpers { get; set; } = new();

    // Núcleos da CPU para o processo do jogo (afinidade), aplicados quando o jogo abre por "Preparar e jogar"
    public bool FreeCore0 { get; set; }      // deixa o núcleo 0 para o Windows e os drivers
    public bool PhysicalOnly { get; set; }   // uma thread por núcleo físico (sem SMT/Hyper-Threading)
    public bool PCoresOnly { get; set; }     // só núcleos de desempenho (sem E-cores) em CPUs híbridas

    /// <summary>Cópia para a janela de ajustes: só vira o perfil do jogo ao clicar em "Salvar ajustes".</summary>
    public GameProfile Clone()
    {
        var c = (GameProfile)MemberwiseClone();
        c.Helpers = Helpers.Select(h => new HelperProgram { Path = h.Path, Args = h.Args, CloseWithGame = h.CloseWithGame }).ToList();
        return c;
    }
}

/// <summary>Jogo adicionado à mão (qualquer .exe).</summary>
public sealed class CustomGame
{
    public string Name { get; set; } = "";
    public string Exe { get; set; } = "";
    public string Args { get; set; } = "";
}

/// <summary>Configurações em %AppData%\ResetExplode. A chave da API é cifrada com DPAPI (só este usuário do Windows a lê).</summary>
public sealed class AppSettings
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcismoMaster");
    private static string FilePath => Path.Combine(Folder, "settings.json");

    public string? HowManyFpsKeyProtected { get; set; }

    // RESET BENCH: últimos resultados (mais recente primeiro)
    public List<BenchRun> BenchRuns { get; set; } = new();
    public List<BenchRecord> BenchRecords { get; set; } = new();

    // RESET OTIMIZA: valores originais dos ajustes do Windows antes da primeira alteração ("<ausente>" = não existia)
    public Dictionary<string, string?> TweakBackup { get; set; } = new();
    /// <summary>ComDB original (reservas de números COM) antes da primeira "liberação".</summary>
    public byte[]? ComDbBackup { get; set; }
    /// <summary>Programas de inicialização removidos, para "Restaurar removidos".</summary>
    public List<StartupBackup> StartupRemoved { get; set; } = new();

    // RESET JOGOS
    public Dictionary<string, GameProfile> GameProfiles { get; set; } = new();
    public List<CustomGame> CustomGames { get; set; } = new();

    // Aparência
    public bool LightTheme { get; set; }

    // Overclock (aba no estilo Afterburner): perfil em edição, 5 perfis salvos e o último aplicado (reaplicado ao abrir o app, se marcado)
    public Modular.OcProfile OcCurrent { get; set; } = new();
    public Modular.OcProfile?[] OcProfiles { get; set; } = new Modular.OcProfile?[5];
    public Modular.OcProfile? OcApplied { get; set; }
    public bool OcApplyOnStart { get; set; }

    // Jogos: biblioteca em lista (true) ou em cartões com capa (false)
    public bool LibListView { get; set; }

    // Limpeza com o DDU: valor do Windows Update antes de bloquear (para desfazer) e o driver baixado antes da limpeza
    public bool DduWuBlocked { get; set; }
    public int? DduWuBackup { get; set; }
    public string? DduDriverFile { get; set; }

    // Manutenção: quando foi a última revisão semanal concluída sem erros
    public DateTime? MaintWeeklyLast { get; set; }
    /// <summary>Curva V/F de fábrica (mV, MHz) lida sem offsets; serve de referência quando o app abre com overclock já aplicado.</summary>
    public List<int[]>? OcStockCurve { get; set; }

    // Overlay
    public string OverlayCorner { get; set; } = "TL";
    public double OverlayScale { get; set; } = 1.0;
    /// <summary>"V" = lista vertical com gráfico; "H" = barra horizontal numa linha.</summary>
    public string OverlayLayout { get; set; } = "V";
    /// <summary>Opacidade do fundo do overlay (0 a 1).</summary>
    public double OverlayOpacity { get; set; } = 0.78;
    /// <summary>Atalho que liga/desliga o overlay: modificadores de RegisterHotKey e código virtual (padrão Ctrl + Shift + O).</summary>
    public uint OverlayHotkeyMods { get; set; } = 0x2 | 0x4;
    public uint OverlayHotkeyVk { get; set; } = 0x4F;
    /// <summary>Itens escolhidos para o overlay (chaves de OverlayMetrics); null = padrão.</summary>
    public List<string>? OverlayItems { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] // nunca gravar a chave em texto puro
    public string? HowManyFpsKey
    {
        get
        {
            if (string.IsNullOrEmpty(HowManyFpsKeyProtected)) return null;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(HowManyFpsKeyProtected), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return null; }
        }
        set
        {
            HowManyFpsKeyProtected = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
