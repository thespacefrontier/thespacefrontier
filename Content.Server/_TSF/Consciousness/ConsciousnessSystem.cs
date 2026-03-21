using Content.Shared._TSF.Consciousness;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;

namespace Content.Server._TSF.Consciousness;

public sealed class ConsciousnessSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private const float UnconsciousThreshold = 0.22f;
    private const float PainToConsciousnessFactor = 0.85f;
    private static readonly Dictionary<string, float> PainMultipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Brute"] = 1f,
        ["Burn"] = 1.2f,
        ["Slash"] = 1.1f,
        ["Piercing"] = 1f,
        ["Blunt"] = 0.9f,
        ["Caustic"] = 1f,
        ["Poison"] = 0.8f,
        ["Asphyxiation"] = 0.7f,
        ["Bloodloss"] = 0.8f,
    };
    private const string AirlossGroup = "Airloss";
    private const string ToxinGroup = "Toxin";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

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
                continue;

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
                foreach (var painType in damageable.PainDamageGroups)
                {
                    var groupId = painType.ToString();
                    if (damageable.DamagePerGroup.TryGetValue(groupId, out var pain))
                        painLevel += pain.Float() * GetPainMultiplier(groupId);
                }
            }
            else
            {
                painLevel = 0f;
            }

            float bloodLossContrib = 0f;
            var deathLevel = FixedPoint2.Zero;
            if (damageable.DamagePerGroup.TryGetValue(AirlossGroup, out var airloss))
                deathLevel += airloss;
            if (damageable.DamagePerGroup.TryGetValue(ToxinGroup, out var toxin))
                deathLevel += toxin;
            if (thresh > FixedPoint2.Zero)
                bloodLossContrib = (deathLevel / thresh).Float();

            var painRatio = (FixedPoint2.New(painLevel) / thresh).Float();
            painRatio = Math.Clamp(painRatio, 0f, 1.5f);
            var bloodRatio = Math.Clamp(bloodLossContrib, 0f, 1f);
            var combined = painRatio * 0.85f + bloodRatio * 0.25f;
            var level = Math.Clamp(1f - combined * PainToConsciousnessFactor, 0f, 1f);

            var wasUnconscious = consciousness.Unconscious;
            consciousness.Level = level;
            consciousness.Unconscious = level < UnconsciousThreshold;

            if (consciousness.Unconscious && !wasUnconscious)
                _standing.Down(uid);
            else if (!consciousness.Unconscious && wasUnconscious)
                _standing.Stand(uid);

            Dirty(uid, consciousness);
        }
    }

    private static float GetPainMultiplier(string damageGroupId)
    {
        return PainMultipliers.TryGetValue(damageGroupId, out var m) ? m : 1f;
    }
}
