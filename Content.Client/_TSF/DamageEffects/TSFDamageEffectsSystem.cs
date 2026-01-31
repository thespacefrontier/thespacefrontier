using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Player;

namespace Content.Client._TSF.DamageEffects;

public sealed class TSFDamageEffectsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private TSFDamageOverlay? _overlay;
    private EntityUid? _damageMusicStream;
    private float _damageMusicCurrentGain;
    private static readonly SoundPathSpecifier DamageMusic = new("/Audio/_TSF/DamageSystem/unconscious_type_beat.ogg", new AudioParams(0, 1, 2000, 1, true, 0f));
    private static readonly FixedPoint2 DamageMusicThreshold = FixedPoint2.New(35);
    private const float DamageMusicRampSpeed = 1.2f;
    private float _mufflingCurrent;
    private const float MufflingRampSpeed = 1.5f;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TSFDamageOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdChecked>(OnThresholdChecked);
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
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _overlayManager.RemoveOverlay(_overlay!);
        _damageMusicStream = _audio.Stop(_damageMusicStream);
        _mufflingCurrent = 0f;
        if (_overlay != null)
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.Target != _playerManager.LocalEntity)
            return;
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
        UpdateOverlayIntensity(uid);

        float targetMuffling = 0f;
        if (TryComp(uid, out DamageableComponent? dmg) && TryComp(uid, out MobThresholdsComponent? th) && th.ShowOverlays
            && dmg.TotalDamage >= DamageMusicThreshold && _mobThreshold.TryGetIncapThreshold(uid, out var critThresh, th))
        {
            var range = critThresh.Value - DamageMusicThreshold;
            if (range > FixedPoint2.Zero)
                targetMuffling = ((dmg.TotalDamage - DamageMusicThreshold) / range).Float();
            targetMuffling = Math.Clamp(targetMuffling, 0f, 1f);
        }
        _mufflingCurrent += (targetMuffling - _mufflingCurrent) * Math.Clamp(MufflingRampSpeed * frameTime, 0f, 1f);

        var audioQuery = EntityQueryEnumerator<AudioComponent>();
        while (audioQuery.MoveNext(out var ent, out var audioComp))
        {
            if (!audioComp.Playing || ent == _damageMusicStream || (audioComp.Flags & AudioFlags.NoOcclusion) != 0)
                continue;
            if (audioComp.Occlusion < _mufflingCurrent)
                audioComp.Occlusion = _mufflingCurrent;
        }

        if (_damageMusicStream == null || !Exists(_damageMusicStream))
            return;
        if (!TryComp(_damageMusicStream, out AudioComponent? musicComp))
            return;

        float targetGain = 0f;
        if (TryComp(uid, out DamageableComponent? damageable) && TryComp(uid, out MobThresholdsComponent? thresholds)
            && thresholds.ShowOverlays && damageable.TotalDamage >= DamageMusicThreshold
            && _mobThreshold.TryGetIncapThreshold(uid, out var critThreshold, thresholds))
        {
            var range = critThreshold.Value - DamageMusicThreshold;
            if (range > FixedPoint2.Zero)
                targetGain = ((damageable.TotalDamage - DamageMusicThreshold) / range).Float();
            targetGain = Math.Clamp(targetGain, 0f, 1f);
        }

        _damageMusicCurrentGain += (targetGain - _damageMusicCurrentGain) * Math.Clamp(DamageMusicRampSpeed * frameTime, 0f, 1f);
        _audio.SetGain(_damageMusicStream, _damageMusicCurrentGain, musicComp);
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
            return;
        }
        if (!thresholds.ShowOverlays)
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            return;
        }
        if (!_mobThreshold.TryGetIncapThreshold(entity, out var critThreshold, thresholds))
        {
            _overlay.DamageStrength = 0f;
            _overlay.CritStrength = 0f;
            return;
        }

        var thresh = critThreshold.Value;
        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            {
                FixedPoint2 painLevel = FixedPoint2.Zero;
                if (!_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(entity, out _))
                {
                    foreach (var painType in damageable.PainDamageGroups)
                    {
                        if (damageable.DamagePerGroup.TryGetValue(painType, out var pain))
                            painLevel += pain;
                    }
                }
                _overlay.DamageStrength = FixedPoint2.Min(1.0f, painLevel / thresh).Float();
                if (_overlay.DamageStrength < 0.05f)
                    _overlay.DamageStrength = 0f;
                _overlay.CritStrength = 0f;
                break;
            }
            case MobState.Critical:
                _overlay.DamageStrength = 0f;
                if (_mobThreshold.TryGetDeadPercentage(entity, damageable.TotalDamage, out var critPct))
                    _overlay.CritStrength = critPct.Value.Float();
                else
                    _overlay.CritStrength = 0.5f;
                break;
            default:
                _overlay.DamageStrength = 0f;
                _overlay.CritStrength = 0f;
                break;
        }
    }
}
