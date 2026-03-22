using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared._TSF.Medical;

[Serializable, NetSerializable]
public enum MedicalRecordsConsoleKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MedicalRecordsConsoleState : BoundUserInterfaceState
{
    public readonly uint? SelectedKey;
    public readonly GeneralStationRecord? Record;
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly StationRecordsFilter? Filter;

    public readonly List<string> LogLines;

    public readonly bool BodyNotLocated;

    public MedicalRecordsConsoleState(
        uint? selectedKey,
        GeneralStationRecord? record,
        Dictionary<uint, string>? recordListing,
        StationRecordsFilter? filter,
        List<string> logLines,
        bool bodyNotLocated)
    {
        SelectedKey = selectedKey;
        Record = record;
        RecordListing = recordListing;
        Filter = filter;
        LogLines = logLines;
        BodyNotLocated = bodyNotLocated;
    }

    public MedicalRecordsConsoleState() : this(null, null, null, null, new List<string>(), false)
    {
    }

    public bool IsEmpty() => SelectedKey == null && Record == null && RecordListing == null;
}
