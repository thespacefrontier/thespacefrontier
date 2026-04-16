// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF.Health;
using Content.Shared.Damage.Components;
using Content.Shared.Medical;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._TSF.Medical;

public sealed class TSFTourniquetMedicalLogSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> TourniquetTag = new("Tourniquet");

    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterCompletedEvent>(OnHealingCompleted);
    }

    private void OnHealingCompleted(Entity<DamageableComponent> target, ref HealingDoAfterCompletedEvent args)
    {
        if (args.Used is not { } used || !Exists(used))
            return;

        if (!_tag.HasTag(used, TourniquetTag))
            return;

        var log = EnsureComp<MedicalRecordLogComponent>(target);
        log.Entries.Add(Loc.GetString("tsf-medical-record-tourniquet", ("time", (int) _timing.CurTime.TotalSeconds)));
        while (log.Entries.Count > MedicalRecordLogComponent.MaxEntries)
            log.Entries.RemoveAt(0);
    }
}
