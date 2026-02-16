using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Pain;

/// <summary>
/// Z-City style pain and shock tracking. Pain 0-150, Shock 0-100.
/// Pain is accumulated from damage to body parts; shock is a separate value
/// that grows from pain and blood loss. Both affect consciousness.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TSFPainComponent : Component
{
    /// <summary>Current pain level. Z-City range: 0-150.</summary>
    [DataField, AutoNetworkedField]
    public float Pain;

    /// <summary>Current shock level. 0-100.</summary>
    [DataField, AutoNetworkedField]
    public float Shock;

    /// <summary>Maximum pain value.</summary>
    [DataField]
    public float MaxPain = 150f;

    /// <summary>Maximum shock value.</summary>
    [DataField]
    public float MaxShock = 100f;

    /// <summary>Pain threshold for disorientation effects.</summary>
    [DataField]
    public float DisorientThreshold = 60f;

    /// <summary>Pain threshold at which shock starts increasing.</summary>
    [DataField]
    public float ShockGrowthThreshold = 80f;

    /// <summary>Pain threshold for ragdoll/stun effects.</summary>
    [DataField]
    public float RagdollThreshold = 100f;

    /// <summary>Shock threshold for unconsciousness.</summary>
    [DataField]
    public float ShockUnconsciousThreshold = 10f;

    /// <summary>Natural pain decay per second.</summary>
    [DataField]
    public float PainDecayRate = 0.8f;

    /// <summary>Natural shock decay per second (only when pain is low).</summary>
    [DataField]
    public float ShockDecayRate = 0.3f;

    /// <summary>Shock growth rate per second when pain exceeds ShockGrowthThreshold.</summary>
    [DataField]
    public float ShockGrowthRate = 1.5f;

    /// <summary>Adrenaline effect: fraction of pain reduction (0.25 = 25% reduction).</summary>
    [DataField, AutoNetworkedField]
    public float AdrenalineFactor;

    /// <summary>
    /// Painkiller strength: 0 = none, 0.4 = ibuprofen (mild), 0.9 = morphine (strong).
    /// Reduces effective pain by this fraction and speeds up pain decay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PainkillerStrength;
}
