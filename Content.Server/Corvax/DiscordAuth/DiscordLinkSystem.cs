using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Corvax.Interfaces.Shared;
using Content.Server.Corvax.Sponsors;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.DiscordLink;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.DiscordAuth;

public sealed class DiscordLinkSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private IServerDiscordAuthManager _authManager = default!;
    private SponsorsManager? _sponsorsManager;

    private readonly HttpClient _httpClient = new();
    private string _apiUrl = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        _authManager = IoCManager.Resolve<IServerDiscordAuthManager>();

        if (IoCManager.Instance!.TryResolveType<ISharedSponsorsManager>(out var mgr))
            _sponsorsManager = mgr as SponsorsManager;

        _cfg.OnValueChanged(CCCVars.SponsorApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CCCVars.SponsorApiKey, v =>
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            if (!string.IsNullOrEmpty(v))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", v);
        }, true);

        SubscribeNetworkEvent<DiscordLinkStatusRequestEvent>(OnStatusRequest);
        SubscribeNetworkEvent<DiscordLinkRequestEvent>(OnLinkRequest);
        SubscribeNetworkEvent<DiscordUnlinkRequestEvent>(OnUnlinkRequest);
    }

    private async void OnStatusRequest(DiscordLinkStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        try
        {
            var (linked, name) = await GetLinkStatusAsync(userId);
            RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = linked, DiscordName = name },
                args.SenderSession);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to get link status for {userId}: {ex.Message}");
            RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = false }, args.SenderSession);
        }
    }

    private async void OnLinkRequest(DiscordLinkRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        try
        {
            var url = await _authManager.GenerateAuthLink(session.UserId, session.Name, CancellationToken.None);
            RaiseNetworkEvent(new DiscordLinkUrlEvent { Url = url }, session);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to generate Discord link for {session.UserId}: {ex.Message}");
        }
    }

    private async void OnUnlinkRequest(DiscordUnlinkRequestEvent ev, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        try
        {
            await UnlinkAsync(userId);
            _sponsorsManager?.InvalidateCache(userId);
            RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = false }, args.SenderSession);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to unlink Discord for {userId}: {ex.Message}");
            try
            {
                var (linked, name) = await GetLinkStatusAsync(userId);
                RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = linked, DiscordName = name },
                    args.SenderSession);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task<(bool Linked, string? DiscordName)> GetLinkStatusAsync(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            return (false, null);

        var url = $"{_apiUrl.TrimEnd('/')}/api/links/by-ss14/{userId.UserId}";
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return (false, null);

        var link = await response.Content.ReadFromJsonAsync<LinkApiResponse>();
        return (true, link?.DiscordUserName);
    }

    private async Task UnlinkAsync(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            throw new InvalidOperationException("Sponsor API URL not configured");

        var url = $"{_apiUrl.TrimEnd('/')}/api/links/{userId.UserId}";
        var response = await _httpClient.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }
}
