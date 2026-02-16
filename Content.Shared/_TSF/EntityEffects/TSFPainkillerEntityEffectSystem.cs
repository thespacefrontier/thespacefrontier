using Content.Shared._TSF.Pain;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._TSF.EntityEffects;

/// <summary>
/// Sets PainkillerStrength on TSFPainComponent while the reagent is metabolized.
/// Higher Strength = stronger pain reduction.
/// Morphine: 0.9, Ibuprofen: 0.4.
/// </summary>
public sealed partial class TSFPainkillerEntityEffectSystem
    : EntityEffectSystem<TSFPainComponent, TSFPainkiller>
{
    protected override void Effect(Entity<TSFPainComponent> entity, ref EntityEffectEvent<TSFPainkiller> args)
    {
        var pain = entity.Comp;

        // Only upgrade painkiller strength, never downgrade (strongest wins)
        if (args.Effect.Strength > pain.PainkillerStrength)
            pain.PainkillerStrength = args.Effect.Strength;

        // Direct pain reduction per metabolism tick
        if (args.Effect.PainReduction > 0f)
            pain.Pain = Math.Max(pain.Pain - args.Effect.PainReduction * args.Scale, 0f);

        Dirty(entity.Owner, pain);
    }
}

/// <summary>
/// Painkiller entity effect. Reduces perceived pain and speeds up pain decay.
/// </summary>
public sealed partial class TSFPainkiller : EntityEffectBase<TSFPainkiller>
{
    /// <summary>
    /// Painkiller strength (0-1). 0.9 = morphine, 0.4 = ibuprofen.
    /// </summary>
    [DataField(required: true)]
    public float Strength = 0.9f;

    /// <summary>
    /// Direct pain reduction per metabolism tick (on top of the multiplier effect).
    /// </summary>
    [DataField]
    public float PainReduction = 0f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-tsf-painkiller",
            ("strength", (int)(Strength * 100f)),
            ("chance", Probability));
}
