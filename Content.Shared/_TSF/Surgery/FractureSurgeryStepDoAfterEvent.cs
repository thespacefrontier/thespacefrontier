// TSF
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

[Serializable, NetSerializable]
public sealed partial class FractureSurgeryStepDoAfterEvent : DoAfterEvent
{
    public NetEntity PartToFix;
    public FractureSurgeryAction Action;

    public FractureSurgeryStepDoAfterEvent() { }

    public FractureSurgeryStepDoAfterEvent(NetEntity partToFix, FractureSurgeryAction action)
    {
        PartToFix = partToFix;
        Action = action;
    }

    public override DoAfterEvent Clone() => new FractureSurgeryStepDoAfterEvent(PartToFix, Action);
}
