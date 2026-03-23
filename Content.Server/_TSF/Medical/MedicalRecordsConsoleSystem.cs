// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.StationRecords.Systems;
using Content.Server.Station.Systems;
using Content.Shared._TSF.Health;
using Content.Shared._TSF.Medical;
using Content.Shared.Forensics.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._TSF.Medical;

public sealed class MedicalRecordsConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private EntityQuery<MobStateComponent> _mobQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        _mobQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<MedicalRecordsConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<MedicalRecordsConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<MedicalRecordsConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);

        Subs.BuiEvents<MedicalRecordsConsoleComponent>(MedicalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
        });
    }

    private void UpdateUserInterface<T>(Entity<MedicalRecordsConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    private void OnKeySelected(Entity<MedicalRecordsConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }

    private void OnFiltersChanged(Entity<MedicalRecordsConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    private void UpdateUserInterface(Entity<MedicalRecordsConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, MedicalRecordsConsoleKey.Key, new MedicalRecordsConsoleState());
            return;
        }

        var listing = _stationRecords.BuildListing((owningStation.Value, stationRecords), console.Filter);

        switch (listing.Count)
        {
            case 0:
                _ui.SetUiState(uid, MedicalRecordsConsoleKey.Key, new MedicalRecordsConsoleState());
                return;
            default:
                if (console.ActiveKey == null)
                    console.ActiveKey = listing.Keys.First();
                break;
        }

        if (console.ActiveKey is not { } id)
            return;

        var key = new StationRecordKey(id, owningStation.Value);
        _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords);

        var logLines = new List<string>();
        var bodyNotLocated = false;

        if (record != null)
        {
            if (TryResolveBodyForRecord(key, record) is { } body)
            {
                if (TryComp<MedicalRecordLogComponent>(body, out var log))
                    logLines.AddRange(log.Entries);
            }
            else
            {
                bodyNotLocated = true;
            }
        }

        var state = new MedicalRecordsConsoleState(id, record, listing, console.Filter, logLines, bodyNotLocated);
        _ui.SetUiState(uid, MedicalRecordsConsoleKey.Key, state);
    }

    private EntityUid? TryResolveBodyForRecord(StationRecordKey key, GeneralStationRecord record)
    {
        if (!string.IsNullOrEmpty(record.DNA))
        {
            if (TryResolveByDna(record.DNA) is { } byDna)
                return byDna;
        }

        if (!string.IsNullOrEmpty(record.Fingerprint))
        {
            if (TryResolveByFingerprint(record.Fingerprint) is { } byFp)
                return byFp;
        }

        return TryResolveByKeyStorageWalk(key);
    }

    private EntityUid? TryResolveByDna(string dna)
    {
        EntityUid? anyMob = null;
        EntityUid? any = null;

        var query = EntityQueryEnumerator<DnaComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.DNA != dna)
                continue;

            any = uid;
            if (!_mobQuery.HasComp(uid))
                continue;

            anyMob = uid;
            if (_mobState.IsAlive(uid))
                return uid;
        }

        return anyMob ?? any;
    }

    private EntityUid? TryResolveByFingerprint(string fingerprint)
    {
        EntityUid? anyMob = null;
        EntityUid? any = null;

        var query = EntityQueryEnumerator<FingerprintComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Fingerprint != fingerprint)
                continue;

            any = uid;
            if (!_mobQuery.HasComp(uid))
                continue;

            anyMob = uid;
            if (_mobState.IsAlive(uid))
                return uid;
        }

        return anyMob ?? any;
    }

    private EntityUid? TryResolveByKeyStorageWalk(StationRecordKey key)
    {
        var query = EntityQueryEnumerator<StationRecordKeyStorageComponent>();
        while (query.MoveNext(out var uid, out var storage))
        {
            if (storage.Key is not { } k || !k.Equals(key))
                continue;

            var cur = uid;
            var safety = 0;
            while (cur.IsValid() && safety++ < 64)
            {
                if (_mobQuery.HasComp(cur))
                    return cur;

                var parent = _transform.GetParentUid(cur);
                if (parent == cur)
                    break;

                cur = parent;
            }
        }

        return null;
    }
}
