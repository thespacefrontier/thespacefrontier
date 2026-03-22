using Content.Shared._TSF;
using Content.Shared.Body.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Server.Body.Systems;
using Robust.Shared.Configuration;

namespace Content.Server._TSF.Shock;

public sealed class TSFHypovolemicShockSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly TimeSpan ShockRefreshDuration = TimeSpan.FromSeconds(6);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shockThreshold = _cfg.GetCVar(TSFCVars.TsfHypovolemicShockBloodThreshold);
        var recoveryThreshold = _cfg.GetCVar(TSFCVars.TsfHypovolemicShockRecoveryBloodThreshold);

        var query = EntityQueryEnumerator<BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var bloodstream))
        {
            if (_mobState.IsDead(uid))
                continue;

            var bloodLevel = _bloodstream.GetBloodLevel((uid, bloodstream));
            if (bloodLevel < shockThreshold)
                _statusEffects.TryAddStatusEffectDuration(uid, "StatusEffectHypovolemicShock", ShockRefreshDuration);
            else if (bloodLevel > recoveryThreshold)
                _statusEffects.TryRemoveStatusEffect(uid, "StatusEffectHypovolemicShock");
        }
    }
}
