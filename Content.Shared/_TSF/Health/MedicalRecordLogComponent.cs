namespace Content.Shared._TSF.Health;

[RegisterComponent]
public sealed partial class MedicalRecordLogComponent : Component
{
    public const int MaxEntries = 24;

    public readonly List<string> Entries = new();
}
