using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
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

/// <summary>
/// Very rarely makes players "cough blood" when they have fairly heavy bleeding.
/// Triggers only when BleedAmount &gt;= threshold (large bleeding). Roll every 45s.
/// Plays gender-specific cough sounds, spills a small blood puddle, and applies slowdown like vomiting.
/// </summary>
public sealed class BloodCoughSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;

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

    /// <summary> Chance per check to trigger blood cough. </summary>
    private const float TriggerChance = 0.35f;
    /// <summary> Minimum bleed amount to allow trigger (fairly large bleeding only). </summary>
    private const float MinBleedAmountForCough = 4f;
    /// <summary> How often we roll for each entity. </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(45);
    /// <summary> Cooldown after coughing so it doesn't repeat immediately. </summary>
    private static readonly TimeSpan CooldownAfterCough = TimeSpan.FromSeconds(90);
    /// <summary> Slowdown duration in seconds (like vomiting). </summary>
    private const float SlowdownSeconds = 3f;
    /// <summary> Walk/sprint multiplier during cough (0.5 = half speed, like vomiting). </summary>
    private const float SlowdownMultiplier = 0.5f;
    /// <summary> Amount of blood spilled (similar scale to vomit puddle: ~(40+40)/6). </summary>
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
        var query = EntityQueryEnumerator<BloodstreamComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var bloodstream, out var appearance))
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

            CoughBlood(uid, bloodstream, appearance);
            _cooldownUntil[uid] = now + CooldownAfterCough;
        }
    }

    private void CoughBlood(EntityUid uid, BloodstreamComponent bloodstream, HumanoidAppearanceComponent appearance)
    {
        var sound = appearance.Gender switch
        {
            Gender.Male => _random.Pick(MaleCoughs),
            Gender.Female => _random.Pick(FemaleCoughs),
            _ => _random.Prob(0.5f) ? _random.Pick(MaleCoughs) : _random.Pick(FemaleCoughs),
        };

        _audio.PlayPvs(sound, uid, AudioParams.Default.WithVariation(0.15f));

        // Spill a small puddle of blood under the entity (direct spill so puddle always appears).
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

        // Show third-person emote message in chat (no extra sound — CoughBlood has no EmoteSounds)
        _chat.TryEmoteWithChat(uid, "CoughBlood", ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }
}
