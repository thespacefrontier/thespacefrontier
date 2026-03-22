using Content.Shared.StationRecords;

namespace Content.Server._TSF.Medical;

[RegisterComponent, Access(typeof(MedicalRecordsConsoleSystem))]
public sealed partial class MedicalRecordsConsoleComponent : Component
{
    [DataField]
    public uint? ActiveKey;

    [DataField]
    public StationRecordsFilter? Filter;
}
