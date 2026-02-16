using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Organs;

/// <summary>
/// Organ damage tracking. Each organ has a float 0.0 (healthy) to 1.0 (destroyed).
/// Death occurs ONLY when Brain >= 0.7. Other organs cause indirect damage chains
/// (e.g. heart failure → internal bleeding → blood loss → brain hypoxia → brain death).
/// Placed on mob entities, not on individual organ entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TSFOrganDamageComponent : Component
{
    // ── Organ integrity (0 = healthy, 1 = destroyed) ──

    [DataField, AutoNetworkedField]
    public float Brain;

    [DataField, AutoNetworkedField]
    public float Heart;

    [DataField, AutoNetworkedField]
    public float LungLeft;

    [DataField, AutoNetworkedField]
    public float LungRight;

    [DataField, AutoNetworkedField]
    public float Liver;

    [DataField, AutoNetworkedField]
    public float Stomach;

    [DataField, AutoNetworkedField]
    public float Intestines;

    [DataField, AutoNetworkedField]
    public float Trachea;

    [DataField, AutoNetworkedField]
    public float Eyes;

    // ── Death / critical thresholds ──

    /// <summary>Brain damage at which death occurs.</summary>
    [DataField]
    public float BrainDeathThreshold = 0.7f;

    /// <summary>Heart damage at which heart is considered stopped.</summary>
    [DataField]
    public float HeartStopThreshold = 0.9f;

    /// <summary>Heart damage at which internal bleeding starts.</summary>
    [DataField]
    public float HeartBleedThreshold = 0.3f;

    /// <summary>Heart damage at which severe effects begin.</summary>
    [DataField]
    public float HeartCriticalThreshold = 0.6f;

    /// <summary>Lung damage at which breathing is impaired.</summary>
    [DataField]
    public float LungImpairedThreshold = 0.5f;

    /// <summary>Lung damage at which lung is critically failed.</summary>
    [DataField]
    public float LungCriticalThreshold = 0.8f;

    /// <summary>Liver damage at which internal bleeding starts.</summary>
    [DataField]
    public float LiverBleedThreshold = 0.3f;

    /// <summary>Liver damage at which severe pain/stun occurs.</summary>
    [DataField]
    public float LiverCriticalThreshold = 0.7f;

    /// <summary>Brain damage at which consciousness drops / random blackouts.</summary>
    [DataField]
    public float BrainDisorientThreshold = 0.3f;

    // ── Indirect brain damage rates (per second) ──

    /// <summary>Brain damage per second when heart is stopped.</summary>
    [DataField]
    public float BrainDamageFromHeartStop = 0.035f;

    /// <summary>Brain damage per second when both lungs are critically damaged.</summary>
    [DataField]
    public float BrainDamageFromLungFailure = 0.025f;

    /// <summary>Brain damage per second when blood level is critically low (&lt; 20%).</summary>
    [DataField]
    public float BrainDamageFromBloodLoss = 0.02f;

    /// <summary>Blood level fraction below which brain starts taking hypoxia damage.</summary>
    [DataField]
    public float BloodCriticalFraction = 0.2f;

    // ── Pain contributed by organ damage ──

    /// <summary>Pain added per 0.1 liver damage above LiverBleedThreshold.</summary>
    [DataField]
    public float LiverPainPerTenth = 8f;

    // ── Flags ──

    /// <summary>Set to true when head is amputated (instant brain death).</summary>
    [AutoNetworkedField]
    public bool HeadAmputated;

    /// <summary>Whether the heart is currently stopped (cached for performance).</summary>
    [AutoNetworkedField]
    public bool HeartStopped;
}
