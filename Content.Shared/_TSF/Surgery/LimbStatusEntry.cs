using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

[Serializable, NetSerializable]
public readonly struct LimbStatusEntry
{
    public readonly string PartName;
    public readonly LimbCondition Condition;

    public LimbStatusEntry(string partName, LimbCondition condition)
    {
        PartName = partName;
        Condition = condition;
    }
}
