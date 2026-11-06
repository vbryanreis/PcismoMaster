using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ResetExplode;

public partial class App : Application
{
    // Uma única instância: abrir o PcismoMaster de novo só traz a janela que já está aberta para a frente.
    private static Mutex? _single;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ajudante sem janela: aplica o perfil global NVIDIA num processo novo. Usado logo depois de instalar o driver,
        // quando a NVAPI do processo aberto ainda não enxerga a placa (o app foi aberto sem driver).
        if (e.Args.Length >= 2 && e.Args[0] == "--apply-drs")
        {
            int code = 0;
            foreach (var id in e.Args.Skip(1))
            {
                try { GraphicsTweaks.ApplyDrsById(id); }
                catch (Exception ex) { code = 1; DriverClean.Log($"Perfil NVIDIA ({id}) no processo ajudante: {ex.Message}"); }
            }
            Shutdown(code);
            return;
        }

        // Depois de uma atualização: espera a versão antiga fechar (ela ainda segura o mutex) e apaga o .exe antigo.
        bool afterUpdate = e.Args.Length >= 2 && e.Args[0] == "--after-update";
        if (afterUpdate && int.TryParse(e.Args[1], out int oldPid))
        {
            try { using var old = System.Diagnostics.Process.GetProcessById(oldPid); old.WaitForExit(20000); } catch { }
        }
        AppUpdater.CleanupOld();

        bool first;
        try { _single = new Mutex(true, @"Local\PcismoMaster.SingleInstance", out first); }
        catch (UnauthorizedAccessException) { first = false; } // criado por outra instância com outro nível de permissão
        // Recém-atualizado: a versão antiga pode ainda estar fechando; espera ela soltar a instância única.
        if (!first && afterUpdate && _single != null)
        {
            try { first = _single.WaitOne(TimeSpan.FromSeconds(20)); }
            catch (AbandonedMutexException) { first = true; }
        }

        if (!first)
        {
            IntPtr h = FindWindow(null, "PcismoMaster");
            if (h != IntPtr.Zero)
            {
                if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
                SetForegroundWindow(h);
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
