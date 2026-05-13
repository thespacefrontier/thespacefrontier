// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Voting;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Voting;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    private const int TSFAutonomousVoteSeconds = 30;

    private static readonly string[] TSFLobbyVotePresetIds =
    {
        "Extended",
        "Secret"
    };

    private Dictionary<string, string> TSFGetLobbyVotePresets()
    {
        var presets = new Dictionary<string, string>();

        foreach (var id in TSFLobbyVotePresetIds)
        {
            if (!_prototypeManager.TryIndex<GamePresetPrototype>(id, out var preset))
                continue;

            if (_playerManager.PlayerCount < (preset.MinPlayers ?? int.MinValue))
                continue;

            if (_playerManager.PlayerCount > (preset.MaxPlayers ?? int.MaxValue))
                continue;

            presets[preset.ID] = preset.ModeTitle;
        }

        return presets;
    }

    public void TSFRunLobbyAutonomousVotes(Action onComplete)
    {
        _gameTicker ??= _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();

        void Done()
        {
            onComplete();
        }

        var maps = _gameMapManager.CurrentlyEligibleMaps().ToDictionary(m => m, m => m.MapName);
        var presets = TSFGetLobbyVotePresets();

        if (presets.Count == 1)
        {
            var id = presets.Keys.First();
            _entityManager.EntitySysManager.GetEntitySystem<GameTicker>().SetGamePreset(id);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium,
                $"TSF autonomous lobby: single preset available, set {id}");
        }

        var needPresetVote = presets.Count >= 2;
        var needMapVote = maps.Count > 0;

        var pending = 0;
        if (needPresetVote)
            pending++;
        if (needMapVote)
            pending++;

        if (pending == 0)
        {
            Done();
            return;
        }

        void PartDone()
        {
            pending--;
            if (pending == 0)
                Done();
        }

        if (needPresetVote)
        {
            var presetOptions = new VoteOptions
            {
                Title = Loc.GetString("ui-vote-gamemode-title"),
                Duration = TimeSpan.FromSeconds(TSFAutonomousVoteSeconds),
            };

            foreach (var (k, v) in presets)
            {
                presetOptions.Options.Add((Loc.GetString(v), k));
            }

            WirePresetVoteInitiator(presetOptions, null);

            var presetVote = CreateVote(presetOptions);
            TimeoutStandardVote(StandardVoteType.Preset);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"TSF autonomous lobby preset vote started");

            void PresetVoteFinished(IVoteHandle voteSender, VoteFinishedEventArgs ev)
            {
                string picked;
                if (ev.Winner == null)
                {
                    picked = (string) _random.Pick(ev.Winners);
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-gamemode-tie", ("picked", Loc.GetString(presets[picked]))));
                }
                else
                {
                    picked = (string) ev.Winner;
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-gamemode-win", ("winner", Loc.GetString(presets[picked]))));
                }

                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset vote finished: {picked}");
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                ticker.SetGamePreset(picked);
                PartDone();
            }

            presetVote.OnFinished += PresetVoteFinished;
        }

        if (needMapVote)
        {
            var options = new VoteOptions
            {
                Title = Loc.GetString("ui-vote-map-title"),
                Duration = TimeSpan.FromSeconds(TSFAutonomousVoteSeconds),
            };

            foreach (var (k, v) in maps)
            {
                options.Options.Add((v, k));
            }

            WirePresetVoteInitiator(options, null);

            var vote = CreateVote(options);
            TimeoutStandardVote(StandardVoteType.Map);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"TSF autonomous lobby map vote started");

            void MapVoteFinished(IVoteHandle voteSender, VoteFinishedEventArgs ev)
            {
                GameMapPrototype picked;
                if (ev.Winner == null)
                {
                    picked = (GameMapPrototype) _random.Pick(ev.Winners);
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-map-tie", ("picked", maps[picked])));
                }
                else
                {
                    picked = (GameMapPrototype) ev.Winner;
                    _chatManager.DispatchServerAnnouncement(
                        Loc.GetString("ui-vote-map-win", ("winner", maps[picked])));
                }

                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Map vote finished: {picked.MapName}");
                var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
                if (ticker.CanUpdateMap())
                {
                    if (_gameMapManager.TrySelectMapIfEligible(picked.ID))
                    {
                        ticker.UpdateInfoText();
                    }
                }
                else
                {
                    if (ticker.RoundPreloadTime <= TimeSpan.Zero)
                    {
                        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-map-notlobby"));
                    }
                    else
                    {
                        var timeString = $"{ticker.RoundPreloadTime.Minutes:0}:{ticker.RoundPreloadTime.Seconds:00}";
                        _chatManager.DispatchServerAnnouncement(
                            Loc.GetString("ui-vote-map-notlobby-time", ("time", timeString)));
                    }
                }

                PartDone();
            }

            vote.OnFinished += MapVoteFinished;
        }

        _gameTicker.UpdateInfoText();
    }
}
