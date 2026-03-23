// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._TSF.Consciousness;
using Content.Shared.Damage.Components;
using Content.Shared.HealthExaminable;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._TSF.Health;

public sealed class TSFHealthExamineSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, HealthBeingExaminedEvent>(OnHealthExamined);
    }

    private void OnHealthExamined(EntityUid uid, DamageableComponent _, ref HealthBeingExaminedEvent args)
    {
        if (_mobState.IsCritical(uid))
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-mob-critical"));
        }

        if (TryComp<ConsciousnessComponent>(uid, out var conc))
        {
            if (conc.Unconscious)
            {
                args.Message.PushNewline();
                args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-unconscious"));
            }
            else if (conc.Level < 0.35f)
            {
                args.Message.PushNewline();
                args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-consciousness-critical"));
            }
            else if (conc.Level < 0.6f)
            {
                args.Message.PushNewline();
                args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-consciousness-low"));
            }
        }

        if (TryComp<TraumaticShockComponent>(uid, out var shock) && shock.Severity > 0.35f)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-traumatic-shock"));
        }

        if (_statusEffects.HasStatusEffect(uid, new EntProtoId("StatusEffectHypovolemicShock")))
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-hypovolemic-shock"));
        }

        if (TryComp<WoundInfectionTrackerComponent>(uid, out var wound) && wound.Infected)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-wound-infection"));
            if (wound.SepsisStage >= 2)
            {
                args.Message.PushNewline();
                args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-sepsis-severe"));
            }
            else if (wound.SepsisStage == 1)
            {
                args.Message.PushNewline();
                args.Message.AddMarkupOrThrow(Loc.GetString("tsf-health-examine-sepsis-mild"));
            }
        }
    }
}
