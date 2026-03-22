using Content.Shared.Atmos.Components;
using Content.Shared._TSF.Consciousness;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._TSF.Health;

public sealed class TSFStressSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private float _accum;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += frameTime;
        if (_accum < 2.5f)
            return;

        _accum = 0f;

        var query = EntityQueryEnumerator<ConsciousnessComponent, MobThresholdsComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var conc, out var thresholds, out var mob))
        {
            if (!thresholds.ShowOverlays || _mobState.IsDead(uid) || mob.CurrentState != MobState.Alive)
                continue;

            var shockSev = TryComp<TraumaticShockComponent>(uid, out var shock) ? shock.Severity : 0f;
            var onFire = TryComp<FlammableComponent>(uid, out var flammable) && flammable.OnFire;
            var bad = conc.Level < 0.42f || shockSev > 0.55f || onFire;
            if (!bad)
                continue;

            var chance = onFire ? 0.2f : 0.12f;
            if (!_random.Prob(chance))
                continue;

            if (TryComp<StaminaComponent>(uid, out var stam))
                _stamina.TakeStaminaDamage(uid, 3f, stam, visual: true);

            _popup.PopupEntity(Loc.GetString("tsf-stress-tension"), uid, uid, PopupType.Small);
        }
    }
}
