using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Voting.Managers;
using Content.Shared._TSF;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._TSF.AutonomousLobby;

public sealed class TSFAutonomousLobbyVoteSystem : EntitySystem
{
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private bool _sequenceActive;
    private bool _oocDisabledByTsf;
    private bool _savedOoc;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!_sequenceActive)
            return;

        foreach (var v in _voteManager.ActiveVotes.ToArray())
        {
            v.Cancel();
        }

        _sequenceActive = false;
        RestoreOoc();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        if (!_cfg.GetCVar(TSFCVars.TsfAutonomousLobbyVote))
            return;

        var ticker = EntityManager.System<GameTicker>();
        if (!ticker.LobbyEnabled)
            return;

        _sequenceActive = true;
        _savedOoc = _cfg.GetCVar(CCVars.OocEnabled);
        _cfg.SetCVar(CCVars.OocEnabled, false);
        _oocDisabledByTsf = true;

        Timer.Spawn(TimeSpan.FromMilliseconds(150), () =>
        {
            if (!_sequenceActive)
                return;

            _audio.PlayGlobal("/Audio/_TSF/Misc/30sec.ogg", Filter.Broadcast(), false, AudioParams.Default);
        });

        if (_voteManager is not VoteManager vm)
        {
            CompleteSequence();
            return;
        }

        vm.TSFRunLobbyAutonomousVotes(CompleteSequence);
    }

    private void CompleteSequence()
    {
        if (!_sequenceActive)
            return;

        _sequenceActive = false;
        RestoreOoc();
    }

    private void RestoreOoc()
    {
        if (!_oocDisabledByTsf)
            return;

        _cfg.SetCVar(CCVars.OocEnabled, _savedOoc);
        _oocDisabledByTsf = false;
    }
}
