using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Server.Discord.DiscordLink;
using Content.Shared.Corvax.CCCVars;
using NetCord.Gateway;
using Robust.Shared.Configuration;

namespace Content.Server.Corvax.Sponsors;

public sealed class DiscordWhoisCommand : IPostInjectInit
{
    [Dependency] private readonly DiscordLink _discordLink = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    private string _apiUrl = string.Empty;

    public void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.SponsorApiUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CCCVars.SponsorApiKey, v =>
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            if (!string.IsNullOrEmpty(v))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", v);
        }, true);

        _discordLink.RegisterCommandCallback(OnWhoisCommand, "whois");
    }

    private async void OnWhoisCommand(CommandReceivedEventArgs args)
    {
        if (args.Arguments.Count < 1)
        {
            await ReplyAsync(args.Message, "Usage: `!whois <ss14_username>`");
            return;
        }

        var ckey = args.Arguments[0];

        if (string.IsNullOrEmpty(_apiUrl))
        {
            await ReplyAsync(args.Message, "Sponsor service is not configured.");
            return;
        }

        try
        {
            var url = $"{_apiUrl.TrimEnd('/')}/api/links/by-name/{Uri.EscapeDataString(ckey)}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                await ReplyAsync(args.Message, $"No linked Discord account found for `{ckey}`.");
                return;
            }

            var link = await response.Content.ReadFromJsonAsync<LinkApiResponse>();
            if (link == null)
            {
                await ReplyAsync(args.Message, $"No linked Discord account found for `{ckey}`.");
                return;
            }

            var mention = $"<@{link.DiscordId}>";
            await ReplyAsync(args.Message,
                $"**{link.Ss14UserName}** is linked to Discord user {mention} ({link.DiscordUserName})");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error in whois command: {ex}");
            await ReplyAsync(args.Message, "An error occurred while looking up the user.");
        }
    }

    private async Task ReplyAsync(Message source, string text)
    {
        try
        {
            await _discordLink.SendMessageAsync(source.ChannelId, text);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to send whois reply: {ex}");
        }
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord.whois");
    }
}
