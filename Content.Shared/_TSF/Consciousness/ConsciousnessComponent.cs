// TSF: Consciousness from pain/blood loss. When below threshold, entity is unconscious (can't move, speak, dark screen).
// Morphine (StatusEffectPainNumbness) reduces pain contribution.

using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Consciousness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    /// <summary>0 = unconscious, 1 = fully conscious. Derived from pain and other factors.</summary>
    [AutoNetworkedField]
    public float Level = 1f;

    /// <summary>When true, entity cannot move, speak; client shows dark screen (like crit).</summary>
    [AutoNetworkedField]
    public bool Unconscious;
}
