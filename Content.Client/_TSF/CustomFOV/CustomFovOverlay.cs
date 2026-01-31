using System.Linq;
using System.Numerics;
using Content.Shared._TSF.CustomFOV;
using Content.Shared.DrawDepth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._TSF.CustomFOV;

public sealed class CustomFovOverlay : Overlay
{
    private const float FadeInDuration = 0.75f;
    private const float FadeOutDuration = 0.5f;

    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _prototype;
    private readonly IGameTiming _gameTiming;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly SpriteSpecifier _fovCorner;
    private readonly ShaderInstance _shader;

    private Dictionary<EntityUid, Dictionary<Vector2i, Entity<TransformComponent>>> _gridTileToEntity = new();
    private Dictionary<(EntityUid Grid, Vector2i Tile, int Corner), CornerState> _cornerState = new();
    private HashSet<(EntityUid Grid, Vector2i Tile, int Corner)> _cornersDrawnThisFrame = new();

    private struct CornerState
    {
        public double FirstVisibleTime;
        public double? FadeOutStartTime;
        public Vector2 LastWorldPos;
        public Angle LastWorldRot;
    }

    internal CustomFovOverlay(IEntityManager entMan, IPrototypeManager prototype, IGameTiming gameTiming)
    {
        _entMan = entMan;
        _prototype = prototype;
        _gameTiming = gameTiming;

        _fovCorner = new SpriteSpecifier.Texture(new ResPath("_TSF/Misc/fov_corner.png"));
        _sprite = entMan.System<SpriteSystem>();
        _transform = entMan.System<SharedTransformSystem>();
        _shader = _prototype.Index<ShaderPrototype>("unshaded").InstanceUnique();

        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.WallFovOverlay;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _gridTileToEntity.Clear();
        _cornersDrawnThisFrame.Clear();

        if (args.Viewport.Eye is not { } eye || !eye.DrawFov)
            return;

        var eyePos = eye.Position.Position;
        var texture = _sprite.Frame0(_fovCorner);
        var handle = args.WorldHandle;
        var now = _gameTiming.RealTime.TotalSeconds;

        var customFovQuery = _entMan.AllEntityQueryEnumerator<FoVOverlayComponent>();
        var occluderQuery = _entMan.GetEntityQuery<OccluderComponent>();
        var xformQuery = _entMan.GetEntityQuery<TransformComponent>();

        while (customFovQuery.MoveNext(out var uid, out _))
        {
            if (!occluderQuery.TryGetComponent(uid, out var occluder) || !occluder.Enabled)
                continue;

            if (!xformQuery.TryGetComponent(uid, out var xform) || !xform.Anchored || !xform.GridUid.HasValue)
                continue;

            var grid = _entMan.GetComponent<MapGridComponent>(xform.GridUid.Value);
            var tile = grid.CoordinatesToTile(xform.Coordinates);

            if (!_gridTileToEntity.TryGetValue(xform.GridUid.Value, out var gridDict))
            {
                gridDict = new Dictionary<Vector2i, Entity<TransformComponent>>();
                _gridTileToEntity[xform.GridUid.Value] = gridDict;
            }

            if (gridDict.ContainsKey(tile))
                continue;

            gridDict[tile] = new Entity<TransformComponent>(uid, xform);
        }

        handle.UseShader(_shader);

        void DrawCornerAt(Vector2 worldPos, Angle worldRot, float alpha)
        {
            if (texture == null || alpha <= 0f)
                return;
            var matrix = Matrix3Helpers.CreateTransform(worldPos, worldRot);
            handle.SetTransform(in matrix);
            handle.DrawTexture(texture, new Vector2(-0.5f, -0.5f), Color.White.WithAlpha(alpha));
        }

        void DrawFovCorner(EntityUid gridUid, Vector2i tile, int cornerIndex, Vector2 worldPos, Angle worldRot)
        {
            if (texture == null)
                return;
            var key = (gridUid, tile, cornerIndex);
            if (!_cornerState.TryGetValue(key, out var state))
            {
                state = new CornerState
                {
                    FirstVisibleTime = now,
                    FadeOutStartTime = null,
                    LastWorldPos = worldPos,
                    LastWorldRot = worldRot
                };
                _cornerState[key] = state;
            }
            else
            {
                state.FadeOutStartTime = null;
                state.LastWorldPos = worldPos;
                state.LastWorldRot = worldRot;
                _cornerState[key] = state;
            }
            _cornersDrawnThisFrame.Add(key);
            var elapsed = (float)(now - state.FirstVisibleTime);
            var alpha = Math.Min(1f, elapsed / FadeInDuration);
            DrawCornerAt(worldPos, worldRot, alpha);
        }

        foreach (var (gridUid, objMap) in _gridTileToEntity)
        {
            var gridRot = _transform.GetWorldRotation(gridUid);

            foreach (var (pos, entityEntry) in objMap)
            {
                var (worldPosition, _, _) = _transform.GetWorldPositionRotationMatrix(entityEntry.Comp, xformQuery);

                Vector2i GetDirRelativeToEdge(Vector2i edge)
                {
                    var invGrid = Matrix3Helpers.CreateInverseTransform(worldPosition, gridRot);
                    var relativePos = Vector2.Transform(eyePos, invGrid) + new Vector2(edge.X * 0.5f, edge.Y * 0.5f);
                    return new Vector2i(MathF.Sign(relativePos.X), MathF.Sign(relativePos.Y));
                }

                bool southNeighbour = objMap.ContainsKey(pos + Vector2i.Down);
                bool southShadowed = GetDirRelativeToEdge(Vector2i.Up).Y > 0;
                bool southObscured = southShadowed || southNeighbour;

                bool northNeighbour = objMap.ContainsKey(pos + Vector2i.Up);
                bool northShadowed = GetDirRelativeToEdge(Vector2i.Down).Y < 0;
                bool northObscured = northShadowed || northNeighbour;

                bool eastNeighbour = objMap.ContainsKey(pos + Vector2i.Right);
                bool eastShadowed = GetDirRelativeToEdge(Vector2i.Left).X < 0;
                bool eastObscured = eastShadowed || eastNeighbour;

                bool westNeighbour = objMap.ContainsKey(pos + Vector2i.Left);
                bool westShadowed = GetDirRelativeToEdge(Vector2i.Right).X > 0;
                bool westObscured = westShadowed || westNeighbour;

                if (southObscured && westObscured && (southShadowed || westShadowed))
                    DrawFovCorner(gridUid, pos, 0, worldPosition, gridRot + Angle.FromDegrees(0));

                if (southObscured && eastObscured && (southShadowed || eastShadowed))
                    DrawFovCorner(gridUid, pos, 1, worldPosition, gridRot + Angle.FromDegrees(90));

                if (northObscured && eastObscured && (northShadowed || eastShadowed))
                    DrawFovCorner(gridUid, pos, 2, worldPosition, gridRot + Angle.FromDegrees(180));

                if (northObscured && westObscured && (northShadowed || westShadowed))
                    DrawFovCorner(gridUid, pos, 3, worldPosition, gridRot + Angle.FromDegrees(270));
            }
        }

        foreach (var key in _cornerState.Keys.ToList())
        {
            if (_cornersDrawnThisFrame.Contains(key))
                continue;
            var state = _cornerState[key];
            if (state.FadeOutStartTime == null)
            {
                state.FadeOutStartTime = now;
                _cornerState[key] = state;
            }
            var fadeOutElapsed = (float)(now - state.FadeOutStartTime.Value);
            var alpha = 1f - Math.Min(1f, fadeOutElapsed / FadeOutDuration);
            if (alpha <= 0f)
            {
                _cornerState.Remove(key);
                continue;
            }
            DrawCornerAt(state.LastWorldPos, state.LastWorldRot, alpha);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        _gridTileToEntity.Clear();
    }
}
