using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SurgeryLimbEntry
{
    public NetEntity PartNetEntity;
    public string PartName = string.Empty;
    public LimbCondition Condition;
    public FractureSurgeryStep FractureStep;
    public bool IsCovered;
    public bool CanReduceDislocation;
    public bool CanDoIncision;
    public bool CanDoRetractor;
    public bool CanDoGel;

    public SurgeryLimbEntry() { }

    public SurgeryLimbEntry(
        NetEntity partNetEntity,
        string partName,
        LimbCondition condition,
        FractureSurgeryStep fractureStep,
        bool isCovered,
        bool canReduceDislocation,
        bool canDoIncision,
        bool canDoRetractor,
        bool canDoGel)
    {
        PartNetEntity = partNetEntity;
        PartName = partName;
        Condition = condition;
        FractureStep = fractureStep;
        IsCovered = isCovered;
        CanReduceDislocation = canReduceDislocation;
        CanDoIncision = canDoIncision;
        CanDoRetractor = canDoRetractor;
        CanDoGel = canDoGel;
    }
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState : BoundUserInterfaceState
{
    public NetEntity Target;
    public List<SurgeryLimbEntry> Limbs;

    public SurgeryBuiState(NetEntity target, List<SurgeryLimbEntry> limbs)
    {
        Target = target;
        Limbs = limbs;
    }
}

[Serializable, NetSerializable]
public sealed class SurgeryActionRequestMessage : BoundUserInterfaceMessage
{
    public NetEntity Part;
    public SurgeryRequestAction Action;

    public SurgeryActionRequestMessage(NetEntity part, SurgeryRequestAction action)
    {
        Part = part;
        Action = action;
    }
}

[Serializable, NetSerializable]
public enum SurgeryRequestAction : byte
{
    ReduceDislocation = 0,
    MakeIncision,
    SpreadWound,
    ApplyBoneGel
}
