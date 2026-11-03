using System.Globalization;
using System.Xml.Linq;

namespace ResetExplode;

/// <summary>De onde vem o valor que o jogo usa.</summary>
public enum DrsSource
{
    User,      // alterado por você neste perfil
    Nvidia,    // predefinido pela NVIDIA para este jogo
    Global,    // herdado do perfil global
    Default,   // padrão do driver (ninguém definiu)
}

/// <summary>Uma configuração de um perfil, pronta para a tela.</summary>
public sealed record DrsItem(uint Id, string Name, string Group, DrsMeta? Meta, DrsType Type, DrsSource Source, string ValueText, ulong? Number);

/// <summary>Um perfil carregado: aplicativos e configurações.</summary>
public sealed record DrsProfileData(string Name, bool IsGlobal, List<DrsApp> Apps, List<DrsItem> Items);

/// <summary>Alteração pendente: novo valor (número ou texto) ou "voltar ao herdado" (Reset).</summary>
public sealed record DrsChange(uint Id, DrsType Type, ulong Number, string? Text, bool Reset);

/// <summary>
/// Operações do Profile Inspector sobre o banco de perfis do driver NVIDIA (NvDrs + DrsCatalog).
/// Tudo é lento o bastante para rodar fora da thread da janela; cada chamada abre sua própria sessão.
/// Exporta e importa no formato .nip do nvidiaProfileInspector (compatível com os arquivos dele).
/// </summary>
public static class ProfileInspector
{
    public const string GlobalLabel = "Perfil global";

    /// <summary>Nomes de todos os perfis; o global vem primeiro. Devolve também o nome real do perfil base.</summary>
    public static (string BaseName, List<(string Name, bool Predefined, int Apps)> Profiles) LoadProfiles()
        => NvDrs.Session(s =>
        {
            string baseName = NvDrs.ProfileInfo(s, NvDrs.BaseProfile(s)).Name;
            var all = NvDrs.Profiles(s).Where(p => p.Name != baseName).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            return (baseName, all);
        });

    private static IntPtr Handle(IntPtr s, string profile, string baseName) => profile == baseName ? NvDrs.BaseProfile(s) : NvDrs.FindProfile(s, profile);

    /// <summary>
    /// Lê o perfil: o valor efetivo de cada configuração do catálogo (e das que o perfil tem fora dele).
    /// <paramref name="everything"/> inclui as configurações escondidas do catálogo e todas as que o driver conhece sem nome.
    /// </summary>
    public static DrsProfileData Load(string profile, string baseName, bool everything)
    {
        var catalog = DrsCatalog.All;
        return NvDrs.Session(s =>
        {
            IntPtr p = Handle(s, profile, baseName);
            var apps = profile == baseName ? new List<DrsApp>() : NvDrs.Apps(s, p);
            var ids = catalog.Values.Where(m => everything || !m.Hidden).Select(m => m.Id).ToHashSet();
            foreach (var v in NvDrs.ProfileSettings(s, p)) ids.Add(v.Id);        // o que o perfil tem, mesmo sem nome
            if (everything) try { foreach (var id in NvDrs.AvailableIds()) ids.Add(id); } catch { }

            var items = new List<DrsItem>(ids.Count);
            foreach (uint id in ids)
            {
                catalog.TryGetValue(id, out var meta);
                DrsValue? v;
                try { v = NvDrs.Get(s, p, id); } catch { continue; }
                string name = meta?.Name ?? NvDrs.NameFromId(id) ?? $"0x{id:X8}";
                string group = meta?.Group ?? "99 - Sem nome no catálogo";
                if (v == null)
                {
                    ulong? def = meta?.Default;
                    items.Add(new DrsItem(id, name, group, meta, meta?.Choices.Count > 0 || def != null ? DrsType.Dword : DrsType.Dword, DrsSource.Default,
                        def is ulong d ? DrsCatalog.ValueName(meta, d) : "(padrão do driver)", def));
                    continue;
                }
                var source = v.Location == DrsLocation.Current ? (v.IsPredefined ? DrsSource.Nvidia : DrsSource.User) : DrsSource.Global;
                string text = v.Type is DrsType.Dword or DrsType.Qword ? DrsCatalog.ValueName(meta, v.Number) : v.Text ?? "";
                items.Add(new DrsItem(id, name, group, meta, v.Type, source, text, v.Type is DrsType.Dword or DrsType.Qword ? v.Number : null));
            }
            return new DrsProfileData(profile, profile == baseName, apps,
                items.OrderBy(i => i.Group).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
        });
    }

    /// <summary>Grava as alterações no perfil e salva o banco do driver (vale na próxima vez que o jogo abrir).</summary>
    public static void Apply(string profile, string baseName, IEnumerable<DrsChange> changes)
        => NvDrs.Session(s =>
        {
            IntPtr p = Handle(s, profile, baseName);
            foreach (var c in changes)
            {
                if (c.Reset) NvDrs.Delete(s, p, c.Id);
                else NvDrs.Set(s, p, c.Id, c.Type, c.Number, c.Text);
            }
            return 0;
        }, save: true);

    public static void CreateProfile(string name, string? exe)
        => NvDrs.Session(s => { var p = NvDrs.CreateProfile(s, name); if (!string.IsNullOrWhiteSpace(exe)) NvDrs.AddApp(s, p, exe); return 0; }, save: true);

    public static void DeleteProfile(string name) => NvDrs.Session(s => { NvDrs.DeleteProfile(s, NvDrs.FindProfile(s, name)); return 0; }, save: true);

    public static void RestoreProfile(string name, string baseName)
        => NvDrs.Session(s => { NvDrs.RestoreProfile(s, Handle(s, name, baseName)); return 0; }, save: true);

    public static void AddApp(string profile, string exe) => NvDrs.Session(s => { NvDrs.AddApp(s, NvDrs.FindProfile(s, profile), exe); return 0; }, save: true);

    public static void RemoveApp(string profile, string exe) => NvDrs.Session(s => { NvDrs.RemoveApp(s, NvDrs.FindProfile(s, profile), exe); return 0; }, save: true);

    // ---------------- .nip (formato do nvidiaProfileInspector) ----------------

    /// <summary>Exporta os perfis com o que foi alterado pelo usuário (executáveis + configurações próprias).</summary>
    public static void Export(IEnumerable<string> profiles, string baseName, string path)
    {
        var root = new XElement("ArrayOfProfile");
        NvDrs.Session(s =>
        {
            foreach (var name in profiles)
            {
                IntPtr p = Handle(s, name, baseName);
                var settings = NvDrs.ProfileSettings(s, p).Where(v => v.Location == DrsLocation.Current && !v.IsPredefined);
                root.Add(new XElement("Profile",
                    new XElement("ProfileName", name == baseName ? "Base Profile" : name),
                    new XElement("Executeables", (name == baseName ? new List<DrsApp>() : NvDrs.Apps(s, p)).Select(a => new XElement("string", a.Name))),
                    new XElement("Settings", settings.Select(v => new XElement("ProfileSetting",
                        new XElement("SettingNameInfo", DrsCatalog.All.TryGetValue(v.Id, out var m) ? m.Name : NvDrs.NameFromId(v.Id) ?? ""),
                        new XElement("SettingID", v.Id),
                        new XElement("SettingValue", v.Type is DrsType.Dword or DrsType.Qword ? v.Number.ToString(CultureInfo.InvariantCulture) : v.Text ?? ""),
                        new XElement("ValueType", v.Type switch { DrsType.Qword => "Qword", DrsType.WString => "String", DrsType.String => "AnsiString", DrsType.Binary => "Binary", _ => "Dword" }))))));
            }
            return 0;
        });
        new XDocument(new XDeclaration("1.0", "utf-16", null), root).Save(path);
    }

    /// <summary>Importa um .nip: cria os perfis que faltarem, liga os executáveis e grava as configurações.</summary>
    public static int Import(string path, string baseName)
    {
        var doc = XDocument.Load(path);
        int count = 0;
        NvDrs.Session(s =>
        {
            foreach (var prof in doc.Root?.Elements("Profile") ?? Enumerable.Empty<XElement>())
            {
                string name = prof.Element("ProfileName")?.Value ?? "";
                if (name.Length == 0) continue;
                IntPtr p;
                if (name == "Base Profile" || name == baseName) p = NvDrs.BaseProfile(s);
                else
                {
                    try { p = NvDrs.FindProfile(s, name); }
                    catch (DrsException e) when (e.Status == NvDrs.ProfileNotFound) { p = NvDrs.CreateProfile(s, name); }
                    foreach (var exe in prof.Element("Executeables")?.Elements("string") ?? Enumerable.Empty<XElement>())
                        try { NvDrs.AddApp(s, p, exe.Value); } catch (DrsException) { } // já ligado a este ou a outro perfil
                }
                foreach (var set in prof.Element("Settings")?.Elements("ProfileSetting") ?? Enumerable.Empty<XElement>())
                {
                    if (!uint.TryParse(set.Element("SettingID")?.Value, out uint id)) continue;
                    string value = set.Element("SettingValue")?.Value ?? "0";
                    var type = (set.Element("ValueType")?.Value ?? "Dword") switch
                    { "Qword" => DrsType.Qword, "String" => DrsType.WString, "AnsiString" => DrsType.String, "Binary" => DrsType.Binary, _ => DrsType.Dword };
                    if (type == DrsType.Binary) continue;
                    ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong n);
                    NvDrs.Set(s, p, id, type, n, value);
                }
                count++;
            }
            return 0;
        }, save: true);
        return count;
    }
}
