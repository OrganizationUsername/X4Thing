using System.Numerics;

namespace _40K.Core;

//10 unit intercessor squad with 10 bolter rifles, 3+BS, 2W, 3+ save, no invuln
/*
 Bolt Rifle: 30" Rapid Fire 1, S4, Ap-1, 1 damage
*/

//XV95 Ghostkeel battlesuit with 2 fusion blasters, 3+BS, 6W, 3+ save, 5+ invuln
/*
 Fusion collider: 18", Heavy D3, S8, Ap-4, D6 damage
(2) burst cannon: 18" Assault 4, S5, AP0, 1 damage
//ignore the stealth drones for now
//If they are more than 6" away, enemy subtracts 1 from hit rolls
*/

public static class AttackService
{
    private static int NearestDistanceInches(Model attacker, Unit target)
    {
        var a = attacker.Position;
        var nearest = target.Alive
            .Select(m => Vector2.Distance(a, m.Position))
            .DefaultIfEmpty(float.PositiveInfinity)
            .Min();
        return (int)MathF.Floor(nearest);
    }

    public static int FireOneModelOneProfile(Model attacker, Unit target, WeaponProfile profile, int range, IDice dice, int hitMod = 0, ICombatLogSink? log = null)
    {
        var distance = NearestDistanceInches(attacker, target);
        if (distance > profile.Range) { return 0; }

        // Add defender’s modifier from its profile
        var defenderMod = target.Alive.Select(m => m.Profile.BallisticSkillModifier(distance)).DefaultIfEmpty(0).Min(); // or Max/aggregate, depending on stacking rules

        var effectiveHitMod = hitMod + defenderMod;

        var unsavedWounds = 0;
        var targetCount = target.Alive.Count();
        var context = new ShotContext(distance, targetCount);
        var shots = profile.GetShots(dice, context, log);
        log?.Write(new CombatEvent(CombatEventType.ShotStart, attacker.Profile.Name, profile.Name, target.Name, distance, shots));

        for (var i = 0; i < shots; i++)
        {
            if (!Resolver.Hits(attacker.CurrentBs(), dice, effectiveHitMod, log)) { continue; }
            var victim = target.Alive.FirstOrDefault(); //ToDo: Eventually, we will want to select targets more intelligently
            if (victim is null) { break; }

            if (!Resolver.Wounds(profile.Strength, victim.Profile.Stats.Toughness, dice, 0, log)) { continue; } //ToDo: wound modifier from other sources

            var saved = Resolver.Saved(profile.Ap, victim.Profile.Stats.Saves.Armor, victim.Profile.Stats.Saves.Invulnerable, dice, log);
            if (saved) { continue; }

            var dmg = profile.Damage(dice);
            log?.Write(new CombatEvent(CombatEventType.DamageRoll, attacker.Profile.Name, profile.Name, victim.Profile.Name, distance, null, null, null, null, dmg));
            victim.ApplyDamage(dmg, log);

            unsavedWounds++;
        }
        return unsavedWounds;
    }

    public static int FireAllWeapons(Model attacker, Unit target, IDice dice, int hitMod = 0, Func<Weapon, WeaponProfile>? selectProfile = null)
    {
        selectProfile ??= w => w.Profiles[0]; // default: first profile

        var total = 0;
        foreach (var w in attacker.Weapons)
        {
            var p = selectProfile(w);
            total += FireOneModelOneProfile(attacker, target, p, range: 0, dice, hitMod);
        }
        return total;
    }

    public static int FireUnitAllWeapons(Unit shooters, Unit target, IDice dice, int hitMod = 0, Func<Weapon, WeaponProfile>? selectProfile = null)
    {
        var total = 0;
        foreach (var m in shooters.Alive) { total += FireAllWeapons(m, target, dice, hitMod, selectProfile); }
        return total;
    }
}

public static class Resolver
{
    public static bool Hits(int ballisticSkill, IDice dice, int hitModifier, ICombatLogSink? log)
    {
        var roll = dice.D6();
        if (roll == 1) { return false; }
        log?.Write(new CombatEvent(CombatEventType.HitRoll, null, null, null, null, roll, ballisticSkill, hitModifier, roll + hitModifier >= ballisticSkill));
        return roll + hitModifier >= ballisticSkill;
    }

    public static bool Wounds(int strength, int toughness, IDice dice, int woundModifier, ICombatLogSink? log)
    {
        var required = RequiredToWound(strength, toughness);

        var roll = dice.D6();
        if (roll == 1) { return false; }
        log?.Write(new CombatEvent(CombatEventType.WoundRoll, null, null, null, null, roll, required, woundModifier, roll + woundModifier >= required));
        return roll + woundModifier >= required;
    }

    private static int RequiredToWound(int s, int t)
    {
        if (s >= t * 2) { return 2; }
        if (s > t) { return 3; }
        if (s == t) { return 4; }
        return s * 2 <= t ? 6 : 5;
    }

    public static bool Saved(int ap, int? armorSave, int? invulnerableSave, IDice dice, ICombatLogSink? log)
    {
        var armorRequired = armorSave - ap; // Ap worsens save
        var specialSaveRequired = invulnerableSave;

        var chosen = armorRequired.HasValue && specialSaveRequired.HasValue ? Math.Min(armorRequired.Value, specialSaveRequired.Value) : armorRequired ?? specialSaveRequired;

        switch (chosen)
        {
            case null: log?.Write(new CombatEvent(CombatEventType.SaveRoll, null, null, null)); return false;
            case > 6: log?.Write(new CombatEvent(CombatEventType.SaveRoll, null, null, null, Required: chosen.Value, Passed: false)); return false;
        }

        var required = Math.Max(2, chosen.Value);
        var roll = dice.D6(); //In the future, I might have to support rerolls or rolls with advantage
        if (roll == 1) { log?.Write(new CombatEvent(CombatEventType.SaveRoll, null, null, null, null, roll, required, null, false)); return false; }
        log?.Write(new CombatEvent(CombatEventType.SaveRoll, null, null, null, null, roll, required, null, roll >= required));
        return roll >= required;
    }
}

public sealed class SaveProfile
{
    public int Armor { get; init; }
    public int? Invulnerable { get; init; }
}

public sealed class Statline
{
    public int BallisticSkill { get; init; }
    public int Toughness { get; init; }
    public int WoundsPerModel { get; init; }
    public required SaveProfile Saves { get; init; }
}

public sealed record DegradeBracket(int MinWounds, int MaxWounds, int Move, int Bs, int Attacks);

public sealed class DegradingTable
{
    public List<DegradeBracket> Brackets { get; init; } = [];
    public DegradeBracket ForWounds(int w) => Brackets.First(b => w >= b.MinWounds && w <= b.MaxWounds);
}
public sealed class ModelProfile
{
    public required string Name { get; init; }
    public required Statline Stats { get; init; }
    public Func<int, int> BallisticSkillModifier { get; init; } = _ => 0;
    public DegradingTable? Degrading { get; init; }
}

public sealed class Model(ModelProfile profile)
{
    public ModelProfile Profile { get; init; } = profile;
    public Vector2 Position { get; set; }
    public List<Weapon> Weapons { get; } = [];
    public int RemainingWounds { get; private set; } = profile.Stats.WoundsPerModel;
    public bool IsAlive => RemainingWounds > 0;

    public DegradeBracket? CurrentBracket() => Profile.Degrading?.ForWounds(RemainingWounds);
    public int CurrentBs() => CurrentBracket()?.Bs ?? Profile.Stats.BallisticSkill;
    public int CurrentMove() => CurrentBracket()?.Move ?? 0;
    public int CurrentAttacks() => CurrentBracket()?.Attacks ?? 1;

    public void ApplyDamage(int dmg, ICombatLogSink? log)
    {
        RemainingWounds = Math.Max(0, RemainingWounds - Math.Max(0, dmg));
        log?.Write(new CombatEvent(CombatEventType.HealthRemaining, null, null, Profile.Name, null, null, null, null, null, RemainingWounds));
    }
}

public sealed class WeaponProfile
{
    //ToDo: Later on, we'll end up passing in some strategy to select which profile to use (e.g: for multimode weapons)
    public required string Name { get; init; }
    public int Range { get; init; }
    public int Strength { get; init; }
    public int Ap { get; init; } = 0; // 0, -1, -2, etc.
    public bool IsBlast { get; init; } = false;
    public Func<IDice, int> Shots { get; init; } = _ => 1;    // e.g., () => 1, or d => d.D3()
    public Func<IDice, int> Damage { get; init; } = _ => 1;   // e.g., () => 1, or d => d.D6()
    public int GetShots(IDice dice, ShotContext ctx, ICombatLogSink? log)
    {
        var baseShots = ShotsByDistance?.Invoke(dice, ctx.DistanceInches) ?? Shots(dice);
        if (IsBlast)
        {
            var adjusted = baseShots + ctx.TargetModels / 5;
            log?.Write(new CombatEvent(CombatEventType.BlastShotCount, null, Name, null, ctx.TargetModels, null, adjusted, null, null, null));
            return adjusted;
        }

        log?.Write(new CombatEvent(CombatEventType.DistanceCountShot, null, Name, null, ctx.DistanceInches, null, baseShots, null, null, null));
        return ShotsByDistance?.Invoke(dice, ctx.DistanceInches) ?? Shots(dice);
    }

    public Func<IDice, int, int?>? ShotsByDistance { get; init; }
}

public readonly record struct ShotContext(int DistanceInches, int TargetModels);

public sealed class Weapon
{
    public required string Name { get; init; }
    public List<WeaponProfile> Profiles { get; } = [];
}

public sealed class Unit
{
    public required string Name { get; init; }
    public List<Model> Models { get; } = [];
    public IEnumerable<Model> Alive => Models.Where(m => m.IsAlive);
}