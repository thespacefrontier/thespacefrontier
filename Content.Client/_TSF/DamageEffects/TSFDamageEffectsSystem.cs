// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Content.Shared._TSF;
using Content.Shared._TSF.Consciousness;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._TSF.DamageEffects;

public sealed class TSFDamageEffectsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    private TSFDamageOverlay? _overlay;
    private EntityUid? _damageMusicStream;
    private float _damageMusicCurrentGain;
    private EntityUid? _painSoundStream;
    private float _painSoundCurrentGain;
    private EntityUid? _tinnitusStream;
    private float _tinnitusCurrentGain;
    private static readonly SoundPathSpecifier DamageMusic = new("/Audio/_TSF/DamageSystem/unconscious_type_beat.ogg", new AudioParams(0, 1, 2000, 1, true, 0f));
    private static readonly SoundPathSpecifier PainSound = new("/Audio/_TSF/DamageSystem/PainDrone.ogg", new AudioParams(0, 0.6f, 2000, 1, true, 0f));
    private static readonly SoundPathSpecifier TinnitusSound = new("/Audio/_TSF/Effects/tinnitus.ogg", new AudioParams(0, 0.5f, 2000, 1, true, 0f));
    private static readonly FixedPoint2 PainSoundDamageThreshold = FixedPoint2.New(40);
    private const float DamageMusicRampSpeed = 0.6f;
    private const float PainSoundRampSpeed = 0.6f;
    private const float TinnitusRampSpeed = 1.5f;
    private float _mufflingCurrent;
    private const float MufflingRampSpeed = 1.5f;
    private FixedPoint2 _prevTotalDamage;
    private Dictionary<string, FixedPoint2> _prevDamagePerGroup = new();
    private bool _hasPrevDamage;

    private const string PiercingGroupId = "Piercing";
    private const string BruteGroupId = "Brute";
    private const string BurnGroupId = "Burn";
    private float _adrenalineRemaining;
    private float _adrenalineCooldown;
    private const float AdrenalineDuration = 2.2f;
    private static readonly FixedPoint2 AdrenalineMinDelta = FixedPoint2.New(4);
    private const float AdrenalineTriggerCooldown = 1f;
    private const string AirlossGroup = "Airloss";
    private const string ToxinGroup = "Toxin";

    /// <summary>Fallback when <see cref="TraumaticShockComponent"/> is not yet replicated.</summary>
    private float _shockLevel;
    private const float ShockDecayPerSecond = 0.35f;
    private const float ShockAddPerDamage = 0.018f;
    private const float ShockMax = 1.2f;

    private float _tinnitusEndTime;
    private const float TinnitusDuration = 6f;
    private static readonly FixedPoint2 TinnitusPiercingThreshold = FixedPoint2.New(16);
    private static readonly FixedPoint2 DisorientationMinDelta = FixedPoint2.New(3);

    private const float DisorientationKickMagnitude = 0.04f;
    private const float DisorientationBurstDuration = 0.4f;
    private float _disorientationBurstTime;

    private static readonly string[] SharpPainPhrases =
    {
        "tsf-status-sharp-pain-0", "tsf-status-sharp-pain-1", "tsf-status-sharp-pain-2", "tsf-status-sharp-pain-3",
        "tsf-status-sharp-pain-4", "tsf-status-sharp-pain-5", "tsf-status-sharp-pain-6", "tsf-status-sharp-pain-7",
        "tsf-status-sharp-pain-8", "tsf-status-sharp-pain-9", "tsf-status-sharp-pain-10", "tsf-status-sharp-pain-11",
        "tsf-status-sharp-pain-12", "tsf-status-sharp-pain-13", "tsf-status-sharp-pain-14", "tsf-status-sharp-pain-15",
        "tsf-status-sharp-pain-16", "tsf-status-sharp-pain-17", "tsf-status-sharp-pain-18", "tsf-status-sharp-pain-19",
        "tsf-status-sharp-pain-20", "tsf-status-sharp-pain-21", "tsf-status-sharp-pain-22", "tsf-status-sharp-pain-23",
        "tsf-status-sharp-pain-24", "tsf-status-sharp-pain-25", "tsf-status-sharp-pain-26", "tsf-status-sharp-pain-27",
        "tsf-status-sharp-pain-28", "tsf-status-sharp-pain-29", "tsf-status-sharp-pain-30",
    };
    private static readonly string[] AudiblePainPhrases =
    {
        "tsf-status-audible-pain-0", "tsf-status-audible-pain-1", "tsf-status-audible-pain-2", "tsf-status-audible-pain-3",
        "tsf-status-audible-pain-4", "tsf-status-audible-pain-5", "tsf-status-audible-pain-6", "tsf-status-audible-pain-7",
        "tsf-status-audible-pain-8", "tsf-status-audible-pain-9", "tsf-status-audible-pain-10", "tsf-status-audible-pain-11",
        "tsf-status-audible-pain-12", "tsf-status-audible-pain-13",
    };
    private static readonly string[] FearPhrases =
    {
        "tsf-status-fear-0", "tsf-status-fear-1", "tsf-status-fear-2", "tsf-status-fear-3", "tsf-status-fear-4",
        "tsf-status-fear-5", "tsf-status-fear-6", "tsf-status-fear-7", "tsf-status-fear-8", "tsf-status-fear-9",
        "tsf-status-fear-10", "tsf-status-fear-11", "tsf-status-fear-12", "tsf-status-fear-13", "tsf-status-fear-14",
        "tsf-status-fear-15", "tsf-status-fear-16", "tsf-status-fear-17", "tsf-status-fear-18",
    };
    private static readonly string[] NearDeathPoeticPhrases =
    {
        "tsf-status-near-death-poetic-0", "tsf-status-near-death-poetic-1", "tsf-status-near-death-poetic-2",
        "tsf-status-near-death-poetic-3", "tsf-status-near-death-poetic-4", "tsf-status-near-death-poetic-5",
        "tsf-status-near-death-poetic-6", "tsf-status-near-death-poetic-7", "tsf-status-near-death-poetic-8",
        "tsf-status-near-death-poetic-9",
    };
    private static readonly string[] NearDeathPositivePhrases =
    {
        "tsf-status-near-death-positive-0", "tsf-status-near-death-positive-1", "tsf-status-near-death-positive-2",
        "tsf-status-near-death-positive-3", "tsf-status-near-death-positive-4", "tsf-status-near-death-positive-5",
        "tsf-status-near-death-positive-6", "tsf-status-near-death-positive-7", "tsf-status-near-death-positive-8",
        "tsf-status-near-death-positive-9", "tsf-status-near-death-positive-10", "tsf-status-near-death-positive-11",
        "tsf-status-near-death-positive-12", "tsf-status-near-death-positive-13",
    };
    private static readonly string[] AfterUnconsciousPhrases =
    {
        "tsf-status-after-unconscious-0", "tsf-status-after-unconscious-1", "tsf-status-after-unconscious-2",
        "tsf-status-after-unconscious-3", "tsf-status-after-unconscious-4", "tsf-status-after-unconscious-5",
        "tsf-status-after-unconscious-6", "tsf-status-after-unconscious-7", "tsf-status-after-unconscious-8",
        "tsf-status-after-unconscious-9", "tsf-status-after-unconscious-10",
    };

    private const float StatusMessageBucketSeconds = 5f;
    private const float StatusMessageDisplayDuration = 5f;
    private const float StatusMessageCooldownAfterHide = 30f;
    private double _afterUnconsciousUntil;
    private double _statusMessageCooldownUntil;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TSFDamageOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdChecked>(OnThresholdChecked);
        SubscribeLocalEvent<TraumaticShockComponent, AfterAutoHandleStateEvent>(OnTraumaticShockStateHandled);
    }

    private void OnTraumaticShockStateHandled(EntityUid uid, TraumaticShockComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (uid == _playerManager.LocalEntity)
            UpdateOverlayIntensity(uid);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        _overlayManager.AddOverlay(_overlay!);
        UpdateOverlayIntensity(ev.Entity);
        var result = _audio.PlayGlobal(DamageMusic, Filter.Local(), true);
        _damageMusicStream = result?.Entity;
        _damageMusicCurrentGain = 0f;
        if (_damageMusicStream != null && TryComp(_damageMusicStream, out AudioComponent? comp))
            _audio.SetGain(_damageMusicStream, 0f, comp);

        var painResult = _audio.PlayGlobal(PainSound, Filter.Local(), true);
        _painSoundStream = painResult?.Entity;
        _painSoundCurrentGain = 0f;
        if (_painSoundStream != null && TryComp(_painSoundStream, out AudioComponent? painComp))
            _audio.SetGain(_painSoundStream, 0f, painComp);

        var tinnitusResult = _audio.PlayGlobal(TinnitusSound, Filter.Local(), true);
        _tinnitusStream = tinnitusResult?.Entity;
        _tinnitusCurrentGain = 0f;
        if (_tinnitusStream != null && TryComp(_tinnitusStream, out AudioComponent? tinnitusComp))
            _audio.SetGain(_tinnitusStream, 0f, tinnitusComp);
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _overlayManager.RemoveOverlay(_overlay!);
        _damageMusicStream = _audio.Stop(_damageMusicStream);
        _painSoundStream = _audio.Stop(_painSoundStream);
        _tinnitusStream = _audio.Stop(_tinnitusStream);
        _mufflingCurrent = 0f;
        if (_overlay != null)
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            _overlay.AdrenalineStrength = 0f;
            _overlay.BloodLossStrength = 0f;
            _overlay.ShockStrength = 0f;
            _overlay.Consciousness = 1f;
        }
        _hasPrevDamage = false;
        _prevDamagePerGroup.Clear();
        _adrenalineRemaining = 0f;
        _adrenalineCooldown = 0f;
        _shockLevel = 0f;
        _tinnitusEndTime = 0f;
        _afterUnconsciousUntil = 0;
        _statusMessageCooldownUntil = 0;
        TSFStatusMessageState.Message = null;
    }

    private bool ShouldTriggerTinnitus(DamageableComponent damageable)
    {
        var piercingDelta = FixedPoint2.Zero;
        var bruteDelta = FixedPoint2.Zero;
        var burnDelta = FixedPoint2.Zero;
        foreach (var (group, current) in damageable.DamagePerGroup)
        {
            var prev = _prevDamagePerGroup.TryGetValue(group, out var p) ? p : FixedPoint2.Zero;
            var d = current - prev;
            if (d <= FixedPoint2.Zero)
                continue;
            if (string.Equals(group, PiercingGroupId, StringComparison.OrdinalIgnoreCase))
                piercingDelta += d;
            else if (string.Equals(group, BruteGroupId, StringComparison.OrdinalIgnoreCase))
                bruteDelta += d;
            else if (string.Equals(group, BurnGroupId, StringComparison.OrdinalIgnoreCase))
                burnDelta += d;
        }
        if (piercingDelta > TinnitusPiercingThreshold)
            return true;
        if (bruteDelta > FixedPoint2.Zero && burnDelta > FixedPoint2.Zero)
            return true;
        return false;
    }

    private void UpdatePrevDamagePerGroup(DamageableComponent damageable)
    {
        _prevDamagePerGroup.Clear();
        foreach (var (k, v) in damageable.DamagePerGroup)
            _prevDamagePerGroup[k] = v;
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.Target != _playerManager.LocalEntity)
            return;
        if (ev.NewMobState == MobState.Alive && ev.OldMobState == MobState.Critical)
            _afterUnconsciousUntil = _timing.RealTime.TotalSeconds + 12;
        UpdateOverlayIntensity(ev.Target);
    }

    private void OnThresholdChecked(ref MobThresholdChecked ev)
    {
        if (ev.Target != _playerManager.LocalEntity)
            return;
        UpdateOverlayIntensity(ev.Target);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var local = _playerManager.LocalEntity;
        if (local == null || _overlay == null)
            return;

        var uid = local.Value;
        if (TryComp(uid, out DamageableComponent? damageable))
        {
            var delta = damageable.TotalDamage - _prevTotalDamage;
            if (_hasPrevDamage && delta > FixedPoint2.Zero && !HasComp<TraumaticShockComponent>(uid))
            {
                _shockLevel = Math.Min(ShockMax, _shockLevel + delta.Float() * ShockAddPerDamage);
                if (delta >= DisorientationMinDelta)
                    _disorientationBurstTime = DisorientationBurstDuration;

                if (delta > FixedPoint2.Zero && ShouldTriggerTinnitus(damageable))
                    _tinnitusEndTime = (float)_timing.RealTime.TotalSeconds + TinnitusDuration;
            }
            if (_hasPrevDamage && delta > AdrenalineMinDelta && _adrenalineCooldown <= 0f)
            {
                _adrenalineRemaining = AdrenalineDuration;
                _adrenalineCooldown = AdrenalineTriggerCooldown;
            }
            UpdatePrevDamagePerGroup(damageable);
            _prevTotalDamage = damageable.TotalDamage;
            _hasPrevDamage = true;
        }
        if (!HasComp<TraumaticShockComponent>(uid))
            _shockLevel = Math.Max(0f, _shockLevel - ShockDecayPerSecond * frameTime);
        if (_overlay != null)
        {
            if (TryComp(uid, out TraumaticShockComponent? traumatic))
                _overlay.ShockStrength = Math.Clamp(traumatic.Severity, 0f, 1f);
            else
                _overlay.ShockStrength = Math.Clamp(_shockLevel, 0f, 1f);
        }

        _adrenalineRemaining = Math.Max(0f, _adrenalineRemaining - frameTime);
        _adrenalineCooldown = Math.Max(0f, _adrenalineCooldown - frameTime);
        if (_overlay != null)
            _overlay.AdrenalineStrength = _adrenalineRemaining > 0f ? (_adrenalineRemaining / AdrenalineDuration) : 0f;

        UpdateOverlayIntensity(uid);
        TryUpdateStatusMessage(uid);

        const float CritOcclusionMute = 50f;
        bool unconscious = TryComp(uid, out ConsciousnessComponent? compForMuffling) && compForMuffling.Unconscious
            || (TryComp(uid, out MobStateComponent? mobStateForMuffling) && mobStateForMuffling.CurrentState == MobState.Critical);
        float targetMuffling = unconscious ? CritOcclusionMute : 0f;
        _mufflingCurrent += (targetMuffling - _mufflingCurrent) * Math.Clamp(MufflingRampSpeed * frameTime, 0f, 1f);

        var audioQuery = EntityQueryEnumerator<AudioComponent>();
        while (audioQuery.MoveNext(out var ent, out var audioComp))
        {
            if (!audioComp.Playing || ent == _damageMusicStream || ent == _painSoundStream || ent == _tinnitusStream || (audioComp.Flags & AudioFlags.NoOcclusion) != 0)
                continue;
            if (audioComp.Occlusion < _mufflingCurrent)
                audioComp.Occlusion = _mufflingCurrent;
        }

        bool unconsciousMusic = TryComp(uid, out ConsciousnessComponent? compForMusic) && compForMusic.Unconscious
            || (TryComp(uid, out MobStateComponent? mobStateForMusic) && mobStateForMusic.CurrentState == MobState.Critical);
        float targetGain = unconsciousMusic ? 1f : 0f;

        if (_damageMusicStream != null && Exists(_damageMusicStream) && TryComp(_damageMusicStream, out AudioComponent? musicComp))
        {
            _damageMusicCurrentGain += (targetGain - _damageMusicCurrentGain) * Math.Clamp(DamageMusicRampSpeed * frameTime, 0f, 1f);
            _audio.SetGain(_damageMusicStream, _damageMusicCurrentGain, musicComp);
        }

        bool painAllowed = damageable != null && damageable.TotalDamage >= PainSoundDamageThreshold;
        float painLevel = painAllowed && _overlay != null ? _overlay.DamageStrength : 0f;
        float painGainCurve = MathF.Pow(painLevel, 0.85f);
        float targetPainGain = painGainCurve * (1f - _damageMusicCurrentGain);
        if (_painSoundStream != null && Exists(_painSoundStream) && TryComp(_painSoundStream, out AudioComponent? painComp))
        {
            _painSoundCurrentGain += (targetPainGain - _painSoundCurrentGain) * Math.Clamp(PainSoundRampSpeed * frameTime, 0f, 1f);
            _audio.SetGain(_painSoundStream, _painSoundCurrentGain, painComp);
        }

        var now = (float)_timing.RealTime.TotalSeconds;
        var tinnitusRemaining = Math.Max(0f, _tinnitusEndTime - now);
        var targetTinnitusGain = tinnitusRemaining > 0.01f ? (tinnitusRemaining / TinnitusDuration) * 0.5f : 0f;
        if (_tinnitusStream != null && Exists(_tinnitusStream) && TryComp(_tinnitusStream, out AudioComponent? tinnitusComp))
        {
            _tinnitusCurrentGain += (targetTinnitusGain - _tinnitusCurrentGain) * Math.Clamp(TinnitusRampSpeed * frameTime, 0f, 1f);
            _audio.SetGain(_tinnitusStream, _tinnitusCurrentGain, tinnitusComp);
        }

        _disorientationBurstTime = Math.Max(0f, _disorientationBurstTime - frameTime);
        if (local != null && _disorientationBurstTime > 0f && TryComp(local, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Alive)
        {
            var strength = _disorientationBurstTime / DisorientationBurstDuration;
            var kick = new Vector2(
                (_random.NextFloat() - 0.5f) * 2f * DisorientationKickMagnitude * strength,
                (_random.NextFloat() - 0.5f) * 2f * DisorientationKickMagnitude * strength);
            _cameraRecoil.KickCamera(local.Value, kick);
        }
    }

    private void UpdateOverlayIntensity(EntityUid entity)
    {
        if (_overlay == null)
            return;
        if (!TryComp(entity, out MobStateComponent? mobState) || !TryComp(entity, out DamageableComponent? damageable)
            || !TryComp(entity, out MobThresholdsComponent? thresholds))
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            _overlay.BloodLossStrength = 0f;
            _overlay.Consciousness = 1f;
            return;
        }
        if (!thresholds.ShowOverlays)
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            _overlay.BloodLossStrength = 0f;
            _overlay.Consciousness = 1f;
            return;
        }
        if (mobState.CurrentState == MobState.Critical)
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 1f;
            _overlay.Consciousness = 0f;
            if (_mobThreshold.TryGetIncapThreshold(entity, out var critThresh, thresholds) && critThresh.HasValue)
            {
                var deathLevel = FixedPoint2.Zero;
                if (damageable.DamagePerGroup.TryGetValue(AirlossGroup, out var airloss))
                    deathLevel += airloss;
                if (damageable.DamagePerGroup.TryGetValue(ToxinGroup, out var toxin))
                    deathLevel += toxin;
                _overlay.BloodLossStrength = Math.Clamp((deathLevel / critThresh.Value).Float(), 0f, 1f);
            }
            return;
        }
        if (!_mobThreshold.TryGetIncapThreshold(entity, out var critThreshold, thresholds))
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            _overlay.BloodLossStrength = 0f;
            _overlay.Consciousness = 1f;
            return;
        }

        if (critThreshold.HasValue)
        {
            var deathLevel = FixedPoint2.Zero;
            if (damageable.DamagePerGroup.TryGetValue(AirlossGroup, out var airloss))
                deathLevel += airloss;
            if (damageable.DamagePerGroup.TryGetValue(ToxinGroup, out var toxin))
                deathLevel += toxin;
            _overlay.BloodLossStrength = Math.Clamp((deathLevel / critThreshold.Value).Float(), 0f, 1f);
        }
        else
            _overlay.BloodLossStrength = 0f;

        var thresh = critThreshold.Value;
        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            {
                if (TryComp(entity, out ConsciousnessComponent? consciousness) && consciousness.Unconscious)
                {
                    _overlay.DamageStrength = 0f;
                    _overlay.CritStrength = 1f;
                    _overlay.Consciousness = 0f;
                    break;
                }

                float painScalar = 0f;
                if (!_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(entity, out _))
                {
                    var weightSetId = _cfg.GetCVar(TSFCVars.TsfPainWeightSet);
                    var globalPain = _cfg.GetCVar(TSFCVars.TsfPainGlobalMultiplier);
                    if (_proto.TryIndex<TSFPainWeightPrototype>(weightSetId, out var painWeights))
                        painScalar = SharedPainMath.ComputePainLevel(damageable, _proto, painWeights, globalPain);
                }

                var painRatio = SharedTsfConsciousnessFormula.ComputePainRatio(painScalar, thresh);
                _overlay.DamageStrength = Math.Clamp(painRatio, 0f, 1f);
                if (_overlay.DamageStrength < 0.15f)
                    _overlay.DamageStrength = 0f;
                _overlay.CritStrength = 0f;

                if (TryComp(entity, out ConsciousnessComponent? c))
                {
                    _overlay.Consciousness = c.Level;
                }
                else
                {
                    var bloodDamageRatio = Math.Clamp(SharedPainMath.ComputeBloodlossStyleContribution(damageable, thresh), 0f, 1f);
                    var hypovolemiaRatio = 0f;
                    if (TryComp(entity, out BloodstreamComponent? bloodstream))
                    {
                        var bloodLevel = _bloodstream.GetBloodLevel((entity, bloodstream));
                        hypovolemiaRatio = SharedTsfConsciousnessFormula.ComputeHypovolemiaRatio(
                            bloodLevel,
                            _cfg.GetCVar(TSFCVars.TsfConsciousnessHypovolemiaBloodStart));
                    }

                    var shockSeverity = TryComp(entity, out TraumaticShockComponent? ts) ? ts.Severity : 0f;
                    var asphyxRatio = SharedTsfConsciousnessFormula.ComputeAsphyxiationRatio(
                        SharedPainMath.GetAsphyxiationDamage(damageable),
                        thresh);
                    _overlay.Consciousness = SharedTsfConsciousnessFormula.ComputeLevel(
                        painRatio,
                        bloodDamageRatio,
                        hypovolemiaRatio,
                        shockSeverity,
                        asphyxRatio,
                        _cfg.GetCVar(TSFCVars.TsfConsciousnessPainRatioBlend),
                        _cfg.GetCVar(TSFCVars.TsfConsciousnessBloodDamageRatioBlend),
                        _cfg.GetCVar(TSFCVars.TsfConsciousnessHypovolemiaBlend),
                        _cfg.GetCVar(TSFCVars.TsfConsciousnessTraumaticShockBlend),
                        _cfg.GetCVar(TSFCVars.TsfConsciousnessAsphyxiationBlend),
                        _cfg.GetCVar(TSFCVars.TsfPainToConsciousnessFactor));
                }

                break;
            }
            case MobState.Critical:
                _overlay.DamageStrength = 0f;
                _overlay.CritStrength = 1f;
                _overlay.Consciousness = 0f;
                break;
            default:
                _overlay.DamageStrength = 0f;
                _overlay.CritStrength = 0f;
                _overlay.Consciousness = 1f;
                break;
        }
    }

    private void TryUpdateStatusMessage(EntityUid uid)
    {
        var now = _timing.RealTime.TotalSeconds;
        if (!TryComp(uid, out MobStateComponent? mobState))
        {
            TSFStatusMessageState.Message = null;
            return;
        }
        if (TryComp(uid, out MobThresholdsComponent? thresholds) && !thresholds.ShowOverlays)
        {
            TSFStatusMessageState.Message = null;
            return;
        }

        if (TSFStatusMessageState.Message != null && now > TSFStatusMessageState.DisplayUntil)
        {
            TSFStatusMessageState.Message = null;
            _statusMessageCooldownUntil = now + StatusMessageCooldownAfterHide;
        }

        if (TSFStatusMessageState.Message != null || now < _statusMessageCooldownUntil)
            return;

        float pain = _overlay?.DamageStrength ?? 0f;
        float bloodLoss = _overlay?.BloodLossStrength ?? 0f;
        bool bloodLow = bloodLoss > 0.35f;
        bool afterUnconscious = now < _afterUnconsciousUntil;

        string[]? phraseList = null;

        if (mobState.CurrentState == MobState.Critical)
        {
            phraseList = ((int)(now / StatusMessageBucketSeconds) + 5) % 2 == 0 ? NearDeathPoeticPhrases : NearDeathPositivePhrases;
        }
        else if (bloodLow || pain >= 0.75f)
        {
            if (pain >= 0.95f)
                phraseList = SharpPainPhrases;
            else if (pain >= 0.75f)
                phraseList = AudiblePainPhrases;
            if (phraseList == null && bloodLow)
                phraseList = NearDeathPoeticPhrases;
        }
        else if (afterUnconscious)
        {
            phraseList = AfterUnconsciousPhrases;
        }
        else if (pain >= 0.5f)
        {
            phraseList = ((int)(now * 2) & 1) == 0 ? SharpPainPhrases : FearPhrases;
        }
        else if (pain >= 0.2f)
        {
            phraseList = FearPhrases;
        }

        if (phraseList == null || phraseList.Length == 0)
            return;

        var bucket = (int)(now / StatusMessageBucketSeconds);
        var seed = bucket * 13 + phraseList.Length;
        var idx = Math.Abs(seed % phraseList.Length);
        TSFStatusMessageState.Message = phraseList[idx];
        TSFStatusMessageState.DisplayUntil = now + StatusMessageDisplayDuration;
    }
}
