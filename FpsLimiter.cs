using System.IO;

namespace ResetExplode;

/// <summary>
/// Limitador de FPS pelo driver NVIDIA ("Taxa Máxima de Quadros" do Painel), sem injetar nada no jogo:
/// global (perfil base), em segundo plano e por jogo (perfil do executável no driver). 0 = desligado.
/// O valor fica gravado no driver: vale mesmo com o PcismoMaster fechado, e desfaz voltando a 0.
/// </summary>
public static class FpsLimiter
{
    public const string ProfilePrefix = "PcismoMaster - ";

    public static bool Available => NvDrs.Available;

    /// <summary>Limite global e limite em segundo plano (0 = desligado).</summary>
    public static (uint Global, uint Background) ReadGlobal() => NvDrs.Session(s =>
    {
        IntPtr p = NvDrs.BaseProfile(s);
        return (Value(s, p, NvidiaPanel.MaxFps, false), Value(s, p, NvidiaPanel.BgFps, false));
    });

    public static void SetGlobal(uint fps) => Write(null, NvidiaPanel.MaxFps, fps);
    public static void SetBackground(uint fps) => Write(null, NvidiaPanel.BgFps, fps);

    /// <summary>Grava o limite no perfil do jogo (cria um perfil "PcismoMaster - jogo" se o driver não conhecer o executável).</summary>
    public static void SetGame(string exe, uint fps) => Write(Exe(exe), NvidiaPanel.MaxFps, fps);

    public static string Exe(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";

    private static uint Value(IntPtr s, IntPtr p, uint id, bool onlyHere)
    {
        try
        {
            var v = NvDrs.Get(s, p, id);
            if (v == null || (onlyHere && (v.Location != DrsLocation.Current || v.IsPredefined))) return 0;
            return (uint)v.Number;
        }
        catch (DrsException) { return 0; }
    }

    private static void Write(string? exe, uint id, uint fps) => NvDrs.Session(s =>
    {
        IntPtr p;
        if (exe == null) p = NvDrs.BaseProfile(s);
        else if (NvDrs.FindAppProfile(s, exe) is IntPtr found) p = found;
        else
        {
            if (fps == 0) return 0; // nada para tirar
            string name = ProfilePrefix + Path.GetFileNameWithoutExtension(exe);
            try { p = NvDrs.FindProfile(s, name); } catch (DrsException) { p = NvDrs.CreateProfile(s, name); }
            NvDrs.AddApp(s, p, exe);
        }
        if (fps == 0) NvDrs.Delete(s, p, id);
        else NvDrs.Set(s, p, id, DrsType.Dword, Math.Clamp(fps, 10u, 1000u), null);
        return 0;
    }, save: true);
}
