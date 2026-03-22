namespace Content.Shared._TSF.Medical;

[ByRefEvent]
public struct TsfDefibrillatorReviveCheckEvent
{
    public EntityUid Target;
    public EntityUid User;
    public bool AllowRevive;
}
