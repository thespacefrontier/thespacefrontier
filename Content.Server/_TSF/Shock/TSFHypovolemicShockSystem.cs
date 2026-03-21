using Content.Shared.Body.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Server.Body.Systems;

namespace Content.Server._TSF.Shock;

public sealed class TSFHypovolemicShockSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private const float ShockBloodLevelThreshold = 0.45f;
    private const float RecoveryBloodLevelThreshold = 0.55f;
    private static readonly TimeSpan ShockRefreshDuration = TimeSpan.FromSeconds(6);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var bloodstream))
        {
            if (_mobState.IsDead(uid))
                continue;

            var bloodLevel = _bloodstream.GetBloodLevel((uid, bloodstream));
            if (bloodLevel < ShockBloodLevelThreshold)
                _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectHypovolemicShock", ShockRefreshDuration);
            else if (bloodLevel > RecoveryBloodLevelThreshold)
                _statusEffects.TryRemoveStatusEffect(uid, "StatusEffectHypovolemicShock");
        }
    }
}
