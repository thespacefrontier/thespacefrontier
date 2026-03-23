// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._TSF.Health;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class WoundInfectionTrackerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float HeavyBleedSeconds;

    [DataField, AutoNetworkedField]
    public bool Infected;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextInfectionDamage;

    [DataField, AutoNetworkedField]
    public byte SepsisStage;

    [DataField, AutoNetworkedField]
    public float SepsisAccumSeconds;
}
