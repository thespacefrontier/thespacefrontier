// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._TSF.CustomFOV;

public sealed class CustomFovSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private CustomFovOverlay? _fovOverlay;

    public override void Initialize()
    {
        base.Initialize();
        _fovOverlay = new CustomFovOverlay(EntityManager, _prototype, _gameTiming);
        _overlay.AddOverlay(_fovOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_fovOverlay != null)
            _overlay.RemoveOverlay(_fovOverlay);
    }
}
