using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Surgery.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedLimbConditionSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class LimbConditionComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbCondition Condition = LimbCondition.Ok;

    [DataField, AutoNetworkedField]
    public FractureSurgeryStep FractureStep = FractureSurgeryStep.None;
}
