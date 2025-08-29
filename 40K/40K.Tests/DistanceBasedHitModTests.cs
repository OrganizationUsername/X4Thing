using System.Numerics;
using _40K.Core;

namespace _40K.Tests;

public class DistanceBasedHitModTests
{
    // Bolt rifle: Rapid Fire 1 (≤15" ⇒ 2 shots), S4, AP-1, D1
    private static Weapon BoltRifle() => new()
    {
        Name = "Bolt Rifle",
        Profiles =
        {
            new WeaponProfile
            {
                Name = "Bolt Rifle (Rapid Fire 1)",
                Range = 30,
                Strength = 4,
                Ap = -1,
                Shots = _ => 1,
                Damage = _ => 1,
                ShotsByDistance = (_, dist) => dist <= 15 ? 2 : 1,
            },
        },
    };

    // Intercessor data
    private static ModelProfile IntercessorProfile => new()
    {
        Name = "Intercessor",
        Stats = new Statline
        {
            BallisticSkill = 3,
            Toughness = 4,
            WoundsPerModel = 2,
            Saves = new SaveProfile { Armor = 3, },
        },
    };

    // “Ghostkeel-like” single target: T6, 6W, 3+/5++, and the distance-based -1 to be hit beyond 6"
    private static ModelProfile StealthyTargetProfile => new()
    {
        Name = "Stealthy Target",
        Stats = new Statline
        {
            BallisticSkill = 3,
            Toughness = 6,
            WoundsPerModel = 6,
            Saves = new SaveProfile { Armor = 3, Invulnerable = 5, },
        },
        // The new feature: function of distance ⇒ BS adjustment
        BallisticSkillModifier = dist => dist > 6 ? -1 : 0,
    };

    private static Model MarineAt(Vector2 pos)
    {
        var m = new Model(IntercessorProfile) { Position = pos, };
        m.Weapons.Add(BoltRifle());
        return m;
    }

    private static Unit TenIntercessorsAt(Vector2 pos)
    {
        var u = new Unit { Name = "Intercessors", };
        for (var i = 0; i < 10; i++) u.Models.Add(MarineAt(pos));
        return u;
    }

    private static Unit StealthySingleAt(Vector2 pos)
    {
        var u = new Unit { Name = "Stealthy Guy", };
        u.Models.Add(new Model(StealthyTargetProfile) { Position = pos, });
        return u;
    }

    [Fact]
    public void TenIntercessors_At5in_NoPenalty_3sHit_DealDamage()
    {
        // 10 Intercessors at (0,0); target at (5,0) ⇒ distance = 5" (no -1), ≤15" so Rapid Fire (2 shots each)
        var marines = TenIntercessorsAt(new Vector2(0, 0));
        var target = StealthySingleAt(new Vector2(5, 0));

        // Script a few early shots to push exactly 6 damage through:
        // For each scripted shot: Hit=3 (hits on 3+ at 5"), Wound=5 (S4 vs T6 needs 5+), Save=2 (fails vs 4+ after AP-1)
        // We script 6 such shots to KO the 6W target; remaining rolls default to 4s (which won't wound vs T6).
        var dice = new ScriptedDice(d6:
        [
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
        ]);

        //var unsaved = FireTenIntercessors(marines, target, dice);
        var unsaved = AttackService.FireUnitAllWeapons(marines, target, dice);

        Assert.True(unsaved >= 6);                   // at least 6 unsaved wounds
        Assert.False(target.Models[0].IsAlive);      // target should be dead (6W)
    }

    [Fact]
    public void TenIntercessors_At7in_SufferMinus1_3sNowMiss_LessDamage()
    {
        // Same setup but distance = 7" ⇒ target applies -1 to be hit; still within 15" (rapid fire = 2 shots each)
        var marines = TenIntercessorsAt(new Vector2(0, 0));
        var target = StealthySingleAt(new Vector2(7, 0));

        // Use the exact same scripted sequence as the 5" test:
        // Hit=3 now becomes 2 after -1 ⇒ misses; the rest default to 4s later which hit,
        // but wound=4 default fails vs T6 (needs 5+), so no meaningful damage lands.
        var dice = new ScriptedDice(d6:
        [
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
            3,5,2,
        ]);

        var unsaved = AttackService.FireUnitAllWeapons(marines, target, dice);

        Assert.True(unsaved <= 1);                  // should be zero; allow <= 1 in case of future tweaks
        Assert.True(target.Models[0].IsAlive);      // target survives (took far less than 6 damage)
        Assert.Equal(6, target.Models[0].RemainingWounds);
    }
}