// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._TSF.Consciousness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    [AutoNetworkedField]
    public float Level = 1f;

    [AutoNetworkedField]
    public bool Unconscious;
}
