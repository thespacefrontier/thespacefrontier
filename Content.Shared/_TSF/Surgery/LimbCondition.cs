// TSF
using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Surgery;

/// <summary>
/// TSF limb state: used for status messages, health analyzer, and repair (e.g. reduce dislocation by hand).
/// </summary>
[Serializable, NetSerializable]
public enum LimbCondition
{
    Ok = 0,
    Dislocated,
    Broken
}

/// <summary>
/// Progress of realistic fracture surgery: incision → retractor → bone gel.
/// </summary>
[Serializable, NetSerializable]
public enum FractureSurgeryStep
{
    None = 0,
    IncisionOpen,
    RetractorSpread
}

/// <summary>
/// Which step of fracture surgery the DoAfter is performing.
/// </summary>
[Serializable, NetSerializable]
public enum FractureSurgeryAction
{
    MakeIncision = 0,
    SpreadWound,
    ApplyBoneGel
}
