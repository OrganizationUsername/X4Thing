using _40K.Core;
using System.Numerics;

namespace _40K.Simulation;

internal class Program
{
    static void Main()
    {
        GhostkeelVolleySim.Run(distanceInches: 12, trials: 20_000);
        Console.WriteLine();
        GhostkeelVolleySim.Run(distanceInches: 18, trials: 20_000);
        Console.ReadLine();
    }
}

public static class GhostkeelVolleySim
{
    // ---- Profiles ----
    private static UnitProfile IntercessorProfile => new()
    {
        Name = "Intercessor",
        Stats = new Statline { BallisticSkill = 3, Toughness = 4, WoundsPerModel = 2, Saves = new SaveProfile { Armor = 3, }, },
    };

    private static UnitProfile GhostkeelProfile => new()
    {
        Name = "XV95 Ghostkeel",
        Stats = new Statline { BallisticSkill = 3, Toughness = 6, WoundsPerModel = 6, Saves = new SaveProfile { Armor = 3, Invulnerable = 5, }, },
    };

    // ---- Weapons ----
    private static Weapon BurstCannon()
    {
        var p = new WeaponProfile
        {
            Name = "Burst Cannon (Assault 4)",
            Range = 18,
            Strength = 5,
            Ap = 0,
            Shots = _ => 4,
            Damage = _ => 1,
        };
        return new Weapon { Name = "Burst Cannon", Profiles = { p, }, };
    }

    private static Weapon FusionCollider()
    {
        var p = new WeaponProfile
        {
            Name = "Fusion Collider (Heavy D3)",
            Range = 18,
            Strength = 8,
            Ap = -4,
            Shots = d => d.D3(),
            Damage = d => d.D6(),
        };
        return new Weapon { Name = "Fusion Collider", Profiles = { p, }, };
    }

    // ---- Builders ----
    private static Unit TenIntercessorsAt(Vector2 pos)
    {
        var u = new Unit { Name = "Intercessors", };
        for (var i = 0; i < 10; i++) { u.Models.Add(new Model(IntercessorProfile) { Position = pos, }); }
        return u;
    }

    private static Unit GhostkeelAt(Vector2 pos)
    {
        var u = new Unit { Name = "Ghostkeel", };
        var m = new Model(GhostkeelProfile) { Position = pos, };
        m.Weapons.Add(BurstCannon());   // 2x Burst
        m.Weapons.Add(BurstCannon());
        m.Weapons.Add(FusionCollider()); // + Collider
        u.Models.Add(m);
        return u;
    }

    // ---- Fire all Ghostkeel weapons once into target ----
    private static int FireAllGhostkeelWeapons(Model ghost, Unit target, IDice dice)
    {
        var totalUnsaved = 0;
        foreach (var w in ghost.Weapons)
        {
            // choose the first (only) profile for each weapon
            var p = w.Profiles[0];
            totalUnsaved += AttackService.FireOneModelOneProfile(ghost, target, p, range: 0, dice, hitMod: 0);
        }
        return totalUnsaved;
    }

    // ---- One volley result: number of dead Intercessors ----
    private static int VolleyKillsAtDistance(int distanceInches, IDice dice)
    {
        var ghost = GhostkeelAt(new Vector2(0, 0));
        var marines = TenIntercessorsAt(new Vector2(distanceInches, 0));

        var gModel = ghost.Models[0];

        // Fire *each* weapon once (Burst, Burst, Collider)
        _ = FireAllGhostkeelWeapons(gModel, marines, dice);

        // Count models destroyed
        return marines.Models.Count(m => !m.IsAlive);
    }

    // ---- Public simulation entrypoint ----
    public static void Run(int distanceInches, int trials = 10_000)
    {
        var kills = new int[trials];
        for (var i = 0; i < trials; i++)
        {
            kills[i] = VolleyKillsAtDistance(distanceInches, new RandomDice());
        }

        // Stats
        var avg = kills.Average();
        var min = kills.Min();
        var max = kills.Max();
        var p50 = Percentile(kills, 0.50);
        var p90 = Percentile(kills, 0.90);

        Console.WriteLine($"Ghostkeel volley vs 10 Intercessors at {distanceInches}\"");
        Console.WriteLine($"Trials: {trials:N0}");
        Console.WriteLine($"Average kills: {avg:F2} (min {min}, median {p50}, p90 {p90}, max {max})");

        // Histogram (0..10)
        var buckets = Enumerable.Range(0, 11).Select(k => (k, kills.Count(x => x == k))).ToList();
        Console.WriteLine("Histogram (kills : count, pct):");
        foreach (var (k, c) in buckets) { Console.WriteLine($"{k,2} : {c,6}  ({(100.0 * c / trials):F1}%)"); }
    }

    private static int Percentile(int[] data, double p)
    {
        var sorted = data.OrderBy(x => x).ToArray();
        var i = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(i);
        var hi = (int)Math.Ceiling(i);
        if (lo == hi) { return sorted[lo]; }
        var frac = i - lo;
        return (int)Math.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * frac);
    }
}