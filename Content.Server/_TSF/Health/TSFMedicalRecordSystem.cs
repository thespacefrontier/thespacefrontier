// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF.Health;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server._TSF.Health;

public sealed class TSFMedicalRecordSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, TargetDefibrillatedEvent>(OnDefibrillated);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnDefibrillated(EntityUid uid, MobStateComponent _, ref TargetDefibrillatedEvent ev)
    {
        var log = EnsureComp<MedicalRecordLogComponent>(uid);
        var line = Loc.GetString("tsf-medical-record-defib",
            ("time", (int) _timing.CurTime.TotalSeconds));
        AppendLine(log, line);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        var uid = args.Target;
        if (args.NewMobState == args.OldMobState)
            return;

        var log = EnsureComp<MedicalRecordLogComponent>(uid);
        var t = (int) _timing.CurTime.TotalSeconds;

        if (args.NewMobState == MobState.Critical && args.OldMobState != MobState.Critical)
        {
            AppendLine(log, Loc.GetString("tsf-medical-record-crit", ("time", t)));
            return;
        }

        if (args.OldMobState == MobState.Critical && args.NewMobState == MobState.Alive)
        {
            AppendLine(log, Loc.GetString("tsf-medical-record-stabilized-from-crit", ("time", t)));
        }
    }

    private static void AppendLine(MedicalRecordLogComponent log, string line)
    {
        log.Entries.Add(line);
        while (log.Entries.Count > MedicalRecordLogComponent.MaxEntries)
            log.Entries.RemoveAt(0);
    }
}
