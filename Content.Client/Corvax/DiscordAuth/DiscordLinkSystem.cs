using System;
using Content.Shared.Corvax.DiscordLink;
using Robust.Client.UserInterface;

namespace Content.Client.Corvax.DiscordAuth;

public sealed class DiscordLinkSystem : EntitySystem
{
    [Dependency] private readonly IUriOpener _uriOpener = default!;

    public bool IsLinked { get; private set; }
    public string? DiscordName { get; private set; }

    public event Action? StatusUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DiscordLinkStatusEvent>(OnStatus);
        SubscribeNetworkEvent<DiscordLinkUrlEvent>(OnLinkUrl);
    }

    public void RequestStatus()
    {
        RaiseNetworkEvent(new DiscordLinkStatusRequestEvent());
    }

    public void RequestLink()
    {
        RaiseNetworkEvent(new DiscordLinkRequestEvent());
    }

    public void RequestUnlink()
    {
        RaiseNetworkEvent(new DiscordUnlinkRequestEvent());
    }

    private void OnStatus(DiscordLinkStatusEvent ev)
    {
        IsLinked = ev.IsLinked;
        DiscordName = ev.DiscordName;
        StatusUpdated?.Invoke();
    }

    private void OnLinkUrl(DiscordLinkUrlEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Url))
        {
            Log.Warning("Discord link request failed (empty URL from server).");
            return;
        }

        try
        {
            var uri = new Uri(ev.Url);
            if (!uri.IsAbsoluteUri)
            {
                Log.Warning("Discord link URL is not absolute: {0}", ev.Url);
                return;
            }

            _uriOpener.OpenUri(uri);
        }
        catch (Exception ex)
        {
            Log.Warning("Discord link URL is invalid: {0} ({1})", ev.Url, ex.Message);
        }
    }
}
