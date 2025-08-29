using _40K.Core;
using System.Numerics;
using Xunit.Abstractions;

namespace _40K.Tests;

public class DegradingTableShootingTests(ITestOutputHelper output)
{
    private static ModelProfile IntercessorProfile => new()
    {
        Name = "Intercessor",
        Stats = new Statline { BallisticSkill = 3, Toughness = 4, WoundsPerModel = 2, Saves = new SaveProfile { Armor = 3, }, },
    };
    private static ModelProfile GhostkeelLikeProfile => new()
    {
        Name = "Bracketed Shooter",
        Stats = new Statline { BallisticSkill = 4, Toughness = 6, WoundsPerModel = 6, Saves = new SaveProfile { Armor = 3, Invulnerable = 5, }, },
        BallisticSkillModifier = _ => 0, // attacker aura not used here
        Degrading = new DegradingTable
        {
            Brackets = {
                new DegradeBracket(6, 6, 12, 4, 3), // 6W:   BS 4+
                new DegradeBracket(3, 5,  8, 5, 2), // 3–5W: BS 5+
                new DegradeBracket(1, 2,  4, 6 /* Changed this just for unit test */, 1),
            },
        },
    };

    private static Unit TenIntercessorsAt(Vector2 pos)
    {
        var u = new Unit { Name = "Intercessors", };
        for (var i = 0; i < 10; i++) { u.Models.Add(new Model(IntercessorProfile) { Position = pos, }); }
        return u;
    }

    private static Weapon TestBurstCannon() // 3 shots, S5, AP0, D1
    {
        var p = new WeaponProfile { Name = "Test Burst (Assault 3)", Range = 18, Strength = 5, Ap = 0, Shots = _ => 3, Damage = _ => 1, };
        return new Weapon { Name = "Test Burst", Profiles = { p, }, };
    }

    private static Model BracketedShooterAt(Vector2 pos)
    {
        var m = new Model(GhostkeelLikeProfile) { Position = pos, };
        m.Weapons.Add(TestBurstCannon());
        return m;
    }

    [Fact]
    public void DegradingShooter_Fires_ThenTakesDamage_Twice_WithSameDice_GetsThreeDistinctOutcomes()
    {
        // Arrange: shooter at (0,0); 10 Intercessors at 12"
        var shooter = BracketedShooterAt(new Vector2(0, 0));
        var profile = shooter.Weapons[0].Profiles[0];

        // Helper to reset a fresh 10-man target each volley
        Unit FreshDefenders() => TenIntercessorsAt(new Vector2(12, 0));

        // ---------- Volley 1: BS 4+ (6W) ----------
        // Per shot: Hit, Wound, Save. Misses consume only the hit die.
        RecordingCombatLog recordingCombatLog = new();
        int[] seq1 = [
            6, 6, 1,
            5, 6, 1,
            4, 6, 1,
        ];
        var defenders1 = FreshDefenders();
        var dice1 = new ScriptedDice(d6: seq1);
        var unsaved1 = AttackService.FireOneModelOneProfile(shooter, defenders1, profile, range: 0, dice1, 0, recordingCombatLog);

        Assert.Equal(3, unsaved1); // 3 unsaved wounds
        Assert.Equal(1, defenders1.Models.Count(m => !m.IsAlive)); // 1 dead marine
        Assert.Equal(1, defenders1.Alive.First().RemainingWounds); // next marine on 1W
        Assert.Equal(3, defenders1.Models.Sum(m => m.Profile.Stats.WoundsPerModel - m.RemainingWounds));

        output.WriteLine("Volley 1 Combat Log:");
        output.WriteLine(recordingCombatLog.ToLines());
        output.WriteLine(string.Empty);
        recordingCombatLog.Clear();

        /*
        Volley 1 Combat Log:
           -- Bracketed Shooter fires Test Burst (Assault 3) at Intercessors (dist 12") ×0
           Hit: roll 6 + 0 vs 4 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 1
           Hit: roll 5 + 0 vs 4 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 0
           Hit: roll 4 + 0 vs 4 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 1
         */

        // Move to mid-bracket: BS 5+ (deal 1 damage to go 6 -> 5W)
        shooter.ApplyDamage(1, null);
        recordingCombatLog.Clear();
        // ---------- Volley 2: BS 5+ (5W) ----------
        int[] seq2 = [
            6, 6, 1,
            5, 6, 1,
            4, //miss
        ];
        var defenders2 = FreshDefenders();
        var dice2 = new ScriptedDice(d6: seq2);
        var unsaved2 = AttackService.FireOneModelOneProfile(shooter, defenders2, profile, range: 0, dice2, 0, recordingCombatLog);

        Assert.Equal(2, unsaved2); // only the 2 unsaved wounds
        Assert.Equal(1, defenders2.Models.Count(m => !m.IsAlive));
        Assert.Equal(2, defenders2.Models.Sum(m => m.Profile.Stats.WoundsPerModel - m.RemainingWounds));

        output.WriteLine("Volley 2 Combat Log:");
        output.WriteLine(recordingCombatLog.ToLines());
        output.WriteLine(string.Empty);
        recordingCombatLog.Clear();
        /*
        Volley 2 Combat Log:
           -- Bracketed Shooter fires Test Burst (Assault 3) at Intercessors (dist 12") ×0
           Hit: roll 6 + 0 vs 5 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 1
           Hit: roll 5 + 0 vs 5 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 0
           Hit: roll 4 + 0 vs 5 ⇒ MISS
         */

        // Move to low bracket: BS 6+ (deal 3 more damage: 5 -> 2W)
        shooter.ApplyDamage(3, null);

        // ---------- Volley 3: BS 6+ (2W) ----------
        int[] seq3 = [
            6, 6, 1,
            5, //miss
            4, //miss
        ];
        var defenders3 = FreshDefenders();
        var dice3 = new ScriptedDice(d6: seq3);
        var unsaved3 = AttackService.FireOneModelOneProfile(shooter, defenders3, profile, range: 0, dice3, 0, recordingCombatLog);

        Assert.Equal(1, unsaved3);
        Assert.Equal(0, defenders3.Models.Count(m => !m.IsAlive));
        Assert.Equal(1, defenders3.Alive.First().RemainingWounds);
        Assert.Equal(1, defenders3.Models.Sum(m => m.Profile.Stats.WoundsPerModel - m.RemainingWounds));

        output.WriteLine("Volley 3 Combat Log:");
        output.WriteLine(recordingCombatLog.ToLines());
        output.WriteLine(string.Empty);
        recordingCombatLog.Clear();
        /*
        Volley 3 Combat Log:
           -- Bracketed Shooter fires Test Burst (Assault 3) at Intercessors (dist 12") ×0
           Hit: roll 6 + 0 vs 6 ⇒ HIT
           Wound: roll 6 vs 3 ⇒ WOUND
           Save: roll 1 vs 3 ⇒ FAILED
           Damage: 1
           Intercessor health remaining: 1
           Hit: roll 5 + 0 vs 6 ⇒ MISS
           Hit: roll 4 + 0 vs 6 ⇒ MISS
         */
    }
}