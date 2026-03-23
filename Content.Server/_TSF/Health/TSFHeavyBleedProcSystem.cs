// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._TSF.Consciousness;
using Content.Shared._TSF;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._TSF.Health;

public sealed class TSFHeavyBleedProcSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void HandleDamageChangedForRelay(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (_mobState.IsDead(ent))
            return;

        if (!TryComp(ent, out BloodstreamComponent? blood))
            return;

        var delta = args.DamageDelta;
        FixedPoint2 sharp = FixedPoint2.Zero;
        if (delta.DamageDict.TryGetValue("Slash", out var sl))
            sharp += sl;
        if (delta.DamageDict.TryGetValue("Piercing", out var pr))
            sharp += pr;

        if (sharp <= FixedPoint2.Zero)
            return;

        var chance = _cfg.GetCVar(TSFCVars.TsfArterialBleedProcChance);
        if (!_random.Prob(chance))
            return;

        var bonus = _cfg.GetCVar(TSFCVars.TsfArterialBleedBonus);
        _bloodstream.TryModifyBleedAmount((ent, blood), bonus);
    }
}
