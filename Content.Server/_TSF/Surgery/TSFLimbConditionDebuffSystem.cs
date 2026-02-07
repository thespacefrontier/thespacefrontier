// TSF
using Content.Server.Body.Systems;
using Content.Shared._TSF.Surgery;
using Content.Shared._TSF.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server._TSF.Surgery;

/// <summary>
/// Applies debuffs when limbs are dislocated or broken: slower movement for leg/foot, blocked use for arm/hand.
/// </summary>
public sealed class TSFLimbConditionDebuffSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>Speed multiplier when a leg or foot is dislocated/broken (stacked if both legs).</summary>
    private const float LegDebuffPerLimb = 0.65f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<HandsComponent, ContainerIsInsertingAttemptEvent>(OnHandInsertAttempt);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, BodyComponent body, ref RefreshMovementSpeedModifiersEvent args)
    {
        int badLegs = 0;
        foreach (var (partUid, part) in _body.GetBodyChildren(uid, body))
        {
            if (part.PartType != BodyPartType.Leg && part.PartType != BodyPartType.Foot)
                continue;
            if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition == LimbCondition.Ok)
                continue;
            badLegs++;
        }
        if (badLegs == 0)
            return;
        float mod = 1f;
        for (int i = 0; i < badLegs; i++)
            mod *= LegDebuffPerLimb;
        args.ModifySpeed(mod);
    }

    private void OnHandInsertAttempt(EntityUid uid, HandsComponent hands, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        if (!hands.Hands.TryGetValue(args.Container.ID, out var hand))
            return;
        if (!TryComp<BodyComponent>(uid, out var body))
            return;

        var handSide = hand.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None
        };
        if (handSide == BodyPartSymmetry.None)
            return;

        foreach (var (partUid, part) in _body.GetBodyChildren(uid, body))
        {
            if (part.PartType != BodyPartType.Arm && part.PartType != BodyPartType.Hand)
                continue;
            if (part.Symmetry != handSide)
                continue;
            if (!TryComp<LimbConditionComponent>(partUid, out var limb) || limb.Condition == LimbCondition.Ok)
                continue;
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("tsf-surgery-hand-injured-cannot-use"), uid, uid, PopupType.SmallCaution);
            return;
        }
    }
}
