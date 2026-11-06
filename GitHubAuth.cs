using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace ResetExplode;

/// <summary>Conta do GitHub entrou: login e nome para mostrar no menu lateral.</summary>
public sealed record GitHubUser(string Login, string Name, string AvatarUrl);

/// <summary>Código que a pessoa digita em github.com/login/device.</summary>
public sealed record DeviceCode(string UserCode, string VerificationUri, string Device, int IntervalSeconds, DateTime Expires);

/// <summary>
/// Login com o GitHub pelo Device Flow (o mesmo do GitHub CLI): o app pede um código, a pessoa autoriza no navegador e o
/// app recebe o token. Não passa senha pelo app e não precisa de segredo dentro do .exe, só do Client ID do OAuth App,
/// que é público e entra no build pelo workflow (-p:GitHubClientId=…). O token fica cifrado com DPAPI nas configurações.
/// </summary>
public static class GitHubAuth
{
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PcismoMaster");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return c;
    }

    /// <summary>Client ID do OAuth App colocado no build; null = login desligado neste build.</summary>
    public static string? ClientId { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitHubClientId")?.Value is { Length: > 0 } id ? id : null;

    public static bool Available => ClientId != null;

    /// <summary>Passo 1: pede o código para a pessoa digitar no GitHub.</summary>
    public static async Task<DeviceCode> StartAsync(CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId!, ["scope"] = "read:user" });
        using var resp = await Http.PostAsync("https://github.com/login/device/code", form, ct);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var r = doc.RootElement;
        if (r.TryGetProperty("error", out var err)) throw new InvalidOperationException(Describe(err.GetString()));
        return new DeviceCode(
            r.GetProperty("user_code").GetString()!,
            r.GetProperty("verification_uri").GetString()!,
            r.GetProperty("device_code").GetString()!,
            r.TryGetProperty("interval", out var i) ? i.GetInt32() : 5,
            DateTime.UtcNow.AddSeconds(r.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900));
    }

    /// <summary>Passo 2: espera a pessoa autorizar e devolve o token (ou lança se negar, expirar ou cancelar).</summary>
    public static async Task<string> WaitForTokenAsync(DeviceCode code, CancellationToken ct)
    {
        int interval = Math.Max(code.IntervalSeconds, 5);
        while (DateTime.UtcNow < code.Expires)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId!,
                ["device_code"] = code.Device,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            });
            using var resp = await Http.PostAsync("https://github.com/login/oauth/access_token", form, ct);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var r = doc.RootElement;
            if (r.TryGetProperty("access_token", out var tok) && tok.GetString() is { Length: > 0 } t) return t;
            string error = r.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            if (error == "authorization_pending") continue;
            if (error == "slow_down") { interval += 5; continue; }
            throw new InvalidOperationException(Describe(error));
        }
        throw new InvalidOperationException("O código expirou. Clique em Entrar de novo.");
    }

    /// <summary>Lê quem é o dono do token. null = token inválido ou revogado.</summary>
    public static async Task<GitHubUser?> GetUserAsync(string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        req.Headers.Authorization = new("Bearer", token);
        using var resp = await Http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return null;
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var r = doc.RootElement;
        string login = r.GetProperty("login").GetString() ?? "";
        string name = r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : login;
        string avatar = r.TryGetProperty("avatar_url", out var a) ? a.GetString() ?? "" : "";
        return new GitHubUser(login, name, avatar);
    }

    private static string Describe(string? error) => error switch
    {
        "access_denied" => "Você negou o acesso no GitHub.",
        "expired_token" => "O código expirou. Clique em Entrar de novo.",
        "device_flow_disabled" => "O Device Flow está desligado no OAuth App do GitHub.",
        "incorrect_client_credentials" => "O Client ID do GitHub neste build está errado.",
        _ => "O GitHub recusou o login (" + error + ").",
    };
}
