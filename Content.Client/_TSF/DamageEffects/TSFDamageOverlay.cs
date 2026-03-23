// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._TSF.DamageEffects;

public sealed class TSFDamageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "TSFDamageVignette";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public float DamageStrength;
    public float CritStrength;
    public float AdrenalineStrength;
    public float BloodLossStrength;
    public float ShockStrength;
    public float Consciousness;

    private float _lerpDamageStrength;
    private float _lerpCritStrength;
    private float _lerpAdrenalineStrength;
    private float _lerpBloodLossStrength;
    private float _lerpShockStrength;
    private float _lerpConsciousness;

    public TSFDamageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
        ZIndex = 8;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } local || !_entityManager.TryGetComponent(local, out EyeComponent? eye))
            return false;
        if (args.Viewport.Eye != eye.Eye)
            return false;

        var dt = (float)_timing.FrameTime.TotalSeconds;
        _lerpDamageStrength += (DamageStrength - _lerpDamageStrength) * Math.Clamp(dt * 6f, 0f, 1f);
        _lerpCritStrength += (CritStrength - _lerpCritStrength) * Math.Clamp(dt * 6f, 0f, 1f);
        _lerpAdrenalineStrength += (AdrenalineStrength - _lerpAdrenalineStrength) * Math.Clamp(dt * 8f, 0f, 1f);
        _lerpBloodLossStrength += (BloodLossStrength - _lerpBloodLossStrength) * Math.Clamp(dt * 4f, 0f, 1f);
        _lerpShockStrength += (ShockStrength - _lerpShockStrength) * Math.Clamp(dt * 5f, 0f, 1f);
        _lerpConsciousness += (Consciousness - _lerpConsciousness) * Math.Clamp(dt * 4f, 0f, 1f);

        return _lerpDamageStrength > 0.01f || _lerpCritStrength > 0.01f || _lerpAdrenalineStrength > 0.01f || _lerpBloodLossStrength > 0.01f
            || _lerpShockStrength > 0.01f || _lerpConsciousness < 0.99f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("DamageStrength", _lerpDamageStrength);
        _shader.SetParameter("CritStrength", _lerpCritStrength);
        _shader.SetParameter("AdrenalineStrength", _lerpAdrenalineStrength);
        _shader.SetParameter("BloodLossStrength", _lerpBloodLossStrength);
        _shader.SetParameter("ShockStrength", _lerpShockStrength);
        _shader.SetParameter("Consciousness", _lerpConsciousness);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
