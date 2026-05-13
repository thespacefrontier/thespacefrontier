using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Corvax.Interfaces.Shared;
using Content.Server.Corvax.Sponsors;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.DiscordLink;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.DiscordAuth;

public sealed class DiscordLinkSystem : EntitySystem
{
    private static readonly TimeSpan HttpOperationTimeout = TimeSpan.FromSeconds(10);

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

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

        _cfg.OnValueChanged(CCCVars.SponsorApiUrl, OnSponsorApiUrlChanged, true);
        _cfg.OnValueChanged(CCCVars.SponsorApiKey, OnSponsorApiKeyChanged, true);

        _httpClient.Timeout = HttpOperationTimeout + TimeSpan.FromSeconds(2);

        SubscribeNetworkEvent<DiscordLinkStatusRequestEvent>(OnStatusRequest);
        SubscribeNetworkEvent<DiscordLinkRequestEvent>(OnLinkRequest);
        SubscribeNetworkEvent<DiscordUnlinkRequestEvent>(OnUnlinkRequest);
    }

    public override void Shutdown()
    {
        _cfg.UnsubValueChanged(CCCVars.SponsorApiUrl, OnSponsorApiUrlChanged);
        _cfg.UnsubValueChanged(CCCVars.SponsorApiKey, OnSponsorApiKeyChanged);
        _httpClient.Dispose();
        base.Shutdown();
    }

    private void OnSponsorApiUrlChanged(string v)
    {
        _apiUrl = v;
    }

    private void OnSponsorApiKeyChanged(string v)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrEmpty(v))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", v);
    }

    private async void OnStatusRequest(DiscordLinkStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var session = args.SenderSession;
        try
        {
            var (linked, name) = await GetLinkStatusAsync(userId).ConfigureAwait(false);
            _taskManager.RunOnMainThread(() =>
            {
                RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = linked, DiscordName = name }, session);
            });
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to get link status for {userId}: {ex.Message}");
            _taskManager.RunOnMainThread(() =>
            {
                RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = false }, session);
            });
        }
    }

    private async void OnLinkRequest(DiscordLinkRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        try
        {
            var url = await _authManager.GenerateAuthLink(session.UserId, session.Name, CancellationToken.None)
                .ConfigureAwait(false);
            _taskManager.RunOnMainThread(() =>
            {
                RaiseNetworkEvent(new DiscordLinkUrlEvent { Url = url }, session);
            });
        }
        catch (DiscordAlreadyLinkedException ex)
        {
            _sponsorsManager?.InvalidateCache(session.UserId);
            _taskManager.RunOnMainThread(() =>
            {
                RaiseNetworkEvent(
                    new DiscordLinkStatusEvent { IsLinked = true, DiscordName = ex.DiscordUserName },
                    session);
            });
        }
        catch (Exception ex)
        {
            Log.Error(
                "Failed to generate Discord link for {0}: {1}\n{2}",
                session.UserId,
                ex,
                ex.StackTrace);
            _taskManager.RunOnMainThread(() =>
            {
                // Empty URL signals failure to the client (see DiscordLinkSystem.OnLinkUrl on client).
                RaiseNetworkEvent(new DiscordLinkUrlEvent { Url = string.Empty }, session);
            });
        }
    }

    private async void OnUnlinkRequest(DiscordUnlinkRequestEvent ev, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var session = args.SenderSession;
        try
        {
            await UnlinkAsync(userId).ConfigureAwait(false);
            _sponsorsManager?.InvalidateCache(userId);
            _taskManager.RunOnMainThread(() =>
            {
                RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = false }, session);
            });
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to unlink Discord for {userId}: {ex.Message}");
            try
            {
                var (linked, name) = await GetLinkStatusAsync(userId).ConfigureAwait(false);
                _taskManager.RunOnMainThread(() =>
                {
                    RaiseNetworkEvent(new DiscordLinkStatusEvent { IsLinked = linked, DiscordName = name }, session);
                });
            }
            catch (Exception refetchEx)
            {
                Log.Error(
                    $"Failed to re-fetch Discord link status after unlink failure for {userId}: {refetchEx}\n{refetchEx.StackTrace}");
            }
        }
    }

    private async Task<(bool Linked, string? DiscordName)> GetLinkStatusAsync(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            return (false, null);

        using var cts = new CancellationTokenSource(HttpOperationTimeout);
        var token = cts.Token;

        try
        {
            var url = $"{_apiUrl.TrimEnd('/')}/api/links/by-ss14/{userId.UserId}";
            var response = await _httpClient.GetAsync(url, token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return (false, null);

            var link = await response.Content.ReadFromJsonAsync<LinkApiResponse>(cancellationToken: token)
                .ConfigureAwait(false);
            // 2xx with empty/malformed body must not be treated as "linked".
            if (link is null)
                return (false, null);

            return (true, link.DiscordUserName);
        }
        catch (OperationCanceledException)
        {
            return (false, null);
        }
    }

    private async Task UnlinkAsync(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            throw new InvalidOperationException("Sponsor API URL not configured");

        using var cts = new CancellationTokenSource(HttpOperationTimeout);
        var token = cts.Token;

        var url = $"{_apiUrl.TrimEnd('/')}/api/links/{userId.UserId}";
        var response = await _httpClient.DeleteAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
