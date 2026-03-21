using System.Linq;
using Content.Server.GameTicking;
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

    public void TSFRunLobbyAutonomousVotes(Action onComplete)
    {
        _gameTicker ??= _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();

        void Done()
        {
            onComplete();
        }

        void StartMapVote()
        {
            var maps = _gameMapManager.CurrentlyEligibleMaps().ToDictionary(m => m, m => m.MapName);
            if (maps.Count == 0)
            {
                Done();
                return;
            }

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
            _gameTicker.UpdateInfoText();
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

                Done();
            }

            vote.OnFinished += MapVoteFinished;
        }

        void AfterPresetVote()
        {
            Timer.Spawn(0, StartMapVote);
        }

        var presets = GetGamePresets();

        if (presets.Count == 0)
        {
            Timer.Spawn(0, StartMapVote);
            return;
        }

        if (presets.Count == 1)
        {
            var picked = presets.Keys.First();
            _entityManager.EntitySysManager.GetEntitySystem<GameTicker>().SetGamePreset(picked);
            _adminLogger.Add(LogType.Vote, LogImpact.Medium,
                $"TSF autonomous lobby: single preset available, set {picked}");
            StartMapVote();
            return;
        }

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
        _gameTicker.UpdateInfoText();
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
            AfterPresetVote();
        }

        presetVote.OnFinished += PresetVoteFinished;
    }
}
