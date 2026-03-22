using Content.Shared._TSF.Health;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._TSF.Health;

public sealed class TSFPneumothoraxDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly FixedPoint2 AsphyxPerTick = FixedPoint2.New(0.45f);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(4);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PneumothoraxComponent>();
        while (query.MoveNext(out var uid, out var pn))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (_timing.CurTime < pn.NextDamage)
                continue;

            pn.NextDamage = _timing.CurTime + Interval;
            Dirty(uid, pn);

            var spec = new DamageSpecifier();
            spec.DamageDict["Asphyxiation"] = AsphyxPerTick;
            _damageable.TryChangeDamage(uid, spec, ignoreResistances: false);
        }
    }
}
