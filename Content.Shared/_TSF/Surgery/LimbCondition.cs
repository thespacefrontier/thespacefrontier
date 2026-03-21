using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

[Serializable, NetSerializable]
public enum LimbCondition
{
    Ok = 0,
    Dislocated,
    Broken
}

[Serializable, NetSerializable]
public enum FractureSurgeryStep
{
    None = 0,
    IncisionOpen,
    RetractorSpread
}

[Serializable, NetSerializable]
public enum FractureSurgeryAction
{
    MakeIncision = 0,
    SpreadWound,
    ApplyBoneGel
}
