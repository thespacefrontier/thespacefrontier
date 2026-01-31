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

    private float _lerpDamageStrength;
    private float _lerpCritStrength;

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

        return _lerpDamageStrength > 0.01f || _lerpCritStrength > 0.01f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("DamageStrength", _lerpDamageStrength);
        _shader.SetParameter("CritStrength", _lerpCritStrength);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
