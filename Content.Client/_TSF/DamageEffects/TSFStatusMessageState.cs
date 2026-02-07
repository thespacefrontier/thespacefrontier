namespace Content.Client._TSF.DamageEffects;

/// <summary>
/// Shared state for status messages (pain/fear/crit thoughts). Written by TSFDamageEffectsSystem, read by TSFStatusMessageUIController.
/// </summary>
public static class TSFStatusMessageState
{
    public static string? Message { get; set; }
    public static double DisplayUntil { get; set; }
}
