using Content.Shared.FixedPoint;

namespace Content.Shared._TSF.Consciousness;

public static class SharedTsfConsciousnessFormula
{
    public static float ComputePainRatio(float painLevel, FixedPoint2 critThreshold)
    {
        if (critThreshold <= FixedPoint2.Zero)
            return 0f;

        var ratio = (FixedPoint2.New(painLevel) / critThreshold).Float();
        return Math.Clamp(ratio, 0f, 1.5f);
    }

    public static float ComputeHypovolemiaRatio(float bloodLevel, float bloodStart)
    {
        if (bloodStart <= 0f || bloodLevel >= bloodStart)
            return 0f;

        return Math.Clamp((bloodStart - bloodLevel) / bloodStart, 0f, 1f);
    }

    public static float ComputeAsphyxiationRatio(float asphyxiationDamage, FixedPoint2 critThreshold)
    {
        if (critThreshold <= FixedPoint2.Zero)
            return 0f;

        var ratio = asphyxiationDamage / critThreshold.Float();
        return Math.Clamp(ratio, 0f, 1.5f);
    }

    public static float ComputeLevel(
        float painRatio,
        float bloodDamageRatio,
        float hypovolemiaRatio,
        float traumaticShockSeverity,
        float asphyxiationRatio,
        float painBlend,
        float bloodDamageBlend,
        float hypovolemiaBlend,
        float shockBlend,
        float asphyxiationBlend,
        float painToConsciousnessFactor)
    {
        var combined = painRatio * painBlend
            + bloodDamageRatio * bloodDamageBlend
            + hypovolemiaRatio * hypovolemiaBlend
            + Math.Clamp(traumaticShockSeverity, 0f, 1f) * shockBlend
            + asphyxiationRatio * asphyxiationBlend;

        return Math.Clamp(1f - combined * painToConsciousnessFactor, 0f, 1f);
    }
}
