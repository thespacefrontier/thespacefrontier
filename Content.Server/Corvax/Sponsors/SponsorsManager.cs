using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Server.Corvax.Sponsors;

public sealed class SponsorsManager : ISharedSponsorsManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;

    private readonly Dictionary<NetUserId, CachedSponsor> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _cfg.OnValueChanged(CCCVars.SponsorApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CCCVars.SponsorApiKey, v =>
        {
            _apiKey = v;
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            if (!string.IsNullOrEmpty(v))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", v);
        }, true);
    }

    public List<string> GetClientPrototypes()
    {
        return [];
    }

    public bool TryGetServerPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        var sponsor = GetCachedOrFetch(userId);
        if (sponsor is { Linked: true, Prototypes: not null } && sponsor.Prototypes.Count > 0)
        {
            prototypes = sponsor.Prototypes;
            return true;
        }

        prototypes = null;
        return false;
    }

    public bool TryGetServerOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color)
    {
        var sponsor = GetCachedOrFetch(userId);
        if (sponsor is { Linked: true, OocColor: not null })
        {
            color = Color.FromHex(sponsor.OocColor);
            return true;
        }

        color = null;
        return false;
    }

    public bool TryGetServerOocPrefix(NetUserId userId, [NotNullWhen(true)] out string? prefix)
    {
        var sponsor = GetCachedOrFetch(userId);
        if (sponsor is { Linked: true, OocPrefix: not null } && sponsor.OocPrefix.Length > 0)
        {
            prefix = sponsor.OocPrefix;
            return true;
        }

        prefix = null;
        return false;
    }

    public bool TryGetServerGhostColor(NetUserId userId, [NotNullWhen(true)] out Color? color)
    {
        var sponsor = GetCachedOrFetch(userId);
        if (sponsor is { Linked: true, GhostColor: not null } && sponsor.GhostColor.Length > 0)
        {
            color = Color.FromHex(sponsor.GhostColor);
            return true;
        }

        color = null;
        return false;
    }

    public int GetServerExtraCharSlots(NetUserId userId)
    {
        var sponsor = GetCachedOrFetch(userId);
        return sponsor is { Linked: true } ? sponsor.ExtraSlots : 0;
    }

    public bool HaveServerPriorityJoin(NetUserId userId)
    {
        var sponsor = GetCachedOrFetch(userId);
        return sponsor is { Linked: true, PriorityJoin: true };
    }

    public void InvalidateCache(NetUserId userId)
    {
        _cache.Remove(userId);
    }

    private SponsorApiResponse? GetCachedOrFetch(NetUserId userId)
    {
        if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Data;

        try
        {
            var data = FetchSponsorDataAsync(userId).GetAwaiter().GetResult();
            _cache[userId] = new CachedSponsor(data, DateTime.UtcNow + CacheDuration);
            return data;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to fetch sponsor data for {userId}: {ex.Message}");
            return null;
        }
    }

    private async Task<SponsorApiResponse?> FetchSponsorDataAsync(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
        {
            _sawmill.Warning("Sponsor API URL not configured");
            return null;
        }

        var url = $"{_apiUrl.TrimEnd('/')}/api/sponsors/{userId.UserId}";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Warning($"Sponsor API returned {response.StatusCode} for {userId}");
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SponsorApiResponse>();
    }

    private sealed record CachedSponsor(SponsorApiResponse? Data, DateTime ExpiresAt);
}
