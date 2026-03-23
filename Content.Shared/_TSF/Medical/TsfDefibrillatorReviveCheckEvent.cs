// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._TSF.Medical;

[ByRefEvent]
public struct TsfDefibrillatorReviveCheckEvent
{
    public EntityUid Target;
    public EntityUid User;
    public bool AllowRevive;
}
