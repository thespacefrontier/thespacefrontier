// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._TSF.DamageEffects;

public static class TSFStatusMessageState
{
    public static string? Message { get; set; }

    public static double DisplayUntil { get; set; }

    /// <summary> Wall-clock delay between revealing each Unicode extended grapheme (rune). </summary>
    public const float RevealSecondsPerRune = 0.042f;
}
