using Content.Shared._TSF.Consciousness;
using Content.Shared._TSF.Organs;
using Content.Shared._TSF.Pain;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Server._TSF.Organs;

/// <summary>
/// Organ damage system. Routes incoming damage to organs based on damage type,
/// and applies ongoing organ failure effects (internal bleeding, airloss, pain, stun).
/// </summary>
public sealed class TSFOrganDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // How much of incoming damage is converted to organ damage (fraction).
    // Balanced so that ~150-200 piercing damage (a full magazine) causes lethal organ cascade.
    private const float DamageToOrganFraction = 0.012f;
    private const float PiercingOrganMultiplier = 2.0f;
    private const float SlashOrganMultiplier = 1.5f;
    private const float BluntOrganMultiplier = 0.7f;
    private const float BurnOrganMultiplier = 1.2f;

    // Internal bleeding rates (BleedAmount increase per tick)
    private const float HeartMildBleed = 0.3f;
    private const float HeartSevereBleed = 1.0f;
    private const float HeartStoppedBleed = 3.0f;
    private const float LiverMildBleed = 0.2f;
    private const float LiverSevereBleed = 0.6f;

    // Airloss damage per second from impaired lungs
    private const float LungImpairedAirloss = 0.5f;
    private const float LungCriticalAirloss = 1.5f;

    // Natural organ healing (very slow: ~30 min for full heal)
    private const float NaturalHealRate = 0.00055f;

    // Time tracking for periodic effects
    private float _effectAccumulator;
    private const float EffectInterval = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TSFOrganDamageComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TSFOrganDamageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TSFOrganDamageComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<DamageableComponent, ComponentStartup>(OnDamageableStartup);
    }

    /// <summary>
    /// Admin Rejuvenate: reset all organ damage, pain, shock to zero.
    /// </summary>
    private void OnRejuvenate(EntityUid uid, TSFOrganDamageComponent organs, RejuvenateEvent args)
    {
        organs.Brain = 0f;
        organs.Heart = 0f;
        organs.LungLeft = 0f;
        organs.LungRight = 0f;
        organs.Liver = 0f;
        organs.Stomach = 0f;
        organs.Intestines = 0f;
        organs.Trachea = 0f;
        organs.Eyes = 0f;
        organs.HeadAmputated = false;
        organs.HeartStopped = false;
        Dirty(uid, organs);

        if (TryComp<TSFPainComponent>(uid, out var pain))
        {
            pain.Pain = 0f;
            pain.Shock = 0f;
            pain.AdrenalineFactor = 0f;
            pain.PainkillerStrength = 0f;
            Dirty(uid, pain);
        }

        if (TryComp<ConsciousnessComponent>(uid, out var consciousness))
        {
            consciousness.Level = 1f;
            consciousness.Unconscious = false;
            Dirty(uid, consciousness);
        }
    }

    private void OnDamageableStartup(EntityUid uid, DamageableComponent comp, ComponentStartup args)
    {
        if (HasComp<MobStateComponent>(uid) && !HasComp<TSFOrganDamageComponent>(uid))
            EnsureComp<TSFOrganDamageComponent>(uid);
    }

    private void OnStartup(EntityUid uid, TSFOrganDamageComponent comp, ComponentStartup args)
    {
        EnsureComp<TSFPainComponent>(uid);
        EnsureComp<ConsciousnessComponent>(uid);
    }

    private void OnDamageChanged(EntityUid uid, TSFOrganDamageComponent organs, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (_mobState.IsDead(uid))
            return;

        var delta = args.DamageDelta;
        var painDelta = 0f;

        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            var rawDamage = amount.Float();
            var typeStr = type.ToString();

            var multiplier = typeStr switch
            {
                "Piercing" => PiercingOrganMultiplier,
                "Slash" => SlashOrganMultiplier,
                "Blunt" => BluntOrganMultiplier,
                "Heat" => BurnOrganMultiplier,
                "Cold" => BurnOrganMultiplier * 0.5f,
                "Caustic" => 1.0f,
                _ => 0f
            };

            if (multiplier <= 0f)
            {
                if (typeStr == "Asphyxiation")
                {
                    var lungDmg = rawDamage * DamageToOrganFraction * 1.5f;
                    if (_random.Prob(0.5f))
                        organs.LungLeft = Math.Min(organs.LungLeft + lungDmg, 1f);
                    else
                        organs.LungRight = Math.Min(organs.LungRight + lungDmg, 1f);
                    organs.Trachea = Math.Min(organs.Trachea + lungDmg * 0.3f, 1f);
                }
                else if (typeStr == "Poison")
                {
                    organs.Liver = Math.Min(organs.Liver + rawDamage * DamageToOrganFraction * 2f, 1f);
                }
                // Pain from special damage types too
                painDelta += rawDamage * GetPainMultiplier(typeStr);
                continue;
            }

            var organDmg = rawDamage * DamageToOrganFraction * multiplier;
            DistributeOrganDamage(organs, organDmg, typeStr);
            painDelta += rawDamage * GetPainMultiplier(typeStr);
        }

        if (painDelta > 0f && TryComp<TSFPainComponent>(uid, out var pain))
        {
            var effectivePain = painDelta;
            if (pain.PainkillerStrength > 0f)
                effectivePain *= 1f - pain.PainkillerStrength;
            else if (pain.AdrenalineFactor > 0f)
                effectivePain *= 1f - pain.AdrenalineFactor;

            pain.Pain = Math.Min(pain.Pain + effectivePain, pain.MaxPain);
            Dirty(uid, pain);
        }

        Dirty(uid, organs);
    }

    /// <summary>
    /// Distributes organ damage across 2-3 organs per hit (primary + splash).
    /// Primary organ gets 60% of damage, 1-2 neighbors get the rest.
    /// </summary>
    private void DistributeOrganDamage(TSFOrganDamageComponent organs, float damage, string damageType)
    {
        const float primaryFraction = 0.6f;
        const float splashFraction = 0.4f;

        var primaryDmg = damage * primaryFraction;
        var splashDmg = damage * splashFraction;

        // Pick primary organ
        var roll = _random.NextFloat();
        ApplyToOrgan(organs, roll, primaryDmg, damageType);

        // 1-2 splash hits to other organs
        var splashCount = _random.Prob(0.5f) ? 2 : 1;
        var perSplash = splashDmg / splashCount;
        for (var i = 0; i < splashCount; i++)
        {
            var splashRoll = _random.NextFloat();
            ApplyToOrgan(organs, splashRoll, perSplash, damageType);
        }
    }

    private static void ApplyToOrgan(TSFOrganDamageComponent organs, float roll, float damage, string damageType)
    {
        if (roll < 0.12f)
            organs.Heart = Math.Min(organs.Heart + damage, 1f);
        else if (roll < 0.23f)
            organs.LungLeft = Math.Min(organs.LungLeft + damage, 1f);
        else if (roll < 0.34f)
            organs.LungRight = Math.Min(organs.LungRight + damage, 1f);
        else if (roll < 0.49f)
            organs.Liver = Math.Min(organs.Liver + damage, 1f);
        else if (roll < 0.59f)
            organs.Stomach = Math.Min(organs.Stomach + damage, 1f);
        else if (roll < 0.71f)
            organs.Intestines = Math.Min(organs.Intestines + damage, 1f);
        else if (roll < 0.81f)
        {
            var brainDmg = damageType == "Blunt" ? damage * 0.5f : damage;
            organs.Brain = Math.Min(organs.Brain + brainDmg, 1f);
        }
        else if (roll < 0.86f)
            organs.Eyes = Math.Min(organs.Eyes + damage, 1f);
        else if (roll < 0.92f)
            organs.Trachea = Math.Min(organs.Trachea + damage, 1f);
        // 0.92-1.0: miss (bone/muscle absorbs, no organ hit)
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _effectAccumulator += frameTime;
        if (_effectAccumulator < EffectInterval)
            return;

        var dt = _effectAccumulator;
        _effectAccumulator = 0f;

        var query = EntityQueryEnumerator<TSFOrganDamageComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var organs, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            ApplyOrganEffects(uid, organs, dt);
            ApplyNaturalHealing(uid, organs, dt);
        }
    }

    private void ApplyOrganEffects(EntityUid uid, TSFOrganDamageComponent organs, float dt)
    {
        // ── HEART effects → internal bleeding via BloodstreamSystem API ──
        if (organs.HeartStopped)
        {
            _bloodstream.TryModifyBleedAmount((uid, null), HeartStoppedBleed * dt);
        }
        else if (organs.Heart >= organs.HeartCriticalThreshold)
        {
            _bloodstream.TryModifyBleedAmount((uid, null), HeartSevereBleed * dt);
            if (TryComp<TSFPainComponent>(uid, out var painComp))
            {
                painComp.Shock = Math.Min(painComp.Shock + 2f * dt, painComp.MaxShock);
                Dirty(uid, painComp);
            }
        }
        else if (organs.Heart >= organs.HeartBleedThreshold)
        {
            _bloodstream.TryModifyBleedAmount((uid, null), HeartMildBleed * dt);
        }

        // ── LIVER effects ──
        if (organs.Liver >= organs.LiverCriticalThreshold)
        {
            _bloodstream.TryModifyBleedAmount((uid, null), LiverSevereBleed * dt);

            if (_random.Prob(0.05f * dt))
                _stun.TryKnockdown((uid, null), TimeSpan.FromSeconds(2));

            if (TryComp<TSFPainComponent>(uid, out var painComp))
            {
                painComp.Pain = Math.Min(painComp.Pain + organs.LiverPainPerTenth * dt, painComp.MaxPain);
                Dirty(uid, painComp);
            }
        }
        else if (organs.Liver >= organs.LiverBleedThreshold)
        {
            _bloodstream.TryModifyBleedAmount((uid, null), LiverMildBleed * dt);
        }

        // ── STOMACH / INTESTINES effects ──
        if (organs.Stomach >= 0.5f)
            _bloodstream.TryModifyBleedAmount((uid, null), 0.1f * dt);

        if (organs.Intestines >= 0.5f)
            _bloodstream.TryModifyBleedAmount((uid, null), 0.1f * dt);

        // ── LUNG effects → Asphyxiation damage ──
        var lungFunction = GetLungFunction(organs);
        if (lungFunction < 1f)
        {
            var airlossRate = lungFunction < 0.3f ? LungCriticalAirloss : LungImpairedAirloss;
            var airloss = airlossRate * (1f - lungFunction) * dt;
            _damageable.TryChangeDamage(uid, new DamageSpecifier
            {
                DamageDict = { ["Asphyxiation"] = FixedPoint2.New(airloss) }
            }, origin: uid);

        }

        // ── BRAIN effects (below death threshold) ──
        if (organs.Brain >= organs.BrainDisorientThreshold && organs.Brain < organs.BrainDeathThreshold)
        {
            if (_random.Prob(0.03f * dt))
                _stun.TryKnockdown((uid, null), TimeSpan.FromSeconds(1.5));
        }

        // ── TRACHEA effects ──
        if (organs.Trachea >= 0.5f)
        {
            var tracheaAirloss = (organs.Trachea - 0.5f) * 2f * dt;
            _damageable.TryChangeDamage(uid, new DamageSpecifier
            {
                DamageDict = { ["Asphyxiation"] = FixedPoint2.New(tracheaAirloss) }
            }, origin: uid);

        }
    }

    private void ApplyNaturalHealing(EntityUid uid, TSFOrganDamageComponent organs, float dt)
    {
        if (organs.Brain >= 0.4f)
            return;

        var heal = NaturalHealRate * dt;
        var changed = false;

        if (organs.Heart > 0f) { organs.Heart = Math.Max(organs.Heart - heal, 0f); changed = true; }
        if (organs.LungLeft > 0f) { organs.LungLeft = Math.Max(organs.LungLeft - heal, 0f); changed = true; }
        if (organs.LungRight > 0f) { organs.LungRight = Math.Max(organs.LungRight - heal, 0f); changed = true; }
        if (organs.Liver > 0f) { organs.Liver = Math.Max(organs.Liver - heal, 0f); changed = true; }
        if (organs.Stomach > 0f) { organs.Stomach = Math.Max(organs.Stomach - heal, 0f); changed = true; }
        if (organs.Intestines > 0f) { organs.Intestines = Math.Max(organs.Intestines - heal, 0f); changed = true; }
        if (organs.Trachea > 0f) { organs.Trachea = Math.Max(organs.Trachea - heal, 0f); changed = true; }
        if (organs.Eyes > 0f) { organs.Eyes = Math.Max(organs.Eyes - heal, 0f); changed = true; }

        if (changed)
            Dirty(uid, organs);
    }

    private static float GetLungFunction(TSFOrganDamageComponent organs)
    {
        var left = Math.Max(1f - organs.LungLeft, 0f);
        var right = Math.Max(1f - organs.LungRight, 0f);
        return (left + right) * 0.5f;
    }

    private static float GetPainMultiplier(string damageType)
    {
        return damageType switch
        {
            "Brute" => 1.0f,
            "Burn" or "Heat" => 1.2f,
            "Slash" => 1.1f,
            "Piercing" => 1.0f,
            "Blunt" => 0.9f,
            "Caustic" => 1.0f,
            "Poison" => 0.8f,
            "Asphyxiation" => 0.7f,
            "Bloodloss" => 0.8f,
            "Cold" => 0.6f,
            _ => 0.5f,
        };
    }
}
