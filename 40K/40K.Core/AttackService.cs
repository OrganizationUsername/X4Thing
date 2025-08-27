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

    public static int FireOneModelOneProfile(Model attacker, Unit target, WeaponProfile profile, int range, IDice dice, int hitMod = 0)
    {
        var distance = NearestDistanceInches(attacker, target);
        if (distance > profile.Range) return 0;

        // Add defender’s modifier from its profile
        var defenderMod = target.Alive
            .Select(m => m.Profile.BallisticSkillModifier(distance))
            .DefaultIfEmpty(0)
            .Min(); // or Max/aggregate, depending on stacking rules

        var effectiveHitMod = hitMod + defenderMod;

        var unsavedWounds = 0;
        var shots = profile.GetShots(dice, distance);

        for (var i = 0; i < shots; i++)
        {
            if (!Resolver.Hits(attacker.Profile.Stats.BallisticSkill, dice, effectiveHitMod)) continue;
            var victim = target.Alive.FirstOrDefault();
            if (victim is null) break;

            if (!Resolver.Wounds(profile.Strength, victim.Profile.Stats.Toughness, dice)) continue;

            var saved = Resolver.Saved(profile.Ap, victim.Profile.Stats.Saves.Armor, victim.Profile.Stats.Saves.Invulnerable, dice);
            if (saved) continue;

            var dmg = profile.Damage(dice);
            victim.ApplyDamage(dmg);
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
    public static bool Hits(int ballisticSkill, IDice dice, int hitModifier = 0)
    {
        var roll = dice.D6();
        if (roll == 1) { return false; }
        return roll + hitModifier >= ballisticSkill;
    }

    public static bool Wounds(int strength, int toughness, IDice dice, int woundModifier = 0)
    {
        var required = RequiredToWound(strength, toughness);

        var roll = dice.D6();
        if (roll == 1) { return false; }
        return roll + woundModifier >= required;
    }

    private static int RequiredToWound(int s, int t)
    {
        if (s >= t * 2) { return 2; }
        if (s > t) { return 3; }
        if (s == t) { return 4; }
        return s * 2 <= t ? 6 : 5;
    }

    public static bool Saved(int ap, int? armorSave, int? invulnerableSave, IDice dice)
    {
        var armorRequired = armorSave - ap; // Ap worsens save
        var specialSaveRequired = invulnerableSave;

        var chosen = armorRequired.HasValue && specialSaveRequired.HasValue ? Math.Min(armorRequired.Value, specialSaveRequired.Value) : armorRequired ?? specialSaveRequired;

        if (!chosen.HasValue) return false;

        var required = Math.Clamp(chosen.Value, 2, 6);
        var roll = dice.D6();
        if (roll == 1) { return false; }
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
    public SaveProfile Saves { get; init; } = new();
}

public sealed class UnitProfile
{
    //ToDo: The next feature to add is having different stats as units take damage (e.g., ballistic skill goes down as wounds are taken)
    public required string Name { get; init; }
    public Statline Stats { get; init; } = new();
    public Func<int, int> BallisticSkillModifier { get; init; } = _ => 0; //For example: XV95 Ghostkeel BattleSuit has -1 to hit if target is >6" away
}

// One weapon can have many profiles; you must select one to fire.
public sealed class WeaponProfile
{
    //ToDo: Later on, we'll end up passing in some strategy to select which profile to use (e.g: for multimode weapons)
    public required string Name { get; init; }
    public int Range { get; init; }
    public int Strength { get; init; }
    public int Ap { get; init; } // 0, -1, -2, etc.
    public Func<IDice, int> Shots { get; init; } = _ => 1;    // e.g., () => 1, or d => d.D3()
    public Func<IDice, int> Damage { get; init; } = _ => 1;   // e.g., () => 1, or d => d.D6()
    public int GetShots(IDice dice, int distanceInches) => ShotsByDistance?.Invoke(dice, distanceInches) ?? Shots(dice);
    public Func<IDice, int, int>? ShotsByDistance { get; init; }
}

public sealed class Weapon
{
    public required string Name { get; init; }
    public List<WeaponProfile> Profiles { get; } = [];
}

public sealed class Model(UnitProfile profile)
{
    public UnitProfile Profile { get; init; } = profile;
    public Vector2 Position { get; set; }
    public List<Weapon> Weapons { get; } = [];
    public int RemainingWounds { get; private set; } = profile.Stats.WoundsPerModel;
    public bool IsAlive => RemainingWounds > 0;

    public void ApplyDamage(int dmg) => RemainingWounds = Math.Max(0, RemainingWounds - Math.Max(0, dmg));
}

public sealed class Unit
{
    public required string Name { get; init; }
    public List<Model> Models { get; } = [];
    public IEnumerable<Model> Alive => Models.Where(m => m.IsAlive);
}