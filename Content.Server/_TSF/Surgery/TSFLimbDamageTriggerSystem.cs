using Content.Server.Body.Systems;
using Content.Shared._TSF.Surgery;
using Content.Shared._TSF.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._TSF.Surgery;

/// <summary>
/// When an entity takes significant blunt/slash damage, may set a random limb to Dislocated
/// so surgery/analyzer can detect it. (MobStateChangedEvent cannot be used here due to single-subscriber limit.)
/// </summary>
public sealed class TSFLimbDamageTriggerSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly string[] LimbDamageTypes = { "Blunt", "Slash" };
    private static readonly SoundSpecifier[] CrunchSounds =
    {
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part1.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part2.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part3.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part4.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part5.ogg"),
    };
    private const float DamageDislocateChance = 0.06f; 
    private const float DamageThreshold = 15f;

    /// <summary>Fractures only from strong brute (Blunt). Higher threshold and separate chance.</summary>
    private const float FractureBluntThreshold = 28f;
    private const float FractureChance = 0.04f; 
    private const string BluntDamageType = "Blunt";

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>Called from TSFPainCrySystem (single DamageChangedEvent subscriber) to avoid duplicate subscription.</summary>
    public void OnDamageChangedForLimb(EntityUid uid, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;
        if (!TryComp<BodyComponent>(uid, out var body))
            return;
        var total = args.DamageDelta.GetTotal();
        if (total <= 0)
            return;

        // Fractures: only from strong blunt (brute) damage
        if (args.DamageDelta.DamageDict.TryGetValue(BluntDamageType, out var bluntDelta) && bluntDelta >= FixedPoint2.New(FractureBluntThreshold))
        {
            if (_random.Prob(FractureChance))
                SetRandomLimbToBroken(uid, body);
        }

        if (total < FixedPoint2.New(DamageThreshold))
            return;
        bool hasLimbDamage = false;
        foreach (var type in LimbDamageTypes)
        {
            if (args.DamageDelta.DamageDict.TryGetValue(type, out var v) && v > 0)
            {
                hasLimbDamage = true;
                break;
            }
        }
        if (!hasLimbDamage)
            return;
        if (!_random.Prob(DamageDislocateChance))
            return;
        SetRandomLimbToDislocated(uid, body);
    }

    private void SetRandomLimbToBroken(EntityUid bodyUid, BodyComponent body)
    {
        var candidates = new List<EntityUid>();
        foreach (var (partUid, part) in _body.GetBodyChildren(bodyUid, body))
        {
            if (part.PartType is BodyPartType.Arm or BodyPartType.Leg or BodyPartType.Hand or BodyPartType.Foot)
            {
                if (TryComp<LimbConditionComponent>(partUid, out var existing) && existing.Condition == LimbCondition.Broken)
                    continue;
                candidates.Add(partUid);
            }
        }
        if (candidates.Count == 0)
            return;
        var chosen = _random.Pick(candidates);
        var limbComp = EnsureComp<LimbConditionComponent>(chosen);
        limbComp.Condition = LimbCondition.Broken;
        limbComp.FractureStep = FractureSurgeryStep.None;
        Dirty(chosen, limbComp);
        _movementSpeed.RefreshMovementSpeedModifiers(bodyUid);
        _audio.PlayPvs(_random.Pick(CrunchSounds), bodyUid, AudioParams.Default.WithVolume(0.4f));
    }

    private void SetRandomLimbToDislocated(EntityUid bodyUid, BodyComponent body)
    {
        var candidates = new List<EntityUid>();
        foreach (var (partUid, part) in _body.GetBodyChildren(bodyUid, body))
        {
            if (part.PartType is BodyPartType.Arm or BodyPartType.Leg or BodyPartType.Hand or BodyPartType.Foot)
                candidates.Add(partUid);
        }
        if (candidates.Count == 0)
            return;
        var chosen = _random.Pick(candidates);
        var limbComp = EnsureComp<LimbConditionComponent>(chosen);
        limbComp.Condition = LimbCondition.Dislocated;
        Dirty(chosen, limbComp);
        _movementSpeed.RefreshMovementSpeedModifiers(bodyUid);
        _audio.PlayPvs(_random.Pick(CrunchSounds), bodyUid, AudioParams.Default.WithVolume(0.4f));
    }
}
