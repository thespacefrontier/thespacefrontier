// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._TSF.BloodCough;

public sealed class BloodCoughSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;

    private static readonly SoundSpecifier[] MaleCoughs =
    {
        new SoundPathSpecifier("/Audio/_TSF/Cough/Male/male_cough1.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Cough/Male/male_cough2.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Cough/Male/male_cough3.ogg"),
    };
    private static readonly SoundSpecifier[] FemaleCoughs =
    {
        new SoundPathSpecifier("/Audio/_TSF/Cough/Female/female_cough1.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Cough/Female/female_cough2.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Cough/Female/female_cough3.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Cough/Female/female_cough4.ogg"),
    };

    private const float TriggerChance = 0.45f;
    private const float MinBleedAmountForCough = 4f;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CooldownAfterCough = TimeSpan.FromSeconds(90);
    private const float SlowdownSeconds = 3f;
    private const float SlowdownMultiplier = 0.5f;
    private static readonly FixedPoint2 BloodSpillAmount = FixedPoint2.New(18);

    private readonly Dictionary<EntityUid, TimeSpan> _nextRollAt = new();
    private readonly Dictionary<EntityUid, TimeSpan> _cooldownUntil = new();

    public override void Initialize()
    {
        base.Initialize();
        EntityManager.EntityDeleted += OnEntityDeleted;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityDeleted -= OnEntityDeleted;
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> entity)
    {
        _nextRollAt.Remove(entity.Owner);
        _cooldownUntil.Remove(entity.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BloodstreamComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out var bloodstream, out _))
        {
            if (_mobState.IsDead(uid))
                continue;
            if (_cooldownUntil.TryGetValue(uid, out var until) && now < until)
                continue;
            if (_nextRollAt.TryGetValue(uid, out var nextRoll) && now < nextRoll)
                continue;

            _nextRollAt[uid] = now + CheckInterval;

            if (bloodstream.BleedAmount < MinBleedAmountForCough)
                continue;
            if (!_random.Prob(TriggerChance))
                continue;

            var gender = _humanoidProfile.GetGender(uid);
            CoughBlood(uid, bloodstream, gender);
            _cooldownUntil[uid] = now + CooldownAfterCough;
        }
    }

    private void CoughBlood(EntityUid uid, BloodstreamComponent bloodstream, Gender gender)
    {
        var sound = gender switch
        {
            Gender.Male => _random.Pick(MaleCoughs),
            Gender.Female => _random.Pick(FemaleCoughs),
            _ => _random.Prob(0.5f) ? _random.Pick(MaleCoughs) : _random.Pick(FemaleCoughs),
        };

        _audio.PlayPvs(sound, uid, AudioParams.Default.WithVariation(0.15f));

        if (TryComp(uid, out BloodstreamComponent? bloodComp)
            && _solutionContainer.ResolveSolution(uid, bloodComp.BloodSolutionName, ref bloodComp.BloodSolution))
        {
            var leaked = _solutionContainer.SplitSolution(bloodComp.BloodSolution.Value, BloodSpillAmount);
            if (leaked.Volume > 0)
                _puddle.TrySpillAt(uid, leaked, out _, sound: false);
        }

        _movementMod.TryUpdateMovementSpeedModDuration(
            uid,
            MovementModStatusSystem.BloodCoughSlowdown,
            TimeSpan.FromSeconds(SlowdownSeconds),
            SlowdownMultiplier);
    }
}
