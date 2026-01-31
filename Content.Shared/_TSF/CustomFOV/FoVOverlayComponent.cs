namespace Content.Shared._TSF.CustomFOV;

/// <summary>
/// Marks an entity (e.g. airlock) to receive perspective wall FOV corner overlay.
/// Used with OccluderComponent; only anchored grid entities are considered.
/// </summary>
[RegisterComponent]
public sealed partial class FoVOverlayComponent : Component
{
}
