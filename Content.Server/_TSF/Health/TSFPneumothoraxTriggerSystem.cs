// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._TSF.Consciousness;
using Content.Shared._TSF;
using Content.Shared._TSF.Health;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._TSF.Health;

public sealed class TSFPneumothoraxTriggerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly TimeSpan StatusRefresh = TimeSpan.FromSeconds(8);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PneumothoraxComponent, RejuvenateEvent>(OnRejuvenate);
    }
    public void HandleDamageChangedForRelay(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        OnDamage(ent, ref args);
    }

    private void OnRejuvenate(Entity<PneumothoraxComponent> ent, ref RejuvenateEvent args)
    {
        RemCompDeferred<PneumothoraxComponent>(ent);
    }

    private void OnDamage(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (_mobState.IsDead(ent))
            return;

        if (HasComp<PneumothoraxComponent>(ent))
            return;

        if (!args.DamageDelta.DamageDict.TryGetValue("Piercing", out var pierce) || pierce <= FixedPoint2.Zero)
            return;

        var min = _cfg.GetCVar(TSFCVars.TsfPneumothoraxMinPiercing);
        if (pierce.Float() < min)
            return;

        if (!_random.Prob(_cfg.GetCVar(TSFCVars.TsfPneumothoraxChance)))
            return;

        EnsureComp<PneumothoraxComponent>(ent);
        _statusEffects.TryAddStatusEffectDuration(ent, "StatusEffectPneumothorax", StatusRefresh);
    }
}
