namespace ResetExplode;

/// <summary>
/// IDs do perfil do driver NVIDIA usados pelo limitador de FPS (Desempenho › Overlay): "Taxa máxima de quadros"
/// e a de "aplicativo em segundo plano", com os valores prontos que o Painel de Controle NVIDIA oferece.
/// </summary>
public static class NvidiaPanel
{
    public const uint MaxFps = 0x10835002, BgFps = 0x10835005;

    public static readonly uint[] FpsPresets = { 30, 60, 75, 90, 100, 120, 141, 144, 157, 160, 165, 180, 200, 237, 240, 300, 360 };
}
