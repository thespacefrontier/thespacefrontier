using Robust.Shared.Configuration;

namespace Content.Shared._TSF;

[CVarDefs]
public sealed class TSFCVars
{
    public static readonly CVarDef<bool> TsfAutonomousLobbyVote =
        CVarDef.Create("tsf.autonomous_lobby_vote", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> TsfLockPlayerVoteMenu =
        CVarDef.Create("tsf.lock_player_vote_menu", true, CVar.SERVERONLY);
}
