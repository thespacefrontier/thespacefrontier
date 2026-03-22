using Content.Shared._TSF.Consciousness;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Mobs.Systems;

namespace Content.Server._TSF.Health;

public sealed class TSFEpinephrineTraumaticShockSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private static readonly ReagentId Epinephrine = new("Epinephrine", null);
    private const float MinEpinephrineU = 0.2f;
    private const float ShockBoostPerSecond = 0.35f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent, TraumaticShockComponent>();
        while (query.MoveNext(out var uid, out var blood, out var shock))
        {
            if (_mobState.IsDead(uid) || shock.Severity <= 0f)
                continue;

            if (!_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var sol))
                continue;

            var qty = sol.GetReagentQuantity(Epinephrine);
            if (qty.Float() < MinEpinephrineU)
                continue;

            shock.Severity = Math.Max(0f, shock.Severity - ShockBoostPerSecond * frameTime);
            Dirty(uid, shock);
        }
    }
}
