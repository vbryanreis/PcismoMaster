namespace ResetExplode;

/// <summary>Uma opção da lista: o que ela grava (uma ou mais configurações do perfil, como o Painel faz com "Modo de latência baixa").</summary>
public sealed record PanelChoice(string Label, (uint Id, uint Value)[] Writes);

/// <summary>
/// Uma linha de "Gerenciar as configurações em 3D" do Painel de Controle NVIDIA.
/// <paramref name="Text"/>: valor em texto (só mostra e permite voltar ao automático); <paramref name="Info"/>: não fica nos perfis (só explica onde mudar);
/// <paramref name="AutoLabel"/>: nome da opção "sem valor" quando o Painel tem um nome próprio para ela (ex.: "Seleção automática").
/// </summary>
public sealed record PanelSetting(string Key, string Title, string Help, uint[] Ids, PanelChoice[] Choices,
    bool Fps = false, bool HybridOnly = false, bool Text = false, bool Info = false, string? AutoLabel = null);

/// <summary>Valor atual de uma linha num perfil: a opção que bate (ou null), se foi definida neste perfil ou pela NVIDIA, e o valor cru.</summary>
public sealed record PanelState(PanelSetting Setting, PanelChoice? Current, bool SetHere, bool Predefined, uint? Raw, string? TextValue = null);

/// <summary>
/// "Gerenciar as configurações em 3D" do Painel de Controle NVIDIA, com os mesmos nomes, opções e ordem do Painel em português,
/// gravado direto nos perfis do driver (o mesmo banco que o Painel e o Profile Inspector usam).
/// Configurações globais = perfil base; de programa = perfil do jogo.
/// </summary>
public static class NvidiaPanel
{
    private static PanelChoice C(string label, uint id, uint value) => new(label, new[] { (id, value) });
    private static PanelSetting S(string key, string title, string help, uint id, params (string Label, uint Value)[] choices)
        => new(key, title, help, new[] { id }, choices.Select(c => C(c.Label, id, c.Value)).ToArray());

    private const uint AnisoMode = 0x10D2BB16, AnisoLevel = 0x101E61A9, TrMs = 0x10FC2D9C, TrSs = 0x10D48A85, AaMode = 0x107EFC5B, AaMethod = 0x10D773D2;
    private const uint LlCpl = 0x0005F543, LlUltra = 0x10835000, Prerender = 0x007BA09E, OptCompat = 0x10F9DC80, OptMode = 0x10F9DC81;
    private const uint Sharpen = 0x00598928, SharpenValue = 0x002ED8CD, SharpenGrain = 0x002ED8CE;
    public const uint MaxFps = 0x10835002, BgFps = 0x10835005, OpenGlGpu = 0x20D0F3E6, CudaExcluded = 0x10354FF8;

    private static PanelChoice Sharp(int pct) => new($"Ligado (nitidez {pct / 100.0:0.00}, ignorar granulação 0,17)",
        new[] { (Sharpen, 1u), (SharpenValue, (uint)pct), (SharpenGrain, 17u) });

    public static readonly PanelSetting[] All =
    {
        new("sharpen", "Nitidez da imagem", "Nitidez do driver (a clássica, que o app ativa em \"Nitidez de imagem clássica\"). A escala de GPU fica no Painel.",
            new[] { Sharpen, SharpenValue, SharpenGrain },
            new[] { new PanelChoice("Nitidez desligada", new[] { (Sharpen, 0u) }), Sharp(30), Sharp(50), Sharp(70), Sharp(100) }),
        S("fxaa", "Anti-aliasing - FXAA", "Suavização rápida por pós-processamento; deixa a imagem um pouco borrada.", 0x1074C972, ("Desligado", 0), ("Ligado", 1)),
        S("aaset", "Anti-aliasing - configuração", "Nível de suavização (MSAA) quando o modo substitui ou melhora a do jogo.",
            AaMethod, ("Nenhum", 0), ("2x", 0x0E), ("4x", 0x10), ("8x", 0x25), ("8xQ", 0x26), ("16x", 0x27)),
        S("aagamma", "Anti-aliasing - correção do gama", "Corrige o brilho das bordas suavizadas (OpenGL).", 0x107D639D, ("Desligado", 0), ("Ligado", 2)),
        new("aamode", "Anti-aliasing - modo", "Deixa o jogo decidir, desliga, substitui ou melhora a suavização do jogo (jogos DirectX 9/OpenGL antigos).",
            new[] { AaMode, AaMethod }, new[]
            {
                C("Controlado pelo aplicativo", AaMode, 0),
                new PanelChoice("Desligado", new[] { (AaMode, 1u), (AaMethod, 0u) }),
                C("Melhorar a configuração do aplicativo", AaMode, 2),
                C("Substituir qualquer configuração do aplicativo", AaMode, 1),
            }),
        new("aatrans", "Anti-aliasing - transparência", "Suaviza folhas, grades e cercas (texturas transparentes). Superamostragem pesa bastante.",
            new[] { TrMs, TrSs }, new[]
            {
                new PanelChoice("Desligado", new[] { (TrMs, 0u), (TrSs, 0u) }), new PanelChoice("Multiamostragem", new[] { (TrMs, 4u), (TrSs, 0u) }),
                new PanelChoice("2x (superamostragem)", new[] { (TrMs, 0u), (TrSs, 0x14u) }), new PanelChoice("4x (superamostragem)", new[] { (TrMs, 0u), (TrSs, 0x24u) }),
                new PanelChoice("8x (superamostragem)", new[] { (TrMs, 0u), (TrSs, 0x34u) }), new PanelChoice("2x (superamostragem de grade esparsa)", new[] { (TrMs, 0u), (TrSs, 0x18u) }),
                new PanelChoice("4x (superamostragem de grade esparsa)", new[] { (TrMs, 0u), (TrSs, 0x28u) }), new PanelChoice("8x (superamostragem de grade esparsa)", new[] { (TrMs, 0u), (TrSs, 0x38u) }),
            }),
        S("triple", "Buffering triplo", "Só para OpenGL com sincronização vertical ligada.", 0x20FDD1F9, ("Desligado", 0), ("Ligado", 1)),
        new("cudagpus", "CUDA - GPUs", "Quais placas os programas CUDA podem usar. Mostrado aqui; para escolher placas, use o Painel de Controle.",
            new[] { CudaExcluded }, Array.Empty<PanelChoice>(), Text: true, AutoLabel: "Todas"),
        S("cudasys", "CUDA - Política de retorno de memória do sistema", "O que fazer quando a memória da placa acaba em CUDA/IA: usar a RAM (mais lento) ou dar erro.",
            0x10ECECC9, ("Padrão do driver", 0), ("Preferir sem retorno de memória do sistema", 1), ("Preferir retorno de memória do sistema", 2)),
        S("gdi", "Compatibilidade com OpenGL e GDI", "Para programas OpenGL antigos que desenham com GDI.", 0x2072C5A3,
            ("Automático", 2), ("Preferência por desempenho", 0), ("Preferência por compatibilidade", 1)),
        new("dsr", "DSR - Fatores e DSR - Suavidade", "O DSR/DLDSR fica na configuração do monitor, não nos perfis: ajuste em \"Gerenciar as configurações em 3D\" do Painel de Controle NVIDIA.",
            Array.Empty<uint>(), Array.Empty<PanelChoice>(), Info: true),
        new("aniso", "Filtragem anisotrópica", "Nitidez das texturas vistas em ângulo (chão, paredes). Custa pouco nas placas atuais.",
            new[] { AnisoMode, AnisoLevel },
            new[] { new PanelChoice("Controlado pelo aplicativo", new[] { (AnisoMode, 0u) }), new PanelChoice("Desligado", new[] { (AnisoMode, 1u), (AnisoLevel, 1u) }) }
                .Concat(new uint[] { 2, 4, 8, 16 }.Select(x => new PanelChoice($"{x}x", new[] { (AnisoMode, 1u), (AnisoLevel, x) }))).ToArray()),
        S("lod", "Filtragem da textura - Diferencial negativo de LOD", "Fixar evita texturas tremulando quando o jogo pede LOD negativo.", 0x0019BB68, ("Permitir", 0), ("Fixar", 1)),
        S("anisoopt", "Filtragem da textura - otimização anisotrópica de amostras", "Usa menos amostras na filtragem anisotrópica (mais rápido, um pouco menos nítido).",
            0x00E73211, ("Desligado", 0), ("Ligado", 1)),
        S("trilinear", "Filtragem da textura - otimização trilinear", "Filtragem trilinear mais barata (ignorada em Alta qualidade).", 0x002ECAF2, ("Ligado", 0), ("Desligado", 1)),
        S("texq", "Filtragem da textura - qualidade", "Qualidade da filtragem das texturas. Alto desempenho quase não muda a imagem nas placas atuais.",
            0x00CE2691, ("Alta qualidade", 0xFFFFFFF6), ("Qualidade", 0), ("Desempenho", 10), ("Alto desempenho", 20)),
        new("oglgpu", "GPU com renderização OpenGL", "Qual placa roda os jogos OpenGL. Com uma placa só, a seleção automática já usa ela.",
            new[] { OpenGlGpu }, Array.Empty<PanelChoice>(), Text: true, AutoLabel: "Seleção automática"),
        S("power", "Modo de gerenciamento da energia", "Desempenho máximo segura o clock alto (menos oscilação, mais consumo e calor parado).",
            0x1057EB71, ("Normal", 5), ("Adaptável", 0), ("Preferência por desempenho máximo.", 1)),
        new("latency", "Modo de latência baixa", "Menos quadros na fila = resposta mais rápida. Ultra envia o quadro bem na hora; jogos com NVIDIA Reflex usam o Reflex no lugar.",
            new[] { LlCpl, LlUltra, Prerender }, new[]
            {
                new PanelChoice("Desligado", new[] { (LlCpl, 0u), (LlUltra, 0u), (Prerender, 0u) }),
                new PanelChoice("Ligado", new[] { (LlCpl, 1u), (LlUltra, 0u), (Prerender, 1u) }),
                new PanelChoice("Ultra", new[] { (LlCpl, 2u), (LlUltra, 1u), (Prerender, 1u) }),
            }),
        S("mfaa", "Multi-Frame Sampled AA (MFAA - Suavização de amostragem de múltiplos quadros)", "Deixa o MSAA do jogo mais barato alternando o padrão de amostras.",
            0x0098C1AC, ("Desligado", 0), ("Ligado", 1)),
        S("present", "Método atual Vulkan/OpenGL", "Em camada no DXGI permite G-SYNC, HDR automático e Frame Generation em jogos Vulkan/OpenGL em janela.",
            0x20D690F8, ("Automático", 2), ("Preferência por nativo", 0), ("Preferência por camada em DXGI Swapchain", 1)),
        S("ao", "Oclusão ambiental", "Sombras de contato mais realistas em jogos compatíveis; custa desempenho.", 0x00667329, ("Desligado", 0), ("Desempenho", 1), ("Qualidade", 2)),
        S("threaded", "Otimização segmentada", "Usa mais núcleos do processador no driver (OpenGL e jogos antigos).", 0x20C1221E, ("Automático", 0), ("Ligado", 1), ("Desligado", 2)),
        new("gpu", "Processador gráfico preferido", "Notebooks com gráfico híbrido: qual placa roda os programas.",
            new[] { OptCompat, OptMode }, new[]
            {
                new PanelChoice("Processador NVIDIA de alto desempenho", new[] { (OptCompat, 1u), (OptMode, 1u) }),
                new PanelChoice("Gráficos integrados", new[] { (OptCompat, 0u), (OptMode, 0u) }),
            }, HybridOnly: true, AutoLabel: "Seleção automática"),
        S("vrframes", "Quadros pré-renderizados de Realidade Virtual", "Quantos quadros os óculos de RV deixam na fila; 1 = menor latência.",
            0x10111133, ("Usar a configuração do aplicativo 3D", 0), ("1", 1), ("2", 2), ("3", 3), ("4", 4)),
        S("vrss", "Realidade Virtual – Superamostragem de taxa variável", "VRSS: mais nitidez no centro da imagem em jogos de RV compatíveis.",
            0x00D5E9C7, ("Desligado", 0), ("Adaptável", 2), ("Sempre ligado", 1)),
        S("vsync", "Sincronização vertical", "Tira o tearing travando o FPS na taxa do monitor; aumenta a latência. Rápido = sem trava e sem tearing (precisa de FPS bem acima da taxa).",
            0x00A879CF, ("Usar a configuração do aplicativo 3D", 0x60925292), ("Desligado", 0x08416747), ("Ligado", 0x47814940), ("Rápido", 0x18888888)),
        S("shader", "Tamanho do cache do criador de sombras", "Espaço em disco para shaders compilados: cache maior = menos travadas ao abrir áreas novas.",
            0x00AC8497, ("Desabilitado", 0), ("5 GB", 0x1400), ("10 GB", 0x2800), ("100 GB", 0x19000), ("Sem limite", 0xFFFFFFFF)),
        new("maxfps", "Taxa Máxima de Quadros", "Limite de FPS do driver. Um pouco abaixo da taxa do monitor com G-SYNC dá a menor latência sem tearing.",
            new[] { MaxFps }, Array.Empty<PanelChoice>(), Fps: true),
        S("refresh", "Taxa de atualização preferida", "Força a maior taxa do monitor em jogos que pedem menos.", 0x0064B541, ("Controlado pelo aplicativo", 0), ("A maior disponível", 1)),
        new("bgfps", "Taxa máxima de quadros de aplicativo em segundo plano", "Limita o FPS do jogo quando ele não está em primeiro plano (economiza energia e calor).",
            new[] { BgFps }, Array.Empty<PanelChoice>(), Fps: true),
        S("monitor", "Tecnologia de monitor", "G-SYNC (ou G-SYNC Compatível) ou taxa de atualização fixa.", 0x10A879CF, ("G-SYNC Compatível", 0), ("Taxa de atualização fixa", 4)),
    };

    /// <summary>Opções comuns de limite de FPS (o valor atual entra na lista se for outro).</summary>
    public static readonly uint[] FpsPresets = { 30, 60, 75, 90, 100, 120, 141, 144, 157, 160, 165, 180, 200, 237, 240, 300, 360 };

    public static PanelChoice FpsChoice(uint id, uint fps) => C(fps == 0 ? "Desligado" : $"{fps} FPS", id, fps);

    /// <summary>Lê as linhas no perfil (global = perfil base). Rápido: ~40 leituras numa sessão.</summary>
    public static List<PanelState> Read(string profile, string baseName, IEnumerable<PanelSetting> settings)
        => NvDrs.Session(s =>
        {
            IntPtr p = profile == baseName ? NvDrs.BaseProfile(s) : NvDrs.FindProfile(s, profile);
            var list = new List<PanelState>();
            foreach (var st in settings)
            {
                if (st.Info) { list.Add(new PanelState(st, null, false, false, null)); continue; }
                var values = st.Ids.ToDictionary(id => id, id => { try { return NvDrs.Get(s, p, id); } catch { return null; } });
                bool here = values.Values.Any(v => v is { Location: DrsLocation.Current, IsPredefined: false });
                var first = values[st.Ids[0]];
                uint? raw = first is { Type: DrsType.Dword or DrsType.Qword } ? (uint)first.Number : null;
                PanelChoice? current = st.Fps
                    ? raw is uint f ? FpsChoice(st.Ids[0], f) : null
                    : st.Choices.FirstOrDefault(c => c.Writes.All(w => values.GetValueOrDefault(w.Id) is { } v && (uint)v.Number == w.Value))
                      // compostas: a primeira configuração decide (ex.: "Modo de latência baixa" pelo estado do Painel)
                      ?? (st.Ids.Length > 1 && raw is uint r ? st.Choices.FirstOrDefault(c => c.Writes[0].Id == st.Ids[0] && c.Writes[0].Value == r) : null);
                bool predefined = !here && values.Values.Any(v => v is { Location: DrsLocation.Current, IsPredefined: true });
                list.Add(new PanelState(st, current, here, predefined, raw, st.Text ? first?.Text : null));
            }
            return list;
        });

    /// <summary>O que gravar: a opção escolhida, ou null = "usar global/padrão" (tira a configuração deste perfil).</summary>
    public static IEnumerable<DrsChange> Changes(PanelSetting st, PanelChoice? choice)
        => choice == null
            ? st.Ids.Select(id => new DrsChange(id, DrsType.Dword, 0, null, Reset: true))
            : choice.Writes.Select(w => new DrsChange(w.Id, DrsType.Dword, w.Value, null, Reset: false));

    /// <summary>
    /// Texto legível de "GPU com renderização OpenGL" ("id,2.0:2C0510DE,…" = código PCI da placa + fabricante)
    /// e de "CUDA - GPUs" (lista de placas excluídas; vazia = todas).
    /// </summary>
    public static string Describe(PanelSetting st, string? text, IReadOnlyList<(int DeviceId, string Name)> gpus)
    {
        if (string.IsNullOrWhiteSpace(text)) return st.AutoLabel ?? "";
        if (st.Key == "cudagpus") return text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? "Todas" : "Algumas placas excluídas";
        var m = System.Text.RegularExpressions.Regex.Match(text, @":([0-9A-F]{4})10DE", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && gpus.FirstOrDefault(g => g.DeviceId == Convert.ToInt32(m.Groups[1].Value, 16)) is { Name: { Length: > 0 } name }) return name;
        return m.Success ? $"Placa NVIDIA (código {m.Groups[1].Value.ToUpperInvariant()})" : text;
    }
}
