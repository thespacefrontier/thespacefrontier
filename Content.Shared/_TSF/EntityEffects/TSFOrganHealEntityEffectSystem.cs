using Content.Shared._TSF.Organs;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._TSF.EntityEffects;

/// <summary>
/// Heals organ damage on entities with TSFOrganDamageComponent.
/// Amount is negative = healing (e.g. -0.05 means heal 0.05 per metabolism tick).
/// Specify which organ(s) to heal via the Organ field.
/// </summary>
public sealed partial class TSFOrganHealEntityEffectSystem
    : EntityEffectSystem<TSFOrganDamageComponent, TSFOrganHeal>
{
    protected override void Effect(Entity<TSFOrganDamageComponent> entity, ref EntityEffectEvent<TSFOrganHeal> args)
    {
        var organs = entity.Comp;
        var amount = args.Effect.Amount * args.Scale;

        switch (args.Effect.Organ)
        {
            case TSFOrganTarget.Brain:
                // Brain healing only works if below MaxBrainHealThreshold
                if (organs.Brain <= args.Effect.MaxBrainHealThreshold)
                    organs.Brain = Math.Max(organs.Brain + amount, 0f);
                break;
            case TSFOrganTarget.Heart:
                organs.Heart = Math.Clamp(organs.Heart + amount, 0f, 1f);
                if (organs.Heart < organs.HeartStopThreshold)
                    organs.HeartStopped = false;
                break;
            case TSFOrganTarget.Lungs:
                organs.LungLeft = Math.Clamp(organs.LungLeft + amount, 0f, 1f);
                organs.LungRight = Math.Clamp(organs.LungRight + amount, 0f, 1f);
                break;
            case TSFOrganTarget.Liver:
                organs.Liver = Math.Clamp(organs.Liver + amount, 0f, 1f);
                break;
            case TSFOrganTarget.Stomach:
                organs.Stomach = Math.Clamp(organs.Stomach + amount, 0f, 1f);
                break;
            case TSFOrganTarget.Eyes:
                organs.Eyes = Math.Clamp(organs.Eyes + amount, 0f, 1f);
                break;
            case TSFOrganTarget.Trachea:
                organs.Trachea = Math.Clamp(organs.Trachea + amount, 0f, 1f);
                break;
            case TSFOrganTarget.All:
                // Heal all organs (except brain, which has its own rules)
                organs.Heart = Math.Clamp(organs.Heart + amount, 0f, 1f);
                organs.LungLeft = Math.Clamp(organs.LungLeft + amount, 0f, 1f);
                organs.LungRight = Math.Clamp(organs.LungRight + amount, 0f, 1f);
                organs.Liver = Math.Clamp(organs.Liver + amount, 0f, 1f);
                organs.Stomach = Math.Clamp(organs.Stomach + amount, 0f, 1f);
                organs.Intestines = Math.Clamp(organs.Intestines + amount, 0f, 1f);
                organs.Trachea = Math.Clamp(organs.Trachea + amount, 0f, 1f);
                organs.Eyes = Math.Clamp(organs.Eyes + amount, 0f, 1f);
                if (organs.Heart < organs.HeartStopThreshold)
                    organs.HeartStopped = false;
                break;
            case TSFOrganTarget.AllWithBrain:
                // Heal everything including brain (for Thiamine etc.)
                organs.Heart = Math.Clamp(organs.Heart + amount, 0f, 1f);
                organs.LungLeft = Math.Clamp(organs.LungLeft + amount, 0f, 1f);
                organs.LungRight = Math.Clamp(organs.LungRight + amount, 0f, 1f);
                organs.Liver = Math.Clamp(organs.Liver + amount, 0f, 1f);
                organs.Stomach = Math.Clamp(organs.Stomach + amount, 0f, 1f);
                organs.Intestines = Math.Clamp(organs.Intestines + amount, 0f, 1f);
                organs.Trachea = Math.Clamp(organs.Trachea + amount, 0f, 1f);
                organs.Eyes = Math.Clamp(organs.Eyes + amount, 0f, 1f);
                if (organs.Brain <= args.Effect.MaxBrainHealThreshold)
                    organs.Brain = Math.Max(organs.Brain + amount, 0f);
                if (organs.Heart < organs.HeartStopThreshold)
                    organs.HeartStopped = false;
                break;
        }

        Dirty(entity.Owner, organs);
    }
}

/// <summary>
/// Entity effect for healing TSF organ damage.
/// </summary>
public sealed partial class TSFOrganHeal : EntityEffectBase<TSFOrganHeal>
{
    /// <summary>
    /// Which organ to heal. Use negative Amount for healing.
    /// </summary>
    [DataField(required: true)]
    public TSFOrganTarget Organ = TSFOrganTarget.All;

    /// <summary>
    /// Amount of organ damage change per metabolism tick (negative = healing).
    /// </summary>
    [DataField]
    public float Amount = -0.02f;

    /// <summary>
    /// Maximum brain damage at which brain healing still works.
    /// Mannitol heals brain only below 0.6.
    /// </summary>
    [DataField]
    public float MaxBrainHealThreshold = 0.6f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-tsf-organ-heal",
            ("organ", Organ.ToString()),
            ("amount", MathF.Abs(Amount)),
            ("chance", Probability));
}

public enum TSFOrganTarget : byte
{
    Brain,
    Heart,
    Lungs,
    Liver,
    Stomach,
    Eyes,
    Trachea,
    All,
    AllWithBrain,
}
