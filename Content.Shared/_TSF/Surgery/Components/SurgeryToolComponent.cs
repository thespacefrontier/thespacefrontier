// TSF
using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Surgery.Components;

/// <summary>
/// Marker component for items that can be used to perform TSF surgery (e.g. reduce dislocation).
/// When held in hand, the user gets the surgery verb on valid patients.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryToolComponent : Component
{
}
