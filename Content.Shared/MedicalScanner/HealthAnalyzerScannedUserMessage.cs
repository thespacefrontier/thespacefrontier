// TSF edit start
using Content.Shared._TSF.Surgery;
// TSF edit end
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    // TSF edit start
    /// <summary>TSF: which body parts are broken/dislocated (from LimbConditionComponent).</summary>
    public List<LimbStatusEntry>? LimbStatus;

    /// <summary>TSF: organ damage status.</summary>
    public OrganStatusData? OrganStatus;
    // TSF edit end

    public HealthAnalyzerUiState() {}

    // TSF edit
    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, List<LimbStatusEntry>? limbStatus = null, OrganStatusData? organStatus = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        // TSF edit
        LimbStatus = limbStatus;
        OrganStatus = organStatus;
    }
}

// TSF edit start
/// <summary>
/// Serializable organ damage data for health analyzer UI.
/// </summary>
[Serializable, NetSerializable]
public struct OrganStatusData
{
    public float Brain;
    public float Heart;
    public float LungLeft;
    public float LungRight;
    public float Liver;
    public float Stomach;
    public float Intestines;
    public float Trachea;
    public float Eyes;
    public bool HeartStopped;
    public float Pain;
    public float Shock;
}
// TSF edit end
