using _40K.Core;

namespace _40K.Tests;

public sealed class WoundSaveTests
{
    private static ModelProfile MarineWithInvuln6AndArmor3 => new()
    {
        Name = "Target",
        Stats = new Statline
        {
            BallisticSkill = 3,
            Toughness = 4,
            WoundsPerModel = 2,
            Saves = new SaveProfile { Armor = 3, Invulnerable = 6, },
        },
    };

    private static Unit MakeSingleTarget()
    {
        var unit = new Unit { Name = "Defenders", };
        unit.Models.Add(new Model(MarineWithInvuln6AndArmor3));
        return unit;
    }

    private static WeaponProfile ApFiveWeapon => new() { Name = "Test Gun (AP-5)", Range = 24, Strength = 4, Ap = -5, Shots = _ => 1, Damage = _ => 1, };
    private static WeaponProfile ApZeroWeapon => new() { Name = "Test Gun (AP0)", Range = 24, Strength = 4, Ap = 0, Shots = _ => 1, Damage = _ => 1, };

    private static Model SimpleShooter() => new(new ModelProfile
    {
        Name = "Shooter",
        Stats = new Statline
        {
            BallisticSkill = 3,
            Toughness = 4,
            WoundsPerModel = 2,
            Saves = new SaveProfile { Armor = 3, },
        },
    });

    [Theory]
    [InlineData(2, 1)] // save roll 2: fails (needs 3+ from armor), take 1 damage
    [InlineData(3, 0)] // save roll 3: succeeds via armor 3+, take 0 damage
    public void APZero_PrefersArmor3_OverInvuln6(int saveRoll, int expectedDamageTaken)
    {
        // Arrange: one shooter with a single-profile AP0 weapon
        var shooter = SimpleShooter();
        var weapon = new Weapon { Name = "AP0 Test", };
        weapon.Profiles.Add(ApZeroWeapon);
        shooter.Weapons.Add(weapon);

        // Target unit with one model: 3+ armor, 6++ invuln (2W, T4)
        var target = MakeSingleTarget();
        var victim = target.Models[0];

        // Scripted dice sequence (D6): Hit, Wound, Save
        // Hit: 4 (>=3) -> hit; Wound: 4 (S4 vs T4 needs 4+) -> wound; Save: variable
        var dice = new ScriptedDice(d6: [4, 4, saveRoll,]);

        var profile = shooter.Weapons[0].Profiles[0];

        // Act
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 12, dice);

        // Assert
        // With AP0: armor = 3 - 0 = 3+, invuln = 6+ -> choose 3+.
        Assert.Equal(expectedDamageTaken > 0 ? 1 : 0, unsaved);
        Assert.Equal(2 - expectedDamageTaken, victim.RemainingWounds);
    }

    [Theory]
    [InlineData(5, 1)] // save roll 5: fails (needs 6+ after picking invuln), take 1 damage
    [InlineData(6, 0)] // save roll 6: succeeds, take 0 damage
    public void APMinus5_ForcesUseOfInvuln6_OverArmor3(int saveRoll, int expectedDamageTaken)
    {
        // Arrange: one shooter with a single-profile weapon
        var shooter = SimpleShooter();
        var weapon = new Weapon { Name = "AP-5 Test", };
        weapon.Profiles.Add(ApFiveWeapon);
        shooter.Weapons.Add(weapon);

        // Target unit with one model: 3+ armor, 6++ invuln (2W, T4)
        var target = MakeSingleTarget();
        var victim = target.Models[0];

        // Scripted dice sequence (D6): Hit, Wound, Save
        // Hit: 4 (>= 3) -> hit; Wound: 4 (S4 vs T4 needs 4+) -> wound; Save: variable
        var dice = new ScriptedDice(d6: [4, 4, saveRoll,]);

        var profile = shooter.Weapons[0].Profiles[0];

        // Act
        var unsaved = AttackService.FireOneModelOneProfile(shooter, target, profile, range: 12, dice);

        // Assert
        // With AP-5: armor becomes 3 - (-5) = 8+ (clamped to 6+), invuln is 6+; chosen = 6+.
        // saveRoll=5 -> fail, 1 damage; saveRoll=6 -> save, 0 damage.
        Assert.Equal(expectedDamageTaken > 0 ? 1 : 0, unsaved);
        Assert.Equal(2 - expectedDamageTaken, victim.RemainingWounds);
    }
}