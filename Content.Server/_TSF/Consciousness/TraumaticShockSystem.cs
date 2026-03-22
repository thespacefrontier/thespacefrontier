using Content.Shared._TSF;
using Content.Shared._TSF.Consciousness;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;

namespace Content.Server._TSF.Consciousness;

public sealed class TraumaticShockSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraumaticShockComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public void HandleDamageChangedForRelay(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        OnDamageChanged(ent, ref args);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var decay = _cfg.GetCVar(TSFCVars.TsfTraumaticShockDecayPerSecond);
        if (decay <= 0f)
            return;

        var query = EntityQueryEnumerator<TraumaticShockComponent>();
        while (query.MoveNext(out var uid, out var shock))
        {
            if (shock.Severity <= 0f)
                continue;

            shock.Severity = Math.Max(0f, shock.Severity - decay * frameTime);
            Dirty(uid, shock);
        }
    }

    private void OnRejuvenate(Entity<TraumaticShockComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Severity = 0f;
        Dirty(ent, ent.Comp);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!TryComp(ent, out MobThresholdsComponent? thresholds) || !thresholds.ShowOverlays)
            return;

        if (_mobState.IsDead(ent))
            return;

        var add = _cfg.GetCVar(TSFCVars.TsfTraumaticShockAddPerDamage);
        if (add <= 0f)
            return;

        var positive = DamageSpecifier.GetPositive(args.DamageDelta);
        var total = positive.GetTotal();
        if (total <= FixedPoint2.Zero)
            return;

        var shock = EnsureComp<TraumaticShockComponent>(ent);
        shock.Severity = Math.Clamp(shock.Severity + total.Float() * add, 0f, 1f);
        Dirty(ent, shock);
    }
}
