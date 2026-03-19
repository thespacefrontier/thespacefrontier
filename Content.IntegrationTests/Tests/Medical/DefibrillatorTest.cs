#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Tests for defibrilators.
/// </summary>
[TestOf(typeof(DefibrillatorComponent))]
public sealed class DefibrillatorTest : InteractionTest
{
    // We need two hands to use a defbrillator.
    protected override string PlayerPrototype => "MobHuman";

    private static readonly EntProtoId DefibrillatorProtoId = "Defibrillator";
    private static readonly EntProtoId TargetProtoId = "MobHuman";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageTypeId = "Blunt";

    /// <summary>
    /// Kills a target mob, heals them and then revives them with a defibrillator.
    /// </summary>
    [Test]
    public async Task KillAndReviveTest()
    {
        var damageableSystem = SEntMan.System<DamageableSystem>();
        var mobThresholdsSystem = SEntMan.System<MobThresholdSystem>();

        // Don't let the player and target suffocate.
        await AddAtmosphere();

        await SpawnTarget(TargetProtoId);

        var targetMobState = Comp<MobStateComponent>();
        var targetDamageable = Comp<DamageableComponent>();

        // Check that the target has no damage and is not crit or dead.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Alive), "Target mob was not alive when spawned.");
            Assert.That(targetDamageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero), "Target mob was damaged when spawned.");
        });

        // Get the damage needed to kill or crit the target.
        var critThreshold = mobThresholdsSystem.GetThresholdForState(STarget.Value, MobState.Critical);
        var deathThreshold = mobThresholdsSystem.GetThresholdForState(STarget.Value, MobState.Dead);
        var critDamage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), (critThreshold + deathThreshold) / 2);
        var deathDamage = new DamageSpecifier(ProtoMan.Index(BluntDamageTypeId), deathThreshold);

        // TSF edit start — for TSF brain death model, set brain damage instead of threshold damage
        var isTSF = SEntMan.TryGetComponent<Content.Shared._TSF.Organs.TSFOrganDamageComponent>(STarget.Value, out var organs);
        if (isTSF)
        {
            await Server.WaitPost(() =>
            {
                organs!.Brain = 1.0f; // irreversible brain death (>= 0.9)
                SEntMan.Dirty(STarget.Value, organs);
            });
        }
        else
        {
            // Fallback for non-TSF entities: kill via damage threshold
            await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, targetDamageable), deathDamage));
        }
        // TSF edit end
        await RunTicks(3);

        // Check that the target is dead.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob did not die from deadly damage amount.");
            // TSF edit — for TSF model, skip damage threshold check (death is from brain, not total damage)
            if (!isTSF)
                Assert.That(targetDamageable.TotalDamage, Is.EqualTo(deathThreshold), "Target mob had the wrong total damage amount after being killed.");
        });

        // Spawn a defib and activate it.
        var defib = await PlaceInHands(DefibrillatorProtoId, enableToggleable: true);
        var cooldown = Comp<DefibrillatorComponent>(defib).ZapDelay;

        // Wait for the cooldown.
        await RunSeconds((float)cooldown.TotalSeconds);

        // ZAP!
        await Interact();

        // Check that the target is still dead since it is over the crit threshold.
        // And it should have taken some extra damage.
        Assert.Multiple(() =>
        {
            Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob was revived despite being over the death damage threshold.");
            // TSF edit — skip damage check for TSF entities (death is from brain >= 0.9, not total damage)
            if (!isTSF)
                Assert.That(targetDamageable.TotalDamage, Is.GreaterThan(deathThreshold), "Target mob did not take damage from being defibrillated.");
        });

        // Set the damage halfway between the crit and death thresholds so that the target can be revived.
        // TSF edit start — for TSF, set brain to revivable window (0.7-0.9) and add shock to keep in crit
        if (isTSF)
        {
            await Server.WaitPost(() =>
            {
                organs!.Brain = 0.75f; // revivable window
                SEntMan.Dirty(STarget.Value, organs);
                // Add shock so ConsciousnessSystem keeps mob in Critical after defib
                Assert.That(SEntMan.TryGetComponent<Content.Shared._TSF.Pain.TSFPainComponent>(STarget.Value, out var pain), "TSFPainComponent missing on STarget; required to set Shock so revived mob remains Critical");
                pain.Shock = 80f; // high shock → low consciousness → stays Critical
                SEntMan.Dirty(STarget.Value, pain);
            });
        }
        else
        {
            await Server.WaitPost(() => damageableSystem.SetDamage((STarget.Value, targetDamageable), critDamage));
        }
        // TSF edit end
        await RunTicks(3);

        // Check that the target is still dead.
        Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Dead), "Target mob revived on its own.");

        // ZAP!
        await RunSeconds((float)cooldown.TotalSeconds);
        await Interact();

        // The target should be revived, but in crit.
        Assert.That(targetMobState.CurrentState, Is.EqualTo(MobState.Critical), "Target mob was not revived from being defibrillated.");
    }
}
