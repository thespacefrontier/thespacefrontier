using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Consciousness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class TraumaticShockComponent : Component
{

    [AutoNetworkedField]
    public float Severity;
}
