// TSF
using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Surgery.Components;

/// <summary>
/// Attached to a body part entity. Tracks if the limb is dislocated or broken for TSF surgery/analyzer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedLimbConditionSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class LimbConditionComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbCondition Condition = LimbCondition.Ok;

    /// <summary>For broken limbs: incision → retractor → then bone gel can repair.</summary>
    [DataField, AutoNetworkedField]
    public FractureSurgeryStep FractureStep = FractureSurgeryStep.None;
}
