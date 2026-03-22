using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Server.Corvax.Sponsors;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.DiscordAuth;

public sealed class ServerDiscordAuthManager : IServerDiscordAuthManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;

    public event EventHandler<ICommonSession>? PlayerVerified;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("discord.auth");
        _cfg.OnValueChanged(CCCVars.SponsorApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CCCVars.SponsorApiKey, v =>
        {
            _apiKey = v;
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            if (!string.IsNullOrEmpty(v))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", v);
        }, true);
    }

    public async Task<string> GenerateAuthLink(NetUserId userId, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            throw new InvalidOperationException("Sponsor API URL not configured");

        var url = $"{_apiUrl.TrimEnd('/')}/api/links/token";
        var payload = new { ss14UserId = userId.UserId, ss14UserName = "" };

        var response = await _httpClient.PostAsJsonAsync(url, payload, cancel);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LinkTokenApiResponse>(cancellationToken: cancel);
        return result?.Url ?? throw new InvalidOperationException("Failed to get link URL from API");
    }

    public async Task<bool> IsVerified(NetUserId userId, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            return false;

        try
        {
            var url = $"{_apiUrl.TrimEnd('/')}/api/links/by-ss14/{userId.UserId}";
            var response = await _httpClient.GetAsync(url, cancel);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to check Discord verification for {userId}: {ex.Message}");
            return false;
        }
    }

    public void RaisePlayerVerified(ICommonSession session)
    {
        PlayerVerified?.Invoke(this, session);
    }
}
