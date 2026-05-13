namespace Content.Server.Corvax.DiscordAuth;

// Thrown when POST /api/links/token returns 409 and by-ss14 shows an existing link.
public sealed class DiscordAlreadyLinkedException : Exception
{
    public string? DiscordUserName { get; }

    public DiscordAlreadyLinkedException(string? discordUserName)
        : base("Discord is already linked for this account.")
    {
        DiscordUserName = discordUserName;
    }
}
