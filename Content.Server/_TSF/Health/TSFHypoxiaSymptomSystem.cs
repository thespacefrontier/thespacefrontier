// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF.Consciousness;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server._TSF.Health;

public sealed class TSFHypoxiaSymptomSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(4);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DamageableComponent, MobThresholdsComponent>();
        while (query.MoveNext(out var uid, out var dmg, out var thresholds))
        {
            if (_mobState.IsDead(uid) || !thresholds.ShowOverlays)
                continue;

            if (!_mobThreshold.TryGetIncapThreshold(uid, out var crit, thresholds) || !crit.HasValue || crit.Value <= FixedPoint2.Zero)
                continue;

            var asphyx = SharedPainMath.GetAsphyxiationDamage(dmg);
            var ratio = asphyx / crit.Value.Float();
            if (ratio >= 0.22f)
                _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectTsfHypoxiaCue", Refresh);
            else
                _statusEffects.TryRemoveStatusEffect(uid, "StatusEffectTsfHypoxiaCue");
        }
    }
}
