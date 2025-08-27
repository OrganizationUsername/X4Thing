using _40K.Core;
using System.Numerics;

namespace _40K.Simulation;

internal class Program
{
    private static void Main()
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
    private static UnitProfile IntercessorProfile => new() { Name = "Intercessor", Stats = new Statline { BallisticSkill = 3, Toughness = 4, WoundsPerModel = 2, Saves = new SaveProfile { Armor = 3, }, }, };
    private static UnitProfile GhostkeelProfile => new() { Name = "XV95 Ghostkeel", Stats = new Statline { BallisticSkill = 3, Toughness = 6, WoundsPerModel = 6, Saves = new SaveProfile { Armor = 3, Invulnerable = 5, }, }, BallisticSkillModifier = dist => dist > 6 ? -1 : 0, };

    // ---- Weapons ----
    private static Weapon BurstCannon() => new() { Name = "Burst Cannon", Profiles = { new WeaponProfile { Name = "Burst Cannon (Assault 4)", Range = 18, Strength = 5, Ap = 0, Shots = _ => 4, Damage = _ => 1, }, }, };
    private static Weapon FusionCollider() => new() { Name = "Fusion Collider", Profiles = { new WeaponProfile { Name = "Fusion Collider (Heavy D3)", Range = 18, Strength = 8, Ap = -4, Shots = d => d.D3(), Damage = d => d.D6(), }, }, };

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

    // ---- One volley result: number of dead Intercessors ----
    private static int VolleyKillsAtDistance(int distanceInches, IDice dice)
    {
        var ghost = GhostkeelAt(new Vector2(0, 0));
        var marines = TenIntercessorsAt(new Vector2(distanceInches, 0));
        var gModel = ghost.Models[0];

        _ = AttackService.FireAllWeapons(gModel, marines, dice);
        return marines.Models.Count(m => !m.IsAlive);
    }

    // ---- Public simulation entrypoint ----
    public static void Run(int distanceInches, int trials = 10_000)
    {
        var kills = new int[trials];
        for (var i = 0; i < trials; i++) { kills[i] = VolleyKillsAtDistance(distanceInches, new RandomDice()); }

        // Stats
        var avg = kills.Average();
        var min = kills.Min();
        var max = kills.Max();
        var p50 = Stats.Percentile(kills, 0.50);
        var p90 = Stats.Percentile(kills, 0.90);

        Console.WriteLine($"Ghostkeel volley vs 10 Intercessors at {distanceInches}\"");
        Console.WriteLine($"Trials: {trials:N0}");
        Console.WriteLine($"Average kills: {avg:F2} (min {min}, median {p50}, p90 {p90}, max {max})");

        // Histogram (0..10)
        var buckets = Enumerable.Range(0, 11).Select(k => (k, kills.Count(x => x == k))).ToList();
        Console.WriteLine("Histogram (kills : count, pct):");
        foreach (var (k, c) in buckets) { Console.WriteLine($"{k,2} : {c,6}  ({100.0 * c / trials:F1}%)"); }
    }
}