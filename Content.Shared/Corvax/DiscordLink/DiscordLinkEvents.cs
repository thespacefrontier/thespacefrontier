using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.DiscordLink;

[Serializable, NetSerializable]
public sealed class DiscordLinkStatusRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class DiscordLinkStatusEvent : EntityEventArgs
{
    public bool IsLinked;
    public string? DiscordName;
}

[Serializable, NetSerializable]
public sealed class DiscordLinkRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class DiscordLinkUrlEvent : EntityEventArgs
{
    public string Url = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DiscordUnlinkRequestEvent : EntityEventArgs;
