using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ResetExplode.Modular;

/// <summary>Deixa os textos de botão escritos em CAIXA ALTA no formato de frase ("REMOVER OS MARCADOS" → "Remover os marcados"), mantendo siglas.</summary>
public static class FriendlyText
{
    // Nomes próprios que ficam com a grafia certa mesmo em caixa alta.
    private static readonly Dictionary<string, string> Proper = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINDOWS"] = "Windows", ["STEAM"] = "Steam", ["EPIC"] = "Epic", ["EDGE"] = "Edge", ["STORE"] = "Store", ["XBOX"] = "Xbox",
        ["ONEDRIVE"] = "OneDrive", ["DEFENDER"] = "Defender", ["BITLOCKER"] = "BitLocker", ["POWERSHELL"] = "PowerShell", ["WEBVIEW"] = "WebView",
        ["ULTIMATE"] = "Ultimate", ["ARDUINO"] = "Arduino", ["WINGET"] = "winget", ["DIRECTX"] = "DirectX", ["NVME"] = "NVMe", ["REBAR"] = "ReBAR",
        ["MMAGENT"] = "MMAgent", ["IPV4"] = "IPv4", ["IPV6"] = "IPv6", ["ICUE"] = "iCUE", ["AFTERBURNER"] = "Afterburner", ["RIVATUNER"] = "RivaTuner",
        ["MHZ"] = "MHz", ["GHZ"] = "GHz", ["HZ"] = "Hz", ["MV"] = "mV", ["WI-FI"] = "Wi-Fi", ["BLUETOOTH"] = "Bluetooth", ["PCIE"] = "PCIe",
        ["GOG"] = "GOG", ["MAX"] = "máx.", ["MIN"] = "min", ["MAKER"] = "Maker", ["INSPECTOR"] = "Inspector", ["PROFILE"] = "Profile",
        ["YT-DLP"] = "yt-dlp", ["FFMPEG"] = "ffmpeg", ["QOS"] = "QoS", ["GAME"] = "Game", ["BAR"] = "Bar",
    };

    private static readonly HashSet<string> KeepUpper = new(StringComparer.Ordinal)
    {
        "PC", "CPU", "GPU", "RAM", "VRAM", "USB", "SSD", "HD", "DDU", "NVIDIA", "AMD", "OC", "BIOS", "UEFI", "DNS", "IP", "RGB", "MSI", "XMP", "EXPO",
        "DEP", "MPO", "HDR", "FPS", "VRR", "API", "DLSS", "FSR", "PDF", "ZIP", "EXE", "MSR", "SMT", "HAGS", "TPM", "UAC", "VBS", "HVCI", "ITAD", "COM",
        "LED", "SDK", "MFT", "NTFS", "VPN", "TCP", "UDP", "OK", "GB", "MB", "KB", "TB", "SATA", "M.2", "CH340", "CP210X", "FTDI", "ESP32", "ESP8266", "W", "V",
    };

    /// <summary>Converte só quando o texto inteiro está em caixa alta (o resto já foi escrito do jeito certo).</summary>
    public static string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        bool hasLetter = false;
        foreach (char c in text)
        {
            if (!char.IsLetter(c)) continue;
            hasLetter = true;
            if (char.IsLower(c)) return text;
        }
        if (!hasLetter) return text;
        // Textos curtos que são só uma sigla ("OK", "GPU") ficam como estão.
        var words = Regex.Split(text, @"(\s+|/|\(|\)|·|,|:|…|\+)");
        if (words.Count(w => w.Any(char.IsLetter)) == 1 && KeepUpper.Contains(words.First(w => w.Any(char.IsLetter)).Trim())) return text;

        var sb = new StringBuilder();
        bool first = true;
        foreach (var w in words)
        {
            if (!w.Any(char.IsLetter)) { sb.Append(w); if (w.Any(char.IsDigit)) first = false; continue; }
            string core = w;
            if (KeepUpper.Contains(core)) sb.Append(core);
            else if (Proper.TryGetValue(core, out var p)) sb.Append(first && char.IsLower(p[0]) && p != "winget" && p != "yt-dlp" && p != "ffmpeg" ? Cap(p) : p);
            else if (Regex.IsMatch(core, @"\d") && core.Length <= 6) sb.Append(core); // 1080P, 4K, 5600X…
            else
            {
                string lower = core.ToLower(new CultureInfo("pt-BR"));
                sb.Append(first ? Cap(lower) : lower);
            }
            first = false;
        }
        return sb.ToString();

        static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], new CultureInfo("pt-BR")) + s[1..];
    }
}

/// <summary>Uma página do app: nome amigável, onde fica, para que serve, palavras para a busca e se aparece no modo Simples.</summary>
public sealed record PageInfo(string Nav, string Title, string Group, string Description, string Keywords, bool Advanced, string Icon);

/// <summary>Uma tarefa da tela inicial ("O que você quer fazer?") que leva a uma página.</summary>
public sealed record HomeTask(string Title, string Description, string Nav, string Icon, string Accent);

public static class PageCatalog
{
    public static readonly PageInfo[] Pages =
    {
        new("NavHome", "Início", "", "Tarefas mais comuns e a busca de tudo que o app faz.", "inicio home comecar tarefas", false, ""),
        new("NavDash", "Visão geral", "", "Temperatura, uso e consumo da CPU e da placa de vídeo, ao vivo.", "painel dashboard temperatura uso agora saude", false, ""),
        new("NavSystem", "Sistema", "MASTER", "Todas as peças do PC: processador, placa-mãe, memória, discos, rede.", "pecas hardware informacoes placa mae memoria modelo", false, ""),
        new("NavSensors", "Sensores", "MASTER", "Todos os sensores de temperatura, tensão, ventoinhas e consumo.", "sensores voltagem tensao fan ventoinha", true, ""),
        new("NavGpu", "Placa de vídeo", "MASTER", "Detalhes e leituras da placa de vídeo.", "gpu placa de video nvidia amd", true, ""),
        new("NavDisks", "Discos", "MASTER", "Saúde e espaço dos SSDs e HDs.", "ssd hd disco saude smart espaco", false, ""),
        new("NavEnergy", "Energia", "MASTER", "Bateria e plano de energia.", "bateria energia plano notebook", true, ""),
        new("NavNet", "Rede", "MASTER", "Velocidade da rede, ping e adaptadores.", "internet rede ping wifi", true, ""),
        new("NavMaint", "Reparos do Windows", "MANUTENÇÃO", "Consertar o Windows (SFC, DISM), limpar arquivos e revisão semanal.", "reparo consertar sfc dism limpeza manutencao lento erro", false, ""),
        new("NavOptFormat", "Formatação", "TWEAKING", "Depois de formatar: aplica o pacote completo de otimização de uma vez, com ponto de restauração.", "formatacao formatar windows novo instalar boost completo pos instalacao", false, ""),
        new("NavOptDebloat", "Debloat", "TWEAKING", "Tirar telemetria, propaganda, IA e apps que vêm com o Windows, em níveis Essencial, Recomendado e Avançado.", "debloat bloatware remover apps telemetria copilot recall onedrive servicos tarefas", false, ""),
        new("NavOptWindows", "Otimizar Windows", "TWEAKING", "Mais de cem ajustes de desempenho, privacidade, aparência, rede e IA, cada um com desfazer.", "otimizar tweak ajustes desempenho privacidade explorer menu contexto ipv6 edge brave rede", false, ""),
        new("NavOptInput", "Mouse e teclado", "TWEAKING", "Mouse sem atraso, teclado mais rápido e sem atalhos que atrapalham no jogo.", "mouse teclado aderencia shift numlock alt shift narrador atraso", false, ""),
        new("NavOptGpu", "GPU e monitor", "TWEAKING", "Monitor na taxa máxima, agendamento de GPU e energia do PCI Express.", "gpu hags hz taxa atualizacao monitor pcie aspm", false, ""),
        new("NavOptProfiles", "Perfis de otimização", "TWEAKING", "Pacotes prontos (Básico, Sem IA, Padrão do Windows) e os seus perfis salvos.", "perfil perfis pacote basico sem ia padrao exportar importar", false, ""),
        new("NavOptClean", "Limpeza", "TWEAKING", "Ver e apagar temporários, lixeira, cache de atualização, de shaders e dos navegadores.", "limpeza limpar temporarios lixo cache shader navegador lixeira espaco", false, ""),
        new("NavLauncher", "Meus jogos", "JOGOS", "Todos os seus jogos num lugar só, com perfil de abertura.", "jogos biblioteca steam epic launcher abrir", false, ""),
        new("NavDeals", "Promoções", "JOGOS", "Preços e promoções dos jogos nas lojas.", "promocao preco barato desconto loja favorito favoritos coracao drm steam epic xbox gog", false, ""),
        new("NavGames", "FPS estimado", "JOGOS", "Quanto FPS os jogos devem rodar no seu PC.", "fps jogos rodar requisitos", false, ""),
        new("NavBottleneck", "Gargalo", "JOGOS", "Se a CPU ou a placa de vídeo está segurando o desempenho.", "gargalo bottleneck", true, ""),
        new("NavOverlay", "Overlay", "JOGOS", "Mostrar FPS e temperatura por cima dos jogos, limitar o FPS, medir e gravar.", "overlay fps tela jogo rivatuner limitador limite benchmark frametime captura print gravar video", false, ""),
        new("NavBench", "Benchmark", "DESEMPENHO", "Medir a velocidade do PC e comparar.", "benchmark teste pontuacao velocidade", true, ""),
        new("NavStress", "Stress test", "DESEMPENHO", "Testar estabilidade e temperatura no máximo.", "stress teste estabilidade temperatura", true, ""),
        new("NavOc", "Overclock", "DESEMPENHO", "Overclock e undervolt da placa de vídeo.", "overclock undervolt afterburner clock", true, ""),
        new("NavOptimize", "Otimização rápida", "FERRAMENTAS", "Ajustes rápidos do Windows num clique.", "otimizacao rapida", true, ""),
        new("NavStartup", "Inicialização", "FERRAMENTAS", "Programas que abrem junto com o Windows.", "inicializacao startup abrir junto lento ligar", false, ""),
        new("NavApps", "Instalar apps", "FERRAMENTAS", "Instalar programas populares com um clique.", "instalar programas apps winget baixar", false, ""),
        new("NavUninstall", "Desinstalar apps", "FERRAMENTAS", "Desinstalar programas e limpar o que eles deixam para trás.", "desinstalar remover programas sobras revo", false, ""),
        new("NavDriver", "Drivers", "FERRAMENTAS", "Atualizar e limpar o driver da placa de vídeo.", "driver atualizar nvidia amd ddu limpar painel", false, ""),
        new("NavInteg", "Integrações", "FERRAMENTAS", "Iluminação RGB e programas de terceiros.", "rgb icue iluminacao integracao", true, ""),
        new("NavUtil", "Utilidades", "FERRAMENTAS", "Baixar vídeos e achar arquivos na hora.", "baixar video youtube buscar arquivo finder", false, ""),
        new("NavMakerPorts", "Portas", "MAKER", "Portas USB/serial de Arduino e ESP.", "arduino esp serial com porta", true, ""),
        new("NavMakerDrv", "Drivers seriais", "MAKER", "Drivers CH340, CP210x e FTDI.", "ch340 cp210x ftdi driver serial", true, ""),
    };

    public static readonly HomeTask[] Tasks =
    {
        new("Ver como o PC está", "Temperatura, uso e saúde agora.", "NavDash", "", "ChartGpu"),
        new("Deixar o PC mais rápido", "Ajustes do Windows, todos com desfazer.", "NavOptWindows", "", "Accent"),
        new("Remover apps que vêm com o Windows", "Debloat em três níveis ou app por app.", "NavOptDebloat", "", "TempWarn"),
        new("Consertar e limpar o Windows", "Reparos, arquivos temporários e revisão.", "NavMaint", "", "AccentCyan"),
        new("Desinstalar programas", "E apagar as sobras que ficam.", "NavUninstall", "", "TempHot"),
        new("Instalar programas", "Os populares, com um clique.", "NavApps", "", "ChartGpu"),
        new("Atualizar o driver de vídeo", "NVIDIA, AMD e Intel.", "NavDriver", "", "AccentCyan"),
        new("Meus jogos", "Abrir jogos com o perfil certo.", "NavLauncher", "", "Accent"),
        new("Programas que abrem com o Windows", "Deixe o PC ligar mais rápido.", "NavStartup", "", "TempWarn"),
        new("Promoções de jogos", "Preços nas lojas agora.", "NavDeals", "", "TempCrit"),
    };

    public static PageInfo? ByNav(string nav) => Pages.FirstOrDefault(p => p.Nav == nav);

    /// <summary>Busca por nome, descrição e palavras-chave, sem diferenciar acentos nem maiúsculas.</summary>
    public static List<PageInfo> Search(string query, bool advanced)
    {
        var terms = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return new();
        return Pages.Where(p => advanced || !p.Advanced)
            .Select(p => (p, text: Fold($"{p.Title} {p.Group} {p.Description} {p.Keywords}"), title: Fold(p.Title)))
            .Where(x => terms.All(t => x.text.Contains(t)))
            .OrderByDescending(x => terms.Count(t => x.title.Contains(t)))
            .Select(x => x.p).ToList();
    }

    public static string Fold(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
