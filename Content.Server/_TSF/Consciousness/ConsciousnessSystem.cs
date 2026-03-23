// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF;
using Content.Shared._TSF.Consciousness;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._TSF.Consciousness;

public sealed class ConsciousnessSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<ConsciousnessComponent> ent, ref RejuvenateEvent args)
    {
        if (!ent.Comp.Unconscious && ent.Comp.Level >= 1f)
            return;

        var wasUnconscious = ent.Comp.Unconscious;
        ent.Comp.Level = 1f;
        ent.Comp.Unconscious = false;
        Dirty(ent, ent.Comp);
        if (wasUnconscious)
            _standing.Stand(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var unconsciousThreshold = _cfg.GetCVar(TSFCVars.TsfPainUnconsciousThreshold);
        var painToConsciousnessFactor = _cfg.GetCVar(TSFCVars.TsfPainToConsciousnessFactor);
        var painBlend = _cfg.GetCVar(TSFCVars.TsfConsciousnessPainRatioBlend);
        var bloodDamageBlend = _cfg.GetCVar(TSFCVars.TsfConsciousnessBloodDamageRatioBlend);
        var hypovolemiaBlend = _cfg.GetCVar(TSFCVars.TsfConsciousnessHypovolemiaBlend);
        var hypovolemiaBloodStart = _cfg.GetCVar(TSFCVars.TsfConsciousnessHypovolemiaBloodStart);
        var shockBlend = _cfg.GetCVar(TSFCVars.TsfConsciousnessTraumaticShockBlend);
        var asphyxBlend = _cfg.GetCVar(TSFCVars.TsfConsciousnessAsphyxiationBlend);
        var painGlobal = _cfg.GetCVar(TSFCVars.TsfPainGlobalMultiplier);
        var weightSetId = _cfg.GetCVar(TSFCVars.TsfPainWeightSet);
        if (!_proto.TryIndex<TSFPainWeightPrototype>(weightSetId, out var painWeights) &&
            !_proto.TryIndex<TSFPainWeightPrototype>("TSFPainWeights", out painWeights))
        {
            return;
        }

        var ensureQuery = EntityQueryEnumerator<MobThresholdsComponent, DamageableComponent>();
        while (ensureQuery.MoveNext(out var uid, out var thresholds, out _))
        {
            if (thresholds.ShowOverlays && !HasComp<ConsciousnessComponent>(uid))
                EnsureComp<ConsciousnessComponent>(uid);
        }

        var query = EntityQueryEnumerator<ConsciousnessComponent, DamageableComponent, MobThresholdsComponent>();
        while (query.MoveNext(out var uid, out var consciousness, out var damageable, out var thresholds))
        {
            if (!thresholds.ShowOverlays)
            {
                if (consciousness.Unconscious || consciousness.Level < 1f)
                {
                    var hadUnconsciousFlag = consciousness.Unconscious;
                    consciousness.Level = 1f;
                    consciousness.Unconscious = false;
                    Dirty(uid, consciousness);
                    if (hadUnconsciousFlag)
                        _standing.Stand(uid);
                }

                continue;
            }

            if (!_mobThreshold.TryGetIncapThreshold(uid, out var critThreshold, thresholds) || !critThreshold.HasValue)
            {
                consciousness.Level = 1f;
                consciousness.Unconscious = false;
                Dirty(uid, consciousness);
                continue;
            }

            var thresh = critThreshold.Value;
            if (thresh <= FixedPoint2.Zero)
            {
                consciousness.Level = 1f;
                consciousness.Unconscious = false;
                Dirty(uid, consciousness);
                continue;
            }

            float painLevel = 0f;
            if (!_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(uid, out _))
            {
                painLevel = SharedPainMath.ComputePainLevel(damageable, _proto, painWeights, painGlobal);
            }

            var painRatio = SharedTsfConsciousnessFormula.ComputePainRatio(painLevel, thresh);
            var bloodDamageRatio = Math.Clamp(SharedPainMath.ComputeBloodlossStyleContribution(damageable, thresh), 0f, 1f);
            var asphyxRatio = SharedTsfConsciousnessFormula.ComputeAsphyxiationRatio(
                SharedPainMath.GetAsphyxiationDamage(damageable),
                thresh);

            float hypovolemiaRatio = 0f;
            if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
            {
                var bloodLevel = _bloodstream.GetBloodLevel((uid, bloodstream));
                hypovolemiaRatio = SharedTsfConsciousnessFormula.ComputeHypovolemiaRatio(bloodLevel, hypovolemiaBloodStart);
            }

            var shockSeverity = TryComp<TraumaticShockComponent>(uid, out var shock) ? shock.Severity : 0f;

            var level = SharedTsfConsciousnessFormula.ComputeLevel(
                painRatio,
                bloodDamageRatio,
                hypovolemiaRatio,
                shockSeverity,
                asphyxRatio,
                painBlend,
                bloodDamageBlend,
                hypovolemiaBlend,
                shockBlend,
                asphyxBlend,
                painToConsciousnessFactor);

            var wasUnconscious = consciousness.Unconscious;
            consciousness.Level = level;
            consciousness.Unconscious = level < unconsciousThreshold;

            if (consciousness.Unconscious && !wasUnconscious)
                _standing.Down(uid);
            else if (!consciousness.Unconscious && wasUnconscious)
                _standing.Stand(uid);

            Dirty(uid, consciousness);
        }
    }
}
