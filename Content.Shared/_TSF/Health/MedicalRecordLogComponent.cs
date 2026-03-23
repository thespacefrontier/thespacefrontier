// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._TSF.Health;

[RegisterComponent]
public sealed partial class MedicalRecordLogComponent : Component
{
    public const int MaxEntries = 24;

    public readonly List<string> Entries = new();
}
