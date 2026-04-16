// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF;
using Content.Shared._TSF.Medical;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._TSF.Medical;

public sealed class TSFDefibrillatorGatingSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, TsfDefibrillatorReviveCheckEvent>(OnReviveCheck);
    }

    private void OnReviveCheck(EntityUid uid, MobStateComponent _, ref TsfDefibrillatorReviveCheckEvent ev)
    {
        if (!ev.AllowRevive)
            return;

        if (ev.Target != uid)
            return;

        if (!TryComp(uid, out MobThresholdsComponent? thresholds) || !thresholds.ShowOverlays)
            return;

        if (!TryComp(uid, out DamageableComponent? damageable))
            return;

        if (!_mobThreshold.TryGetIncapThreshold(uid, out var critThresh, thresholds) || !critThresh.HasValue)
            return;

        var crit = critThresh.Value;
        if (crit <= FixedPoint2.Zero)
            return;

        FixedPoint2? ratioDen = null;
        if (_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var deadThresh, thresholds))
            ratioDen = deadThresh;
        else
            ratioDen = crit;

        float asphyxRatio = 0f;
        if (ratioDen is { } den && den > FixedPoint2.Zero)
        {
            var dict = _damageable.GetAllDamage((uid, damageable)).DamageDict;
            var asphyxKey = new ProtoId<DamageTypePrototype>("Asphyxiation");
            if (dict.TryGetValue(asphyxKey, out var asphyx))
                asphyxRatio = (asphyx / den).Float();
        }

        if (asphyxRatio >= _cfg.GetCVar(TSFCVars.TsfDefibrillatorAsphyxBlockRatio) && _random.Prob(0.5f))
        {
            ev.AllowRevive = false;
            return;
        }

        if (!TryComp(uid, out BloodstreamComponent? bloodstream))
            return;

        var bloodLevel = _bloodstream.GetBloodLevel((uid, bloodstream));
        var minBlood = _cfg.GetCVar(TSFCVars.TsfDefibrillatorMinBlood);
        var strict = _cfg.GetCVar(TSFCVars.TsfDefibrillatorMinBloodStrict);

        if (bloodLevel < strict)
        {
            ev.AllowRevive = false;
            return;
        }

        if (bloodLevel < minBlood && _random.Prob(0.45f))
            ev.AllowRevive = false;
    }
}
