using System.IO;
using System.Text.Json;

namespace ResetExplode.Modular;

/// <summary>Valor de registro de um ajuste: DWORD (Value), texto (Text), binário em hexadecimal (Hex, ex.: "3000"), apagar o valor (Remove)
/// ou qualquer tipo já pronto (Kind + Data no formato do SavedValue: QWORD, texto expansível, multi-texto… vindos de um .reg).</summary>
public sealed record Setting(string Hive, string Path, string Name, int Value, string? Text = null, string? Hex = null, bool Remove = false, int Kind = 0, string? Data = null);
public sealed record SavedValue(bool Exists, int Kind, string Data);
public sealed record Module(string Id, string Title, string Category, string Description, string Source, Setting[] Settings);
public interface ISettingsStore
{
    SavedValue Read(Setting setting);
    void Write(Setting setting, SavedValue value);
}
public sealed record Snapshot(string ModuleId, Setting[] Settings, SavedValue[] Before);

// Persist the recovery journal BEFORE the first system write. A failed or interrupted
// operation keeps its journal so Restore can be retried after restarting the app.
public sealed class ModuleEngine(ISettingsStore store, string directory)
{
    private readonly object gate = new();
    private string FileFor(Module module) => Path.Combine(directory, module.Id + ".json");
    public bool HasBackup(Module module) => File.Exists(FileFor(module));
    public bool Matches(Module module) => module.Settings.All(s => store.Read(s) == Target(s));
    private static SavedValue Target(Setting s) => s.Remove ? new(false, 0, "")
        : s.Data is not null ? new(true, s.Kind, s.Data)
        : s.Hex is not null ? new(true, 3, JsonSerializer.Serialize(Convert.FromHexString(s.Hex)))
        : s.Text is null ? new(true, 4, JsonSerializer.Serialize(s.Value)) : new(true, 1, JsonSerializer.Serialize(s.Text));
    public string Preview(Module module) => string.Join(Environment.NewLine, module.Settings.Select(s =>
        $"{s.Hive}\\{s.Path}\\{s.Name}\n  Atual: {Describe(store.Read(s))} → Novo: DWORD {s.Value}"));
    private static string Describe(SavedValue value) => value.Exists ? value.Data : "valor não definido";
    public void Enable(Module module)
    {
        lock (gate)
        {
            if (HasBackup(module)) throw new InvalidOperationException("Já existe backup. Restaure antes de aplicar novamente.");
            var snapshot = new Snapshot(module.Id, module.Settings, module.Settings.Select(store.Read).ToArray());
            Directory.CreateDirectory(directory);
            var file = FileFor(module);
            using (var stream = new FileStream(file + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, snapshot);
                stream.Flush(true);
            }
            File.Move(file + ".tmp", file, false);
            try
            {
                foreach (var s in module.Settings) store.Write(s, Target(s));
                if (!Matches(module)) throw new IOException("A verificação dos valores aplicados falhou.");
            }
            catch (Exception error)
            {
                try { Restore(module); }
                catch (Exception recovery) { throw new AggregateException("Falha ao aplicar e restaurar; backup mantido para recuperação.", error, recovery); }
                throw;
            }
        }
    }
    public void Restore(Module module)
    {
        lock (gate)
        {
            if (!HasBackup(module)) throw new InvalidOperationException("Não há backup para restaurar: este ajuste não foi aplicado pelo PcismoMaster.");
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(FileFor(module)))
                ?? throw new IOException("Backup inválido.");
            if (snapshot.ModuleId != module.Id || !snapshot.Settings.SequenceEqual(module.Settings) || snapshot.Before.Length != module.Settings.Length)
                throw new IOException("Backup incompatível. Nenhuma alteração realizada.");
            for (int i = snapshot.Settings.Length - 1; i >= 0; i--)
                store.Write(snapshot.Settings[i], snapshot.Before[i]);
            for (int i = 0; i < snapshot.Settings.Length; i++)
                if (store.Read(snapshot.Settings[i]) != snapshot.Before[i]) throw new IOException("Restauração não confirmada; backup preservado.");
            File.Delete(FileFor(module));
        }
    }
}
