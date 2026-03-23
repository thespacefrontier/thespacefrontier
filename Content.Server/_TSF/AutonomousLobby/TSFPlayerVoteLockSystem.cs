// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._TSF.AutonomousLobby;

public sealed class TSFPlayerVoteLockSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_cfg.GetCVar(TSFCVars.TsfLockPlayerVoteMenu))
        {
            _cfg.SetCVar(CCVars.VotePresetEnabled, false);
            _cfg.SetCVar(CCVars.VoteMapEnabled, false);
        }
    }
}
