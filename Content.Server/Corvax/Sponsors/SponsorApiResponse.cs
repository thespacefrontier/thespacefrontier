using System.Text.Json.Serialization;

namespace Content.Server.Corvax.Sponsors;

public sealed class SponsorApiResponse
{
    [JsonPropertyName("linked")]
    public bool Linked { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("prototypes")]
    public List<string>? Prototypes { get; set; }

    [JsonPropertyName("oocColor")]
    public string? OocColor { get; set; }

    [JsonPropertyName("oocPrefix")]
    public string? OocPrefix { get; set; }

    [JsonPropertyName("ghostColor")]
    public string? GhostColor { get; set; }

    [JsonPropertyName("extraSlots")]
    public int ExtraSlots { get; set; }

    [JsonPropertyName("priorityJoin")]
    public bool PriorityJoin { get; set; }
}

public sealed class LinkTokenApiResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class LinkApiResponse
{
    [JsonPropertyName("ss14UserId")]
    public Guid Ss14UserId { get; set; }

    [JsonPropertyName("ss14UserName")]
    public string Ss14UserName { get; set; } = string.Empty;

    [JsonPropertyName("discordId")]
    public long DiscordId { get; set; }

    [JsonPropertyName("discordUserName")]
    public string DiscordUserName { get; set; } = string.Empty;

    [JsonPropertyName("linkedAt")]
    public DateTime LinkedAt { get; set; }
}
