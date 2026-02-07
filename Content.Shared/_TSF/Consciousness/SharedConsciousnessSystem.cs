using Content.Shared.Movement.Events;
using Content.Shared.Speech;
using Content.Shared.Interaction.Events;

namespace Content.Shared._TSF.Consciousness;

public sealed partial class SharedConsciousnessSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsciousnessComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<ConsciousnessComponent, ConsciousAttemptEvent>(OnConsciousAttempt);
        SubscribeLocalEvent<SpeakAttemptEvent>(OnSpeakAttempt);
    }

    private void OnUpdateCanMove(Entity<ConsciousnessComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Unconscious)
            args.Cancel();
    }

    private void OnConsciousAttempt(Entity<ConsciousnessComponent> ent, ref ConsciousAttemptEvent args)
    {
        if (ent.Comp.Unconscious)
            args.Cancelled = true;
    }

    private void OnSpeakAttempt(SpeakAttemptEvent args)
    {
        if (TryComp(args.Uid, out ConsciousnessComponent? comp) && comp.Unconscious)
            args.Cancel();
    }
}
