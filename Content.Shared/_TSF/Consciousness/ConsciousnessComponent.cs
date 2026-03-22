using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Consciousness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    [AutoNetworkedField]
    public float Level = 1f;

    [AutoNetworkedField]
    public bool Unconscious;
}
