using Robust.Shared.GameObjects;

namespace Content.Shared.Medical;

public sealed class HealingDoAfterCompletedEvent : EntityEventArgs
{
    public EntityUid User;
    public EntityUid? Used;
}
