// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._TSF.Consciousness;

public static class SharedPainMath
{

    public static HashSet<string> GetPainDamageTypeIds(
        DamageableComponent damageable,
        IPrototypeManager proto)
    {
        var set = new HashSet<string>();
        foreach (var groupId in damageable.PainDamageGroups)
        {
            if (!proto.TryIndex(groupId, out DamageGroupPrototype? group))
                continue;

            foreach (var typeId in group.DamageTypes)
                set.Add(typeId.Id.ToLowerInvariant());
        }

        return set;
    }

    public static float ComputePainLevel(
        DamageableComponent damageable,
        IPrototypeManager proto,
        TSFPainWeightPrototype weights,
        float globalMultiplier)
    {
        if (globalMultiplier <= 0f)
            return 0f;

        var allowed = GetPainDamageTypeIds(damageable, proto);
        float sum = 0f;

        foreach (var (typeId, amount) in damageable.Damage.DamageDict)
        {
            if (!allowed.Contains(typeId.ToLowerInvariant()))
                continue;

            var w = weights.GetWeight(typeId);
            sum += amount.Float() * w;
        }

        return sum * globalMultiplier;
    }

    public static float GetAsphyxiationDamage(DamageableComponent damageable)
    {
        return damageable.Damage.DamageDict.TryGetValue("Asphyxiation", out var v) ? v.Float() : 0f;
    }

    public static float ComputeBloodlossStyleContribution(
        DamageableComponent damageable,
        FixedPoint2 critThreshold)
    {
        if (critThreshold <= FixedPoint2.Zero)
            return 0f;

        var deathLevel = FixedPoint2.Zero;
        if (damageable.DamagePerGroup.TryGetValue("Airloss", out var airloss))
            deathLevel += airloss;
        if (damageable.DamagePerGroup.TryGetValue("Toxin", out var toxin))
            deathLevel += toxin;

        return (deathLevel / critThreshold).Float();
    }
}
