// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF.Health;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Timing;

namespace Content.Server._TSF.Health;

public sealed class TSFWoundInfectionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float HeavyBleedFraction = 0.38f;
    private const float SecondsToInfect = 140f;
    private const float InfectionIntervalSeconds = 50f;
    private static readonly FixedPoint2 PoisonPerTick = FixedPoint2.New(0.35f);
    private const float SepsisStage1Seconds = 120f;
    private const float SepsisStage2Seconds = 280f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundInfectionTrackerComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<WoundInfectionTrackerComponent> ent, ref RejuvenateEvent args)
    {
        RemCompDeferred<WoundInfectionTrackerComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var blood, out var damageable, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            var threshold = blood.MaxBleedAmount * HeavyBleedFraction;
            if (blood.BleedAmount < threshold)
            {
                if (TryComp<WoundInfectionTrackerComponent>(uid, out var tr))
                {
                    tr.HeavyBleedSeconds = 0;
                    if (tr.Infected)
                    {
                        tr.Infected = false;
                        tr.SepsisStage = 0;
                        tr.SepsisAccumSeconds = 0f;
                        Dirty(uid, tr);
                    }
                }

                continue;
            }

            var tracker = EnsureComp<WoundInfectionTrackerComponent>(uid);
            tracker.HeavyBleedSeconds += frameTime;

            if (tracker.HeavyBleedSeconds < SecondsToInfect)
            {
                Dirty(uid, tracker);
                continue;
            }

            if (_timing.CurTime < tracker.NextInfectionDamage)
            {
                Dirty(uid, tracker);
                continue;
            }

            var poison = new DamageSpecifier();
            var tick = PoisonPerTick;
            if (tracker.SepsisStage >= 2)
                tick += FixedPoint2.New(0.25f);
            else if (tracker.SepsisStage == 1)
                tick += FixedPoint2.New(0.12f);

            poison.DamageDict["Poison"] = tick;
            _damageable.TryChangeDamage(uid, poison, ignoreResistances: false);

            tracker.Infected = true;
            tracker.NextInfectionDamage = _timing.CurTime + TimeSpan.FromSeconds(InfectionIntervalSeconds);
            tracker.HeavyBleedSeconds = 0f;
            Dirty(uid, tracker);
        }

        var sepsisQuery = EntityQueryEnumerator<WoundInfectionTrackerComponent>();
        while (sepsisQuery.MoveNext(out var uid, out var tr))
        {
            if (!tr.Infected || _mobState.IsDead(uid))
                continue;

            tr.SepsisAccumSeconds += frameTime;
            if (tr.SepsisAccumSeconds >= SepsisStage2Seconds)
                tr.SepsisStage = 2;
            else if (tr.SepsisAccumSeconds >= SepsisStage1Seconds)
                tr.SepsisStage = Math.Max(tr.SepsisStage, (byte) 1);

            Dirty(uid, tr);
        }
    }
}
