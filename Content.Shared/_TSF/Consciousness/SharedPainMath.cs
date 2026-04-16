// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
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
        Entity<DamageableComponent?> ent,
        DamageableSystem damageableSystem,
        IPrototypeManager proto,
        TSFPainWeightPrototype weights,
        float globalMultiplier)
    {
        if (globalMultiplier <= 0f)
            return 0f;

        var damageable = ent.Comp;
        if (damageable == null)
            return 0f;

        var allowed = GetPainDamageTypeIds(damageable, proto);
        float sum = 0f;

        var damageDict = damageableSystem.GetAllDamage(ent).DamageDict;
        foreach (var (typeId, amount) in damageDict)
        {
            if (!allowed.Contains(typeId.Id.ToLowerInvariant()))
                continue;

            var w = weights.GetWeight(typeId.Id);
            sum += amount.Float() * w;
        }

        return sum * globalMultiplier;
    }

    public static float GetAsphyxiationDamage(
        Entity<DamageableComponent?> ent,
        DamageableSystem damageableSystem)
    {
        var damageDict = damageableSystem.GetAllDamage(ent).DamageDict;
        var key = new ProtoId<DamageTypePrototype>("Asphyxiation");
        return damageDict.TryGetValue(key, out var v) ? v.Float() : 0f;
    }

    public static float ComputeBloodlossStyleContribution(
        Entity<DamageableComponent?> ent,
        DamageableSystem damageableSystem,
        FixedPoint2 critThreshold)
    {
        if (critThreshold <= FixedPoint2.Zero)
            return 0f;

        var deathLevel = damageableSystem.GetTotalDamage(ent);
        return (deathLevel / critThreshold).Float();
    }
}
