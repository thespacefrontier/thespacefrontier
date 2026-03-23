// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._TSF.Consciousness;

[Prototype("tsfPainWeights")]
public sealed partial class TSFPainWeightPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float DefaultWeight { get; private set; } = 1f;

    [DataField]
    public Dictionary<string, float> Weights { get; private set; } = new();

    public float GetWeight(string damageTypeId)
    {
        if (Weights.TryGetValue(damageTypeId, out var w))
            return w;

        foreach (var (key, weight) in Weights)
        {
            if (string.Equals(key, damageTypeId, StringComparison.OrdinalIgnoreCase))
                return weight;
        }

        return DefaultWeight;
    }
}
