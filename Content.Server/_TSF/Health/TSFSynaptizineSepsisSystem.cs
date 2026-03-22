using Content.Shared._TSF.Health;
using Content.Shared.Body.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
namespace Content.Server._TSF.Health;

public sealed class TSFSynaptizineSepsisSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly ReagentId Synaptizine = new("Synaptizine", null);
    private const float MinSynaptizineU = 0.25f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent, WoundInfectionTrackerComponent>();
        while (query.MoveNext(out var uid, out var blood, out var tracker))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (!_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var sol))
                continue;

            var qty = sol.GetReagentQuantity(Synaptizine);
            if (qty.Float() < MinSynaptizineU)
                continue;

            tracker.SepsisStage = 0;
            tracker.SepsisAccumSeconds = 0f;
            tracker.Infected = false;
            tracker.HeavyBleedSeconds = 0f;
            Dirty(uid, tracker);

            if (HasComp<PneumothoraxComponent>(uid))
                RemCompDeferred<PneumothoraxComponent>(uid);
            _statusEffects.TryRemoveStatusEffect(uid, "StatusEffectPneumothorax");
        }
    }
}
