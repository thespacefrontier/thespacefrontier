// TSF
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

[Serializable, NetSerializable]
public sealed partial class DislocationRepairDoAfterEvent : DoAfterEvent
{
    public NetEntity PartToFix;
    public bool IsFracture;

    public DislocationRepairDoAfterEvent() { }

    public DislocationRepairDoAfterEvent(NetEntity partToFix, bool isFracture = false)
    {
        PartToFix = partToFix;
        IsFracture = isFracture;
    }

    public override DoAfterEvent Clone() => new DislocationRepairDoAfterEvent(PartToFix, IsFracture);
}
