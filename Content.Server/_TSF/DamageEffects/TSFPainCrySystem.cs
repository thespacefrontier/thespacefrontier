using Content.Server._TSF.Surgery;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._TSF.DamageEffects;

public sealed class TSFPainCrySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TSFLimbDamageTriggerSystem _limbTrigger = default!;

    private static readonly SoundSpecifier[] MaleCries = new SoundSpecifier[]
    {
        new SoundPathSpecifier("/Audio/Voice/Human/cry_male_1.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_male_2.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_male_3.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_male_4.ogg"),
    };
    private static readonly SoundSpecifier[] FemaleCries = new SoundSpecifier[]
    {
        new SoundPathSpecifier("/Audio/Voice/Human/cry_female_1.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_female_2.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_female_3.ogg"),
        new SoundPathSpecifier("/Audio/Voice/Human/cry_female_4.ogg"),
    };

    private static readonly FixedPoint2 MinDamageForCry = FixedPoint2.New(3);
    private const float CooldownSeconds = 9.0f;
    private readonly Dictionary<EntityUid, TimeSpan> _cooldownUntil = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        EntityManager.EntityDeleted += OnEntityDeleted;
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

        var damageDelta = args.DamageDelta;
        _limbTrigger.OnDamageChangedForLimb(ent.Owner, ref args);

        var total = damageDelta.GetTotal();
        if (total < MinDamageForCry)
            return;
        if (_mobState.IsDead(ent) || _mobState.IsCritical(ent))
            return;
        var now = _timing.CurTime;
        if (_cooldownUntil.TryGetValue(ent, out var until) && now < until)
            return;

        if (!TryComp(ent, out HumanoidAppearanceComponent? appearance))
            return;

        var sound = appearance.Gender switch
        {
            Gender.Male => _random.Pick(MaleCries),
            Gender.Female => _random.Pick(FemaleCries),
            _ => _random.Prob(0.5f) ? _random.Pick(MaleCries) : _random.Pick(FemaleCries)
        };

        _audio.PlayPvs(sound, ent, AudioParams.Default);
        _cooldownUntil[ent] = now + TimeSpan.FromSeconds(CooldownSeconds);
    }
}
