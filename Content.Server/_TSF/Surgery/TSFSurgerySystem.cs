using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared._TSF.Surgery;
using Content.Shared._TSF.Surgery.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._TSF.Surgery;

public sealed class TSFSurgerySystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const float DislocationRepairChance = 0.85f;
    private const float DislocationRepairTime = 3f;
    private const float FractureIncisionTime = 3f;
    private const float FractureRetractorTime = 2f;
    private const float FractureBoneGelTime = 4f;
    private const float FractureBoneGelChance = 0.85f;
    private const float PainOnSuccess = 18f;
    private const float PainOnFail = 10f;
    /// <summary>Reduced pain when patient is asleep (e.g. under nitrous oxide).</summary>
    private const float PainAsleepSuccess = 5f;
    private const float PainAsleepFail = 3f;

    private static readonly SoundSpecifier[] CrunchSounds =
    {
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part1.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part2.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part3.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part4.ogg"),
        new SoundPathSpecifier("/Audio/_TSF/Effects/BrokenParts/broken_part5.ogg"),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DislocationRepairDoAfterEvent>(OnDislocationDoAfter);
        SubscribeLocalEvent<FractureSurgeryStepDoAfterEvent>(OnFractureStepDoAfter);

        Subs.BuiEvents<BodyComponent>(SurgeryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnSurgeryUIOpened);
            subs.Event<SurgeryActionRequestMessage>(OnSurgeryActionRequest);
        });
    }

    private void OnGetVerbs(EntityUid uid, BodyComponent body, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;
        if (!HasComp<DamageableComponent>(args.Target))
            return;

        if (_ui.HasUi(uid, SurgeryUiKey.Key))
        {
            var surgeryIcon = new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Medical/Surgery/scalpel.rsi"), "scalpel");
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("tsf-surgery-open-ui-verb"),
                Icon = surgeryIcon,
                Act = () =>
                {
                    _ui.OpenUi(uid, SurgeryUiKey.Key, args.User);
                    SendSurgeryState(uid);
                },
            });
        }

        // Test verbs: create a dislocated or broken limb for testing.
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("tsf-surgery-dislocate-limb-test-verb"),
            Act = () => DislocateRandomLimb(args.Target, body),
        });
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("tsf-surgery-break-limb-test-verb"),
            Act = () => BreakRandomLimb(args.Target, body),
        });
    }

    private static string GetPartDisplayName(BodyPartComponent part)
    {
        return part.Symmetry != BodyPartSymmetry.None
            ? $"{part.Symmetry} {part.PartType}"
            : part.PartType.ToString();
    }

    /// <summary>Slot flags that cover this body part (e.g. gloves for hands). If any of these slots have an item, surgery on that part is blocked.</summary>
    private static SlotFlags GetSlotFlagsForBodyPart(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Hand or BodyPartType.Arm => SlotFlags.GLOVES | SlotFlags.OUTERCLOTHING,
            BodyPartType.Leg or BodyPartType.Foot => SlotFlags.FEET | SlotFlags.LEGS,
            BodyPartType.Torso => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING,
            BodyPartType.Head => SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES | SlotFlags.EARS,
            _ => SlotFlags.NONE
        };
    }

    private bool IsLimbCoveredByClothing(EntityUid target, BodyPartComponent part)
    {
        var flags = GetSlotFlagsForBodyPart(part.PartType);
        if (flags == SlotFlags.NONE)
            return false;
        var enumerator = _inventory.GetSlotEnumerator((target, null), flags);
        return enumerator.NextItem(out _);
    }

    /// <summary>Debug/test: set a random limb to dislocated so you can try the repair verb.</summary>
    public void DislocateRandomLimb(EntityUid bodyUid, BodyComponent body)
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
        var comp = EnsureComp<LimbConditionComponent>(chosen);
        comp.Condition = LimbCondition.Dislocated;
        Dirty(chosen, comp);
        _movementSpeed.RefreshMovementSpeedModifiers(bodyUid);
        _audio.PlayPvs(_random.Pick(CrunchSounds), bodyUid, AudioParams.Default.WithVolume(0.4f));
    }

    /// <summary>Debug/test: set a random limb to broken so you can test debuffs/analyzer.</summary>
    public void BreakRandomLimb(EntityUid bodyUid, BodyComponent body)
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
        var comp = EnsureComp<LimbConditionComponent>(chosen);
        comp.Condition = LimbCondition.Broken;
        comp.FractureStep = FractureSurgeryStep.None;
        Dirty(chosen, comp);
        _movementSpeed.RefreshMovementSpeedModifiers(bodyUid);
        _audio.PlayPvs(_random.Pick(CrunchSounds), bodyUid, AudioParams.Default.WithVolume(0.4f));
    }

    private void StartReduceDislocation(EntityUid user, EntityUid target, EntityUid partUid)
    {
        if (!TryComp<BodyComponent>(target, out var body))
            return;
        if (!_body.GetBodyChildren(target, body).Any(p => p.Id == partUid))
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-no-limb"), target, user, PopupType.SmallCaution);
            return;
        }
        if (!TryComp<BodyPartComponent>(partUid, out var part))
            return;
        if (IsLimbCoveredByClothing(target, part))
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-limb-covered"), target, user, PopupType.SmallCaution);
            return;
        }
        if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition != LimbCondition.Dislocated)
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-no-limb"), target, user, PopupType.SmallCaution);
            return;
        }

        var ev = new DislocationRepairDoAfterEvent(GetNetEntity(partUid), isFracture: false);
        var doAfter = new DoAfterArgs(EntityManager, user, DislocationRepairTime, ev, target, target: target)
        {
            BreakOnMove = true,
            NeedHand = true,
            Broadcast = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
    }

    private void StartFractureStep(EntityUid user, EntityUid target, EntityUid partUid, FractureSurgeryAction action)
    {
        if (!TryComp<BodyComponent>(target, out var body) || !_body.GetBodyChildren(target, body).Any(p => p.Id == partUid))
            return;
        if (TryComp<BodyPartComponent>(partUid, out var part) && IsLimbCoveredByClothing(target, part))
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-limb-covered"), target, user, PopupType.SmallCaution);
            return;
        }
        if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition != LimbCondition.Broken)
            return;

        float delay = action switch
        {
            FractureSurgeryAction.MakeIncision => FractureIncisionTime,
            FractureSurgeryAction.SpreadWound => FractureRetractorTime,
            FractureSurgeryAction.ApplyBoneGel => FractureBoneGelTime,
            _ => 3f
        };

        EntityUid? used = null;
        if (TryComp<HandsComponent>(user, out var handsComp) && _hands.TryGetActiveItem((user, handsComp), out var held))
            used = held;

        var ev = new FractureSurgeryStepDoAfterEvent(GetNetEntity(partUid), action);
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, target, target: target, used: used)
        {
            BreakOnMove = true,
            NeedHand = true,
            Broadcast = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
    }

    private void OnFractureStepDoAfter(FractureSurgeryStepDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled || ev.Target == null)
            return;

        var target = ev.Target.Value;
        if (!TryComp<BodyComponent>(target, out var body))
            return;

        var partUid = GetEntity(ev.PartToFix);
        if (!Exists(partUid) || !_body.GetBodyChildren(target, body).Any(p => p.Id == partUid))
            return;
        if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition != LimbCondition.Broken)
            return;

        switch (ev.Action)
        {
            case FractureSurgeryAction.MakeIncision:
                limb.FractureStep = FractureSurgeryStep.IncisionOpen;
                Dirty(partUid, limb);
                _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-incision-done"), target, ev.User, PopupType.Small);
                break;
            case FractureSurgeryAction.SpreadWound:
                limb.FractureStep = FractureSurgeryStep.RetractorSpread;
                Dirty(partUid, limb);
                _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-retractor-done"), target, ev.User, PopupType.Small);
                break;
            case FractureSurgeryAction.ApplyBoneGel:
                if (limb.FractureStep != FractureSurgeryStep.RetractorSpread)
                    return;
                if (ev.Used != null && TryComp<StackComponent>(ev.Used, out var stack))
                    _stack.TryUse((ev.Used.Value, stack), 1);
                else if (ev.Used != null && !HasComp<StackComponent>(ev.Used))
                    QueueDel(ev.Used.Value);

                var success = _random.Prob(FractureBoneGelChance);
                var painAmount = HasComp<SleepingComponent>(target)
                    ? (success ? PainAsleepSuccess : PainAsleepFail)
                    : (success ? PainOnSuccess : PainOnFail);
                var painDamage = new DamageSpecifier();
                painDamage.DamageDict["Blunt"] = FixedPoint2.New(painAmount);
                _damageable.TryChangeDamage(target, painDamage, ignoreResistances: false, interruptsDoAfters: false, origin: ev.User);

                if (success)
                {
                    limb.Condition = LimbCondition.Ok;
                    limb.FractureStep = FractureSurgeryStep.None;
                    Dirty(partUid, limb);
                    _movementSpeed.RefreshMovementSpeedModifiers(target);
                    _audio.PlayPvs(_random.Pick(CrunchSounds), target, AudioParams.Default.WithVolume(0.5f));
                    _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-gel-success"), target, ev.User, PopupType.Medium);
                    _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-gel-success"), target, target, PopupType.Medium);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-gel-fail"), target, ev.User, PopupType.SmallCaution);
                    _popup.PopupEntity(Loc.GetString("tsf-surgery-fracture-gel-fail"), target, target, PopupType.SmallCaution);
                }
                break;
        }
    }

    private void OnDislocationDoAfter(DislocationRepairDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled || ev.Target == null)
            return;

        var target = ev.Target.Value;
        if (!TryComp<BodyComponent>(target, out var body) || !TryComp<DamageableComponent>(target, out var _))
            return;

        var partUid = GetEntity(ev.PartToFix);
        if (!Exists(partUid) || !_body.GetBodyChildren(target, body).Any(p => p.Id == partUid))
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-no-limb"), target, ev.User, PopupType.SmallCaution);
            return;
        }
        if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition != LimbCondition.Dislocated)
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-no-limb"), target, ev.User, PopupType.SmallCaution);
            return;
        }

        var success = _random.Prob(DislocationRepairChance);
        // Pain from the procedure: higher when awake, reduced when asleep (e.g. nitrous oxide).
        var painAmount = HasComp<SleepingComponent>(target)
            ? (success ? PainAsleepSuccess : PainAsleepFail)
            : (success ? PainOnSuccess : PainOnFail);
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(painAmount);

        _damageable.TryChangeDamage(target, damage, ignoreResistances: false, interruptsDoAfters: false, origin: ev.User);

        _audio.PlayPvs(_random.Pick(CrunchSounds), target, AudioParams.Default.WithVolume(0.5f));

        if (success)
        {
            limb.Condition = LimbCondition.Ok;
            Dirty(partUid, limb);
            _movementSpeed.RefreshMovementSpeedModifiers(target);
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-success"), target, ev.User, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-success"), target, target, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-fail"), target, ev.User, PopupType.SmallCaution);
            _popup.PopupEntity(Loc.GetString("tsf-surgery-reduce-dislocation-popup-fail"), target, target, PopupType.SmallCaution);
        }
    }

    private void OnSurgeryUIOpened(Entity<BodyComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendSurgeryState(ent.Owner);
    }

    private void OnSurgeryActionRequest(Entity<BodyComponent> ent, ref SurgeryActionRequestMessage msg)
    {
        var target = ent.Owner;
        var user = msg.Actor;
        var partUid = GetEntity(msg.Part);
        if (!Exists(partUid) || !TryComp<BodyComponent>(target, out var body) || !_body.GetBodyChildren(target, body).Any(p => p.Id == partUid))
            return;

        if (!TryCheckSurgeryTool(user, msg.Action, out var toolMessageLocId))
        {
            _popup.PopupEntity(Loc.GetString(toolMessageLocId), target, user, PopupType.SmallCaution);
            return;
        }

        switch (msg.Action)
        {
            case SurgeryRequestAction.ReduceDislocation:
                StartReduceDislocation(user, target, partUid);
                break;
            case SurgeryRequestAction.MakeIncision:
                StartFractureStep(user, target, partUid, FractureSurgeryAction.MakeIncision);
                break;
            case SurgeryRequestAction.SpreadWound:
                StartFractureStep(user, target, partUid, FractureSurgeryAction.SpreadWound);
                break;
            case SurgeryRequestAction.ApplyBoneGel:
                StartFractureStep(user, target, partUid, FractureSurgeryAction.ApplyBoneGel);
                break;
        }
        SendSurgeryState(target);
    }

    /// <returns>True if the user has the correct tool in hand (or no tool required); otherwise false and locale id for message.</returns>
    private bool TryCheckSurgeryTool(EntityUid user, SurgeryRequestAction action, out string messageLocId)
    {
        messageLocId = string.Empty;
        if (action == SurgeryRequestAction.ReduceDislocation)
            return true;

        if (!TryComp<HandsComponent>(user, out var hands) || !_hands.TryGetActiveItem((user, hands), out var held))
        {
            messageLocId = GetToolMessageForAction(action);
            return false;
        }

        var ok = action switch
        {
            SurgeryRequestAction.MakeIncision => HasComp<ScalpelComponent>(held),
            SurgeryRequestAction.SpreadWound => HasComp<RetractorComponent>(held),
            SurgeryRequestAction.ApplyBoneGel => HasComp<BoneGelComponent>(held),
            _ => true
        };
        if (!ok)
            messageLocId = GetToolMessageForAction(action);
        return ok;
    }

    private static string GetToolMessageForAction(SurgeryRequestAction action)
    {
        return action switch
        {
            SurgeryRequestAction.MakeIncision => "tsf-surgery-need-scalpel",
            SurgeryRequestAction.SpreadWound => "tsf-surgery-need-retractor",
            SurgeryRequestAction.ApplyBoneGel => "tsf-surgery-need-bone-gel",
            _ => string.Empty
        };
    }

    private SurgeryBuiState BuildSurgeryState(EntityUid target)
    {
        var limbs = new List<SurgeryLimbEntry>();
        if (!TryComp<BodyComponent>(target, out var body))
            return new SurgeryBuiState(GetNetEntity(target), limbs);

        foreach (var (partUid, part) in _body.GetBodyChildren(target, body))
        {
            var condition = LimbCondition.Ok;
            var fractureStep = FractureSurgeryStep.None;
            if (TryComp<LimbConditionComponent>(partUid, out var limb))
            {
                condition = limb.Condition;
                fractureStep = limb.FractureStep;
            }
            var covered = IsLimbCoveredByClothing(target, part);
            var partName = GetPartDisplayName(part);
            limbs.Add(new SurgeryLimbEntry(
                GetNetEntity(partUid),
                partName,
                condition,
                fractureStep,
                covered,
                canReduceDislocation: condition == LimbCondition.Dislocated && !covered,
                canDoIncision: condition == LimbCondition.Broken && fractureStep == FractureSurgeryStep.None && !covered,
                canDoRetractor: condition == LimbCondition.Broken && fractureStep == FractureSurgeryStep.IncisionOpen && !covered,
                canDoGel: condition == LimbCondition.Broken && fractureStep == FractureSurgeryStep.RetractorSpread && !covered));
        }
        return new SurgeryBuiState(GetNetEntity(target), limbs);
    }

    private void SendSurgeryState(EntityUid target)
    {
        if (!_ui.HasUi(target, SurgeryUiKey.Key))
            return;
        _ui.SetUiState(target, SurgeryUiKey.Key, BuildSurgeryState(target));
    }
}
