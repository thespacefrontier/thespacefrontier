using Content.Shared._TSF.Consciousness;
using Content.Shared._TSF.Organs;
using Content.Shared._TSF.Pain;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;

namespace Content.Server._TSF.Consciousness;

/// <summary>
/// Consciousness model. Consciousness is a composite value derived from:
///   - Blood volume (low blood → low consciousness)
///   - Shock (high shock → low consciousness)
///   - Brain damage (high damage → low consciousness)
/// When consciousness drops below threshold → mob enters Critical state (unconscious).
/// When it recovers above recovery threshold → mob returns to Alive.
/// This system is the SOLE authority for the Critical state.
/// </summary>
public sealed class ConsciousnessSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    /// <summary>Below this → unconscious (MobState.Critical).</summary>
    private const float UnconsciousThreshold = 0.1f;

    /// <summary>Above this → wake up (MobState.Alive). Hysteresis prevents flickering.</summary>
    private const float RecoveryThreshold = 0.3f;

    /// <summary>Below this → ragdoll (fall down but still conscious).</summary>
    private const float RagdollThreshold = 0.4f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConsciousnessComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var consciousness, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            var level = CalculateConsciousness(uid);

            var wasUnconscious = consciousness.Unconscious;
            consciousness.Level = level;

            // Determine unconscious state with hysteresis
            if (!wasUnconscious && level < UnconsciousThreshold)
                consciousness.Unconscious = true;
            else if (wasUnconscious && level > RecoveryThreshold)
                consciousness.Unconscious = false;

            // ── Trigger MobState changes ──
            if (consciousness.Unconscious && mobState.CurrentState != MobState.Critical)
            {
                _mobState.ChangeMobState(uid, MobState.Critical);
            }
            else if (!consciousness.Unconscious && mobState.CurrentState == MobState.Critical)
            {
                _mobState.ChangeMobState(uid, MobState.Alive);
            }

            // fall down when consciousness is low but not unconscious
            if (level < RagdollThreshold && !consciousness.Unconscious)
            {
                _standing.Down(uid);
            }
            else if (level >= RagdollThreshold && !consciousness.Unconscious)
            {
                _standing.Stand(uid);
            }

            // Down when unconscious
            if (consciousness.Unconscious)
                _standing.Down(uid);

            Dirty(uid, consciousness);
        }
    }

    /// <summary>
    /// Consciousness formula:
    /// consciousness = bloodFactor * (1 - brainDamage) * (1 - shockFactor)
    /// Where bloodFactor = min(currentBlood / referenceBlood, 1)
    ///       shockFactor = clamp(shock / shockMax, 0, 1)
    /// </summary>
    private float CalculateConsciousness(EntityUid uid)
    {
        // ── Blood factor ──
        var bloodFactor = 1f;
        if (TryComp<BloodstreamComponent>(uid, out var blood))
        {
            var bloodLevel = _bloodstream.GetBloodLevel((uid, blood));
            // GetBloodLevel returns 0-2 (MaxVolumeModifier), normalize to 0-1
            bloodFactor = Math.Clamp(bloodLevel / 1f, 0f, 1f);
        }

        // ── Brain damage factor ──
        var brainFactor = 0f;
        if (TryComp<TSFOrganDamageComponent>(uid, out var organs))
        {
            brainFactor = Math.Clamp(organs.Brain, 0f, 1f);

            // Heart stopped → additional consciousness penalty
            if (organs.HeartStopped)
                bloodFactor *= 0.3f;
        }

        // ── Shock factor ──
        var shockFactor = 0f;
        if (TryComp<TSFPainComponent>(uid, out var pain))
        {
            shockFactor = Math.Clamp(pain.Shock / pain.MaxShock, 0f, 1f);
        }

        var level = bloodFactor * (1f - brainFactor) * (1f - shockFactor * 0.8f);
        return Math.Clamp(level, 0f, 1f);
    }
}
