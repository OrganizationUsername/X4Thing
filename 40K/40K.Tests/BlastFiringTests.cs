using _40K.Core;
using System.Numerics;
using Xunit.Abstractions;

namespace _40K.Tests;

public class BlastFiringTests(ITestOutputHelper output)
{
    [Fact]
    public void Blast_AddsPlus1PerFiveModels_ToBaseShots_AndLogs()
    {
        var frag = new WeaponProfile { Name = "Frag Grenade", Range = 30, Strength = 3, IsBlast = true, Shots = d => d.D6(), Damage = _ => 1, };
        // Base D6 = 2; Target has 10 models → +2 ⇒ total 4
        var dice = new ScriptedDice(d6: [2,]);
        var ctx = new ShotContext(12, 10);

        var log = new RecordingCombatLog();
        var shots = frag.GetShots(dice, ctx, log);
        Assert.Equal(4, shots);
        Assert.Contains(log.Events, e => e.Type == CombatEventType.BlastShotCount && e is { Required: 4, Distance: 10, });
        output.WriteLine("Combat Log:");
        output.WriteLine(log.ToLines()); // Blast shot count for 10 models: 4
    }

    [Fact]
    public void Blast_EndToEnd_FiresD6PlusBonusShots_UnsavedEqualsAdjustedShots()
    {
        var log = new RecordingCombatLog();
        try
        {
            // Attacker with good BS to make hits easy
            var attackerProfile = new ModelProfile
            {
                Name = "Blaster",
                Stats = new Statline { BallisticSkill = 2, Toughness = 5, WoundsPerModel = 3, Saves = new SaveProfile { Armor = 3, }, },
            };
            var attacker = new Model(attackerProfile) { Position = new Vector2(0, 0), };

            // Frag-like blast weapon: D6 shots, S5, AP-7 (forces impossible save vs 3+), D1
            var fragBlast = new WeaponProfile
            {
                Name = "Frag (Blast)",
                Range = 30,
                Strength = 5,
                Ap = -7,                 // 3 - (-7) = 10+ → impossible save (no save die consumed)
                IsBlast = true,
                Shots = d => d.D6(),     // base roll
                Damage = _ => 1,
            };
            attacker.Weapons.Add(new Weapon { Name = "Frag Launcher", Profiles = { fragBlast, }, });

            // Target: 10 models at 12", T4, 3+ armor, 2W (doesn't matter much; we care about unsaved count)
            var targetProfile = new ModelProfile
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
            var target = new Unit { Name = "Intercessors", };
            for (var i = 0; i < 10; i++) { target.Models.Add(new Model(targetProfile) { Position = new Vector2(12, 0), }); }

            // Dice:
            // 1) D6 for base shots = 2  → adjusted shots = 2 + (10/5) = 4
            // 2) Then 4 hit rolls = 6,6,6,6 (BS2+) → all hit
            // 3) Then 4 wound rolls = 6,6,6,6 (S5 vs T4 needs 3+) → all wound
            // 4) No save rolls are consumed (impossible save)
            var dice = new ScriptedDice(d6: [2, 6, 6, 6, 6, 6, 6, 6, 6,]);

            var profile = attacker.Weapons[0].Profiles[0];

            // AttackService must pass ShotContext (distance + target count) into profile.GetShots(...)
            var unsaved = AttackService.FireOneModelOneProfile(attacker, target, profile, range: 0, dice, 0, log);

            Assert.Equal(4, unsaved); // D6(2) + 10/5=2 = 4 shots → 4 unsaved
        }
        finally
        {
            output.WriteLine("Combat Log:");
            output.WriteLine(log.ToLines());
            /*
            Combat Log:
               Blast shot count for 10 models: 4
               -- Blaster fires Frag (Blast) at Intercessors (dist 12")
               Hit: roll 6 + 0 vs 2 ⇒ HIT
               Wound: roll 6 vs 3 ⇒ WOUND
               Save: roll 6 vs 6 ⇒ SAVED
               Hit: roll 6 + 0 vs 2 ⇒ HIT
               Wound: roll 6 vs 3 ⇒ WOUND
               Save: roll 6 vs 6 ⇒ SAVED
               Hit: roll 6 + 0 vs 2 ⇒ HIT
               Wound: roll 6 vs 3 ⇒ WOUND
               Save: roll 4 vs 6 ⇒ FAILED
               Damage: 1
               Intercessor health remaining: 1
               Hit: roll 4 + 0 vs 2 ⇒ HIT
               Wound: roll 4 vs 3 ⇒ WOUND
               Save: roll 4 vs 6 ⇒ FAILED
               Damage: 1
               Intercessor health remaining: 0
             */
        }
    }
}