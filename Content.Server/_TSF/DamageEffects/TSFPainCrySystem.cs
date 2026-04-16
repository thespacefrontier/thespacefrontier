// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._TSF.DamageEffects;

public sealed class TSFPainCrySystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private static readonly FixedPoint2 MinDamageForScream = FixedPoint2.New(15);
    private const float ScreamChance = 0.45f;
    private const float CooldownSeconds = 9.0f;
    private readonly Dictionary<EntityUid, TimeSpan> _cooldownUntil = new();

    public override void Initialize()
    {
        base.Initialize();
        EntityManager.EntityDeleted += OnEntityDeleted;
    }

    public void HandleDamageChangedForRelay(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        OnDamageChanged(ent, ref args);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityDeleted -= OnEntityDeleted;
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> entity)
    {
        _cooldownUntil.Remove(entity.Owner);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!HasComp<MobStateComponent>(ent))
            return;

        var damageDelta = args.DamageDelta;

        var total = damageDelta.GetTotal();
        if (total <= MinDamageForScream)
            return;
        if (!_random.Prob(ScreamChance))
            return;
        if (_mobState.IsDead(ent) || _mobState.IsCritical(ent))
            return;
        var now = _timing.CurTime;
        if (_cooldownUntil.TryGetValue(ent, out var until) && now < until)
            return;

        if (_chat.TryEmoteWithChat(ent.Owner, "PainScream"))
            _cooldownUntil[ent] = now + TimeSpan.FromSeconds(CooldownSeconds);
    }
}
