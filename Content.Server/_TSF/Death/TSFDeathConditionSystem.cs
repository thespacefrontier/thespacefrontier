using Content.Shared._TSF.Organs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._TSF.Death;

/// <summary>
/// Death occurs ONLY when brain damage >= BrainDeathThreshold.
/// Heart failure, lung failure, and blood loss cause INDIRECT brain damage over time.
/// Head amputation = instant brain death.
/// This system ticks every frame and is the SOLE authority on mob death.
/// </summary>
public sealed class TSFDeathConditionSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TSFOrganDamageComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var organs, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            // ── HEAD AMPUTATED → instant brain death ──
            if (organs.HeadAmputated)
            {
                organs.Brain = 1f;
                Dirty(uid, organs);
                _mobState.ChangeMobState(uid, MobState.Dead);
                continue;
            }

            // ── INDIRECT brain damage chains ──
            var brainDelta = 0f;

            // Heart stopped → brain hypoxia
            organs.HeartStopped = organs.Heart >= organs.HeartStopThreshold;
            if (organs.HeartStopped)
                brainDelta += organs.BrainDamageFromHeartStop * frameTime;

            // Both lungs critically damaged → O2 deprivation
            if (organs.LungLeft >= organs.LungCriticalThreshold &&
                organs.LungRight >= organs.LungCriticalThreshold)
            {
                brainDelta += organs.BrainDamageFromLungFailure * frameTime;
            }

            // Critical blood loss → brain hypoxia
            if (TryComp<BloodstreamComponent>(uid, out var blood))
            {
                var bloodLevel = _bloodstream.GetBloodLevel((uid, blood));
                if (bloodLevel < organs.BloodCriticalFraction)
                    brainDelta += organs.BrainDamageFromBloodLoss * frameTime;
            }

            // Apply indirect brain damage
            if (brainDelta > 0f)
            {
                organs.Brain = Math.Min(organs.Brain + brainDelta, 1f);
                Dirty(uid, organs);
            }

            // ── BRAIN DEATH CHECK ──
            if (organs.Brain >= organs.BrainDeathThreshold)
            {
                _mobState.ChangeMobState(uid, MobState.Dead);
            }
        }
    }
}
