// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
namespace Content.Server._TSF.Health;

public sealed class TSFVitalsTierSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly TimeSpan RefreshDuration = TimeSpan.FromSeconds(5);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var high = _cfg.GetCVar(TSFCVars.TsfVitalsDistressBloodHigh);
        var low = _cfg.GetCVar(TSFCVars.TsfVitalsDistressBloodLow);
        if (high <= low)
            return;

        var query = EntityQueryEnumerator<BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var bloodstream))
        {
            if (_mobState.IsDead(uid))
                continue;

            var blood = _bloodstream.GetBloodLevel((uid, bloodstream));
            if (blood < high && blood > low)
                _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectTsfVitalsDistress", RefreshDuration);
            else if (blood >= high || blood <= low)
                _statusEffects.TryRemoveStatusEffect(uid, "StatusEffectTsfVitalsDistress");
        }
    }
}
