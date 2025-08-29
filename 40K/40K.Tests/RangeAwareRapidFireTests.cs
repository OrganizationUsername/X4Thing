using System.Numerics;
using _40K.Core;

namespace _40K.Tests;

public class RangeAwareRapidFireTests
{
    private static Weapon BoltRifle() => new() { Name = "Bolt Rifle", Profiles = { new WeaponProfile { Name = "Bolt Rifle (Rapid Fire 1)", Range = 30, Strength = 4, Ap = -1, Shots = _ => 1, Damage = _ => 1, ShotsByDistance = (_, dist) => dist <= 15 ? 2 : 1, }, }, }; // ⟵ double shots at half range (≤ 15")
    private static ModelProfile IntercessorProfile => new() { Name = "Intercessor", Stats = new Statline { BallisticSkill = 3, Toughness = 4, WoundsPerModel = 2, Saves = new SaveProfile { Armor = 3, }, }, };
    private static Model MarineAt(Vector2 pos) => new(IntercessorProfile) { Position = pos, };

    [Fact]
    public void BoltRifle_DoublesShots_AtHalfRange_15inOrLess()
    {
        // shooter at (0,0) with bolt rifle
        var shooter = MarineAt(new Vector2(0, 0));
        shooter.Weapons.Add(BoltRifle());

        // target single marine at 12" away → within half (<=15")
        var target = new Unit { Name = "Target", };
        target.Models.Add(MarineAt(new Vector2(12, 0)));

        // Scripted rolls for 2 shots: 
        // shot1: hit 4, wound 4, save 2 (fails) → 1 dmg
        // shot2: hit 5, wound 4, save 5 (vs 4+ after AP-1; 5 succeeds) → 0 dmg
        var dice = new ScriptedDice(d6: [4, 4, 2, 5, 4, 2,]);

        var profile = shooter.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 0, dice);

        Assert.Equal(2, unsaved);
        Assert.Equal(0, target.Models[0].RemainingWounds);
    }

    [Fact]
    public void BoltRifle_OneShot_BeyondHalfRange_ButWithinMaxRange()
    {
        // shooter at (0,0)
        var shooter = MarineAt(new Vector2(0, 0));
        shooter.Weapons.Add(BoltRifle());

        // target at 24" → >15", <=30": only 1 shot
        var target = new Unit { Name = "Target", };
        target.Models.Add(MarineAt(new Vector2(24, 0)));

        // shot1: hit 4, wound 4, save 2 (fails) → 1 dmg
        // shot2: (not used)
        var dice = new ScriptedDice(d6: [4, 4, 2, 5, 4, 2,]);

        var profile = shooter.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 0, dice);

        Assert.Equal(1, unsaved);
        Assert.Equal(1, target.Models[0].RemainingWounds);
    }

    [Fact]
    public void BoltRifle_NoShots_IfOutOfRange()
    {
        var shooter = MarineAt(new Vector2(0, 0));
        shooter.Weapons.Add(BoltRifle());

        // target at 36" → beyond 30" range
        var target = new Unit { Name = "Too Far", };
        target.Models.Add(MarineAt(new Vector2(36, 0)));

        var dice = new ScriptedDice(); // won't be used

        var profile = shooter.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, 0, dice);

        Assert.Equal(0, unsaved);
        Assert.Equal(2, target.Models[0].RemainingWounds);
    }
}