using Content.Server._TSF.Surgery;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._TSF.DamageEffects;

/// <summary>
/// Makes the character scream in pain when taking damage.
/// Cooldown prevents spam from rapid fire.
/// </summary>
public sealed class TSFPainCrySystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TSFLimbDamageTriggerSystem _limbTrigger = default!;

    private static readonly FixedPoint2 MinDamageForScream = FixedPoint2.New(3);
    private const float CooldownSeconds = 9.0f;
    private readonly Dictionary<EntityUid, TimeSpan> _cooldownUntil = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HumanoidAppearanceComponent, EmoteEvent>(OnEmote);
        EntityManager.EntityDeleted += OnEntityDeleted;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityDeleted -= OnEntityDeleted;
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> entity)
    {
        _cooldownUntil.Remove(entity.Owner);
    }

    private void OnEmote(EntityUid uid, HumanoidAppearanceComponent comp, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        if (args.Emote.ID == "ScreamPain")
            args.Handled = true;
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        var damageDelta = args.DamageDelta;
        _limbTrigger.OnDamageChangedForLimb(ent.Owner, ref args);

        var total = damageDelta.GetTotal();
        if (total < MinDamageForScream)
            return;
        if (_mobState.IsDead(ent) || _mobState.IsCritical(ent))
            return;
        var now = _timing.CurTime;
        if (_cooldownUntil.TryGetValue(ent, out var until) && now < until)
            return;

        if (!HasComp<HumanoidAppearanceComponent>(ent))
            return;

        _chat.TryEmoteWithoutChat(ent, "Scream", ignoreActionBlocker: true);

        _chat.TryEmoteWithChat(ent, "ScreamPain", ChatTransmitRange.Normal, ignoreActionBlocker: true);

        _cooldownUntil[ent] = now + TimeSpan.FromSeconds(CooldownSeconds);
    }
}
