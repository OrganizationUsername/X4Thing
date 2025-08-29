using _40K.Core;

namespace _40K.Tests;

public class ShootingTests
{
    private static ModelProfile IntercessorProfile => new() { Name = "Intercessor", Stats = new Statline { BallisticSkill = 3, Toughness = 4, WoundsPerModel = 2, Saves = new SaveProfile { Armor = 3, }, }, };
    private static ModelProfile GhostkeelProfile => new()
    {
        Name = "Ghostkeel",
        Stats = new Statline { BallisticSkill = 3, Toughness = 6, WoundsPerModel = 6, Saves = new SaveProfile { Armor = 3, Invulnerable = 5, }, },
        Degrading = new DegradingTable { Brackets = { new DegradeBracket(6, 6, 12, 4, 3), new DegradeBracket(3, 5, 8, 5, 2), new DegradeBracket(1, 2, 4, 5, 1), }, },
    };

    private static Weapon BoltRifle() => new() { Name = "Bolt Rifle", Profiles = { new WeaponProfile { Name = "Bolt Rifle (Rapid Fire 1)", Range = 30, Strength = 4, Ap = -1, Shots = _ => 1, Damage = _ => 1, }, }, };
    private static Weapon FusionCollider() => new() { Name = "Fusion Collider", Profiles = { new WeaponProfile { Name = "Fusion Collider (Heavy D3)", Range = 18, Strength = 8, Ap = -4, Shots = d => d.D3(), Damage = d => d.D6(), }, }, };

    [Fact]
    public void BoltRifle_HasSingleProfile_AndCanBeFired()
    {
        var shooter = new Model(IntercessorProfile);
        shooter.Weapons.Add(BoltRifle());

        var target = new Unit { Name = "Target", };
        target.Models.Add(new Model(IntercessorProfile)); // T4, 2W, 3+ save

        // Scripted dice: Hit(4), Wound(4 vs T4 needs 4+), Save(2) -> 2 >= 4? no, so fail? Wait: save is roll >= required. 
        // Required save with Ap-1 vs 3+: 4+. If we roll 2, it fails -> damage 1.
        var dice = new ScriptedDice(d6: [4, 4, 2,]);  // hit=4, wound=4, save=2

        var profile = shooter.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 24, dice);

        Assert.Equal(1, unsaved);
        Assert.Equal(1, target.Models[0].RemainingWounds); // took 1 damage (2 -> 1)
    }

    [Fact]
    public void FusionCollider_SingleProfile_D3Shots_D6Damage_DoesDamage()
    {
        var shooter = new Model(GhostkeelProfile);
        shooter.Weapons.Add(FusionCollider());

        var target = new Unit { Name = "Marines", };
        target.Models.Add(new Model(IntercessorProfile)); // T4, 2W

        // Scripted:
        // D3 shots=3, Hit=5, Wound=3 (S8 vs T4 needs 2+), Save=3 (Ap-4 vs 3+ -> armor 7+; invuln? none on marine) => no save.
        // Damage D6=5 -> kills marine model outright (2W).
        var dice = new ScriptedDice(
            d6: [ /* shots don't use d6 */ 5, 3, /* save roll irrelevant (no save) */ 5,],
            d3: [3,] // Heavy D3 -> 3 shots
        );

        var profile = shooter.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 12, dice);

        Assert.True(unsaved >= 1);
        Assert.False(target.Models[0].IsAlive);
    }

    [Fact]
    public void OneModel_FromUnit_Shoots_OtherUnit_AllocatesToFirstAlive()
    {
        // Shooter unit with one model carrying a bolt rifle
        var shooters = new Unit { Name = "Intercessors", };
        var shooterModel = new Model(IntercessorProfile);
        shooterModel.Weapons.Add(BoltRifle());
        shooters.Models.Add(shooterModel);

        // Target unit: two marines
        var targets = new Unit { Name = "Defenders", };
        targets.Models.Add(new Model(IntercessorProfile));
        targets.Models.Add(new Model(IntercessorProfile));

        // Scripted: hit 6, wound 5 (vs T4 needs 4+), save 1 (fails), damage 1
        var dice = new ScriptedDice(d6: [6, 5, 1,]);

        var profile = shooterModel.Weapons[0].Profiles[0];
        var unsaved = AttackService.FireOneModelOneProfile(shooterModel, targets, profile, range: 24, dice);

        Assert.Equal(1, unsaved);
        Assert.Equal(1, targets.Models[0].RemainingWounds); // first model takes the wound
        Assert.Equal(2, targets.Models[1].RemainingWounds); // second untouched
    }
}