// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._TSF;

[CVarDefs]
public sealed class TSFCVars
{
    public static readonly CVarDef<bool> TsfAutonomousLobbyVote =
        CVarDef.Create("tsf.autonomous_lobby_vote", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> TsfLockPlayerVoteMenu =
        CVarDef.Create("tsf.lock_player_vote_menu", true, CVar.SERVERONLY);

    public static readonly CVarDef<string> TsfPainWeightSet =
        CVarDef.Create("tsf.pain.weight_set", "TSFPainWeights", CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfPainGlobalMultiplier =
        CVarDef.Create("tsf.pain.global_multiplier", 1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfPainUnconsciousThreshold =
        CVarDef.Create("tsf.pain.unconscious_threshold", 0.22f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfPainToConsciousnessFactor =
        CVarDef.Create("tsf.pain.to_consciousness_factor", 0.85f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessPainRatioBlend =
        CVarDef.Create("tsf.consciousness.pain_ratio_blend", 0.85f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessBloodDamageRatioBlend =
        CVarDef.Create("tsf.consciousness.blood_damage_ratio_blend", 0.25f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessHypovolemiaBlend =
        CVarDef.Create("tsf.consciousness.hypovolemia_blend", 0.35f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessHypovolemiaBloodStart =
        CVarDef.Create("tsf.consciousness.hypovolemia_blood_start", 0.85f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessTraumaticShockBlend =
        CVarDef.Create("tsf.consciousness.traumatic_shock_blend", 0.15f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfConsciousnessAsphyxiationBlend =
        CVarDef.Create("tsf.consciousness.asphyxiation_blend", 0.2f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> TsfTraumaticShockAddPerDamage =
        CVarDef.Create("tsf.traumatic_shock.add_per_damage", 0.018f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfTraumaticShockDecayPerSecond =
        CVarDef.Create("tsf.traumatic_shock.decay_per_second", 0.35f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfHypovolemicShockBloodThreshold =
        CVarDef.Create("tsf.hypovolemic_shock.blood_threshold", 0.45f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfHypovolemicShockRecoveryBloodThreshold =
        CVarDef.Create("tsf.hypovolemic_shock.recovery_blood_threshold", 0.55f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfDefibrillatorMinBlood =
        CVarDef.Create("tsf.defibrillator.min_blood_for_revive", 0.32f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfDefibrillatorMinBloodStrict =
        CVarDef.Create("tsf.defibrillator.min_blood_strict", 0.22f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfDefibrillatorAsphyxBlockRatio =
        CVarDef.Create("tsf.defibrillator.asphyx_block_ratio", 0.55f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfVitalsDistressBloodHigh =
        CVarDef.Create("tsf.vitals.distress_blood_high", 0.82f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfVitalsDistressBloodLow =
        CVarDef.Create("tsf.vitals.distress_blood_low", 0.48f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfArterialBleedProcChance =
        CVarDef.Create("tsf.bleed.arterial_proc_chance", 0.07f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfArterialBleedBonus =
        CVarDef.Create("tsf.bleed.arterial_bonus_amount", 1.6f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfPneumothoraxChance =
        CVarDef.Create("tsf.pneumo.chest_pierce_chance", 0.04f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TsfPneumothoraxMinPiercing =
        CVarDef.Create("tsf.pneumo.min_piercing_damage", 6f, CVar.SERVERONLY);
}
