using Robust.Shared.Configuration;

namespace Content.Shared._TSF;

/// <summary>
///     Cvars for TSF server features (see Content.Server/_TSF).
/// </summary>
public static class TSFCVars
{
    /// <summary>
    ///     When the round restarts into lobby, start automatic preset and map votes (30s each), disable OOC, play 30sec lobby sting.
    /// </summary>
    public static readonly CVarDef<bool> TsfAutonomousLobbyVote =
        CVarDef.Create("tsf.autonomous_lobby_vote", true, CVar.SERVERONLY);
}
