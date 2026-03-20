using Content.Shared._TSF.Pain;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Server._TSF.Pain;

/// <summary>
/// Pain and shock system.
/// - Pain decays naturally over time.
/// - Shock grows when pain exceeds threshold or blood is critically low.
/// - Shock decays when pain is below threshold.
/// - Pain causes: movement slow, disorientation (stuns), knockdown.
/// - Both pain and shock feed into the ConsciousnessSystem.
/// </summary>
public sealed class TSFPainShockSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>Blood level fraction below which shock increases from blood loss.</summary>
    private const float BloodShockThreshold = 0.45f;

    /// <summary>Shock growth rate from blood loss (per second).</summary>
    private const float BloodShockGrowthRate = 2.0f;

    /// <summary>How often effects tick (disorientation, knockdown).</summary>
    private float _effectAccumulator;
    private const float EffectInterval = 0.5f;

    /// <summary>Pain level at which movement starts slowing.</summary>
    private const float SlowdownStartPain = 30f;

    /// <summary>Max movement speed multiplier reduction from pain (at MaxPain).</summary>
    private const float MaxPainSlowFraction = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TSFPainComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    /// <summary>
    /// Applies movement speed debuff based on current pain level.
    /// </summary>
    private void OnRefreshSpeed(EntityUid uid, TSFPainComponent pain, RefreshMovementSpeedModifiersEvent args)
    {
        var effectivePain = GetEffectivePain(pain);
        if (effectivePain <= SlowdownStartPain)
            return;

        // Linear slowdown from 30 to MaxPain: 0% → MaxPainSlowFraction
        var fraction = Math.Clamp((effectivePain - SlowdownStartPain) / (pain.MaxPain - SlowdownStartPain), 0f, MaxPainSlowFraction);
        var speedMul = 1f - fraction;
        args.ModifySpeed(speedMul, speedMul);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _effectAccumulator += frameTime;
        var doEffects = _effectAccumulator >= EffectInterval;
        if (doEffects)
            _effectAccumulator = 0f;

        var query = EntityQueryEnumerator<TSFPainComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var pain, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            var prevPain = pain.Pain;

            if (pain.PainkillerStrength > 0f)
            {
                pain.PainkillerStrength = Math.Max(pain.PainkillerStrength - 0.02f * frameTime, 0f);
            }

            // ── Pain decay ──
            if (pain.Pain > 0f)
            {
                var decayRate = pain.PainDecayRate;
                if (pain.PainkillerStrength > 0f)
                    decayRate *= 1f + pain.PainkillerStrength * 3f; // morphine (0.9) → ~3.7x decay, ibuprofen (0.4) → ~2.2x decay
                pain.Pain = Math.Max(pain.Pain - decayRate * frameTime, 0f);
            }

            // ── Effective pain (after adrenaline/painkiller) ──
            var effectivePain = GetEffectivePain(pain);

            // ── Shock growth from pain ──
            if (effectivePain > pain.ShockGrowthThreshold)
            {
                var excess = effectivePain - pain.ShockGrowthThreshold;
                var growthRate = pain.ShockGrowthRate * (excess / (pain.MaxPain - pain.ShockGrowthThreshold));
                pain.Shock = Math.Min(pain.Shock + growthRate * frameTime, pain.MaxShock);
            }

            // ── Shock growth from blood loss ──
            if (TryComp<BloodstreamComponent>(uid, out var blood))
            {
                var bloodLevel = _bloodstream.GetBloodLevel((uid, blood));
                if (bloodLevel < BloodShockThreshold)
                {
                    var deficit = BloodShockThreshold - bloodLevel;
                    pain.Shock = Math.Min(pain.Shock + BloodShockGrowthRate * deficit * frameTime, pain.MaxShock);
                }
            }

            // ── Shock decay (only when effective pain is below growth threshold) ──
            if (effectivePain < pain.ShockGrowthThreshold && pain.Shock > 0f)
            {
                pain.Shock = Math.Max(pain.Shock - pain.ShockDecayRate * frameTime, 0f);
            }

            // ── Refresh movement speed when pain changes significantly ──
            if (MathF.Abs(prevPain - pain.Pain) > 1f)
                _movementSpeed.RefreshMovementSpeedModifiers(uid);

            // ── Periodic effects ──
            if (doEffects)
            {
                // Disorientation at moderate pain — knockdown (fall + can't get up briefly)
                if (effectivePain > pain.DisorientThreshold && effectivePain <= pain.RagdollThreshold)
                {
                    // Probability scales with pain: 15%-35%
                    var prob = 0.15f + 0.2f * ((effectivePain - pain.DisorientThreshold) / (pain.RagdollThreshold - pain.DisorientThreshold));
                    if (_random.Prob(prob))
                        _stun.TryKnockdown((uid, null), TimeSpan.FromSeconds(1.5));
                }

                // Knockdown at extreme pain — very reliable, longer duration
                if (effectivePain > pain.RagdollThreshold)
                {
                    // Probability scales 40%-70%
                    var prob = 0.4f + 0.3f * Math.Clamp((effectivePain - pain.RagdollThreshold) / (pain.MaxPain - pain.RagdollThreshold), 0f, 1f);
                    if (_random.Prob(prob))
                        _stun.TryKnockdown((uid, null), TimeSpan.FromSeconds(3));
                }

                // Immediate knockdown on pain spike (> 40 in one tick)
                var painSpike = pain.Pain - prevPain + pain.PainDecayRate * EffectInterval;
                if (painSpike > 40f)
                    _stun.TryKnockdown((uid, null), TimeSpan.FromSeconds(2));
            }

            Dirty(uid, pain);
        }
    }

    private static float GetEffectivePain(TSFPainComponent pain)
    {
        var effectivePain = pain.Pain;
        if (pain.PainkillerStrength > 0f)
            effectivePain *= 1f - pain.PainkillerStrength;
        else if (pain.AdrenalineFactor > 0f)
            effectivePain *= 1f - pain.AdrenalineFactor;
        return effectivePain;
    }
}