// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._TSF.Consciousness;
using Content.Server._TSF.DamageEffects;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._TSF.Health;

public sealed class TSFDamageChangedHubSystem : EntitySystem
{
    [Dependency] private readonly TraumaticShockSystem _traumatic = default!;
    [Dependency] private readonly TSFHeavyBleedProcSystem _heavyBleed = default!;
    [Dependency] private readonly TSFPneumothoraxTriggerSystem _pneumo = default!;
    [Dependency] private readonly TSFPainCrySystem _painCry = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        _traumatic.HandleDamageChangedForRelay(ent, ref args);
        _heavyBleed.HandleDamageChangedForRelay(ent, ref args);
        _pneumo.HandleDamageChangedForRelay(ent, ref args);
        _painCry.HandleDamageChangedForRelay(ent, ref args);
    }
}
