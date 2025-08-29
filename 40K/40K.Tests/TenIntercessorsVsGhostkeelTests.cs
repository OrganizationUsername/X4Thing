using System.Numerics;
using _40K.Core;

namespace _40K.Tests;

public class TenIntercessorsVsGhostkeelTests
{
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

    private static ModelProfile GhostkeelProfile => new()
    {
        Name = "XV95 Ghostkeel",
        Stats = new Statline
        {
            BallisticSkill = 3,
            Toughness = 6,
            WoundsPerModel = 6,
            Saves = new SaveProfile { Armor = 3, Invulnerable = 5, },
        },
    };

    private static Weapon BurstCannon()
    {
        var p = new WeaponProfile
        {
            Name = "Burst Cannon (Assault 4)",
            Range = 18,
            Strength = 5,
            Ap = 0,
            Shots = _ => 4,   // flat 4
            Damage = _ => 1,
        };
        return new Weapon { Name = "Burst Cannon", Profiles = { p, }, };
    }

    // Ghostkeel with 2x Burst Cannons (and collider if you want both available)
    private static Unit GhostkeelWithBurstCannonsAt(Vector2 pos)
    {
        var u = new Unit { Name = "Ghostkeel", };
        var m = new Model(GhostkeelProfile) { Position = pos, };
        m.Weapons.Add(BurstCannon());
        m.Weapons.Add(BurstCannon());
        m.Weapons.Add(FusionCollider());
        u.Models.Add(m);
        return u;
    }

    private static Weapon BoltRifle()
    {
        var boltProfile = new WeaponProfile
        {
            Name = "Bolt Rifle (Rapid Fire 1)",
            Range = 30,
            Strength = 4,
            Ap = -1,
            Shots = _ => 1,
            Damage = _ => 1,
            ShotsByDistance = (_, dist) => dist <= 15 ? 2 : 1,
        };
        return new Weapon { Name = "Bolt Rifle", Profiles = { boltProfile, }, };
    }

    private static Weapon FusionCollider()
    {
        var colliderProfile = new WeaponProfile
        {
            Name = "Fusion Collider (Heavy D3)",
            Range = 18,
            Strength = 8,
            Ap = -4,
            Shots = d => d.D3(),
            Damage = d => d.D6(),
        };
        return new Weapon { Name = "Fusion Collider", Profiles = { colliderProfile, }, };
    }

    // Builders
    private static Unit TenIntercessorsAt(Vector2 pos)
    {
        var u = new Unit { Name = "Intercessors", };
        for (var i = 0; i < 10; i++)
        {
            var m = new Model(IntercessorProfile) { Position = pos, };
            m.Weapons.Add(BoltRifle());
            u.Models.Add(m);
        }
        return u;
    }

    private static Unit GhostkeelAt(Vector2 pos)
    {
        var u = new Unit { Name = "Ghostkeel", };
        var m = new Model(GhostkeelProfile) { Position = pos, };
        // Weapons only needed when Ghostkeel shoots
        m.Weapons.Add(FusionCollider());
        u.Models.Add(m);
        return u;
    }

    // Fire all models in a unit with the same chosen profile (simple wrapper)
    private static int FireUnitWithProfile(Unit shooters, Unit target, WeaponProfile profile, IDice dice, int hitMod = 0)
    {
        var totalUnsaved = 0;
        foreach (var model in shooters.Alive) { totalUnsaved += AttackService.FireOneModelOneProfile(model, target, profile, range: 0, dice, hitMod); }
        return totalUnsaved;
    }

    [Fact]
    public void IntercessorsRapidFireAt12in_VsGhostkeel_Minus1ToHit_Applies_AndWeDealScriptedDamage()
    {
        // Arrange: 10 Intercessors at (0,0); Ghostkeel at (12,0) → distance 12" (≤15" → Rapid Fire, and >6" → -1 to hit)
        var marines = TenIntercessorsAt(new Vector2(0, 0));
        var ghost = GhostkeelAt(new Vector2(12, 0));

        // Chosen profile (bolt rifle)
        var boltProfile = marines.Models[0].Weapons[0].Profiles[0];

        // Script dice for the first 3 shots to push 3 damage through, then defaults (4s) cause no further damage:
        // For each shot: HIT=4 (with -1 → 3 meets BS3+), WOUND=5 (S4 vs T6 needs 5+), SAVE=2 (fail vs 4+ armor)
        // After these 9 numbers, remaining rolls default to 4: hit ok, wound fails (needs 5+), so no more damage.
        var dice = new ScriptedDice(d6: [4, 5, 2, 4, 5, 2, 4, 5, 2,]);

        // Act
        var hitMod = -1; // Ghostkeel stealth: if >6", enemy subtracts 1 from hit rolls
        var unsaved = FireUnitWithProfile(marines, ghost, boltProfile, dice, hitMod);

        // Assert
        Assert.Equal(3, unsaved);                        // 3 unsaved wounds inflicted
        Assert.Equal(3, ghost.Models[0].RemainingWounds); // Ghostkeel 6W → now 3W
    }

    [Fact]
    public void IntercessorsAt24in_NotHalfRange_StillMinus1ToHit_FewerShots_LessDamage()
    {
        // Arrange: 10 Intercessors at (0,0); Ghostkeel at (24,0) → distance 24" (>15" so 1 shot each; >6" → -1 to hit)
        var marines = TenIntercessorsAt(new Vector2(0, 0));
        var ghost = GhostkeelAt(new Vector2(24, 0));

        var boltProfile = marines.Models[0].Weapons[0].Profiles[0];

        // Script only the first 2 shots to go through (each: 4,5,2), then defaults prevent more damage
        var dice = new ScriptedDice(d6: [4, 5, 2, 4, 5, 2,]);

        // Act
        var hitMod = -1;
        var unsaved = FireUnitWithProfile(marines, ghost, boltProfile, dice, hitMod);

        // Assert
        Assert.Equal(2, unsaved);
        Assert.Equal(4, ghost.Models[0].RemainingWounds); // 6W → 4W
    }

    [Fact]
    public void Ghostkeel_FusionCollider_HeavyD3_IntoMarines_KillsWithHighDamage()
    {
        // Arrange: Ghostkeel at (0,0) with Fusion Collider; Intercessor target at (12,0) (within 18")
        var ghost = GhostkeelAt(new Vector2(0, 0));
        var marines = new Unit { Name = "Marines", };
        var m1 = new Model(IntercessorProfile) { Position = new Vector2(12, 0), };
        marines.Models.Add(m1);

        var collider = ghost.Models[0].Weapons[0].Profiles[0];

        // Script: D3 shots = 3; for the first shot make everything succeed and deal 5+ damage:
        // D3=3, Hit=6, Wound=3 (S8 vs T4 needs 2+), Save roll is irrelevant vs AP-4 (no invuln), Damage=6 (kills 2W marine)
        // (Remaining shot rolls default to 4s, but target is already dead.)
        var dice = new ScriptedDice(
            d6: [6, 3, 6,], // Hit=6, Wound=3, Damage=6   (we don't enqueue a save roll since it can't be saved against)
            d3: [3,] // Heavy D3 -> 3 shots
        );

        // Act
        var unsaved = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, collider, range: 0, dice, hitMod: 0);

        // Assert: at least 1 unsaved; first marine should be dead
        Assert.True(unsaved >= 1);
        Assert.False(m1.IsAlive);
    }

    [Fact]
    public void Ghostkeel_FusionCollider_At12in_TwoShots_KillsOne_AndWoundsNext()
    {
        // Ghostkeel at 0", Marines at 12" (within 18" range)
        var ghost = GhostkeelAt(new Vector2(0, 0)); // builder adds the collider
        var marines = new Unit { Name = "Marines", };
        var m1 = new Model(IntercessorProfile) { Position = new Vector2(12, 0), };
        var m2 = new Model(IntercessorProfile) { Position = new Vector2(12, 0), };
        marines.Models.Add(m1);
        marines.Models.Add(m2);

        var collider = ghost.Models[0].Weapons[0].Profiles[0]; // Fusion Collider (AP-4)

        // AP-4 vs 3+ armor => 7+ required => impossible save => NO save roll consumed.
        // Per shot now: Hit, Wound, Damage (no Save).
        // Shot1: Hit=5, Wound=4 (2+), Damage=3 -> kills m1 (2W)
        // Shot2: Hit=4, Wound=4 (2+), Damage=1 -> m2 to 1W
        var dice = new ScriptedDice(
            d6: [5, 4, 3, 4, 4, 1,],
            d3: [2,] // Heavy D3 -> 2 shots
        );

        var unsaved = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, collider, 0, dice);

        Assert.True(unsaved >= 2);
        Assert.False(m1.IsAlive);
        Assert.Equal(1, m2.RemainingWounds);
    }

    [Fact]
    public void Ghostkeel_TwoBurstCannons_At12in_PushesSomeDamage_ThenSavesCleanUp()
    {
        // Ghostkeel with 2x Burst at 0", Marines at 12"
        var ghost = GhostkeelWithBurstCannonsAt(new Vector2(0, 0));
        var marines = new Unit { Name = "Marines", };
        var m1 = new Model(IntercessorProfile) { Position = new Vector2(12, 0), };
        var m2 = new Model(IntercessorProfile) { Position = new Vector2(12, 0), };
        marines.Models.Add(m1);
        marines.Models.Add(m2);

        var burstProfile = ghost.Models[0].Weapons.First(w => w.Name.Contains("Burst")).Profiles[0];

        // Each burst cannon = 4 shots; firing twice = 8 + 8 = 16 shots total.
        // We script the first 3 shots only to control outcomes:
        var dice = new ScriptedDice(d6:
            [
            5, 4, 2, //1) 5,4,2  -> hit, wound (3+), save fail → 1 dmg to m1
            5, 4, 2, //2) 5,4,2  -> another unsaved → m1 dies
            4, 4, 2, //3) 4,4,2  -> 1 dmg to m2 (down to 1W)
                     //The rest default to 4s: hit ok, wound ok, but save=4 succeeds vs 3+ armor → no further damage.
            ]);

        // Fire first burst cannon
        var unsaved1 = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, burstProfile, 0, dice, hitMod: 0);
        // Fire second burst cannon
        var unsaved2 = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, burstProfile, 0, dice, hitMod: 0);
        var totalUnsaved = unsaved1 + unsaved2;

        Assert.True(totalUnsaved >= 3);
        Assert.False(m1.IsAlive);
        Assert.Equal(1, m2.RemainingWounds);
    }

    [Fact]
    public void Ghostkeel_AllGuns_IntoTenIntercessors_At12in()
    {
        // Arrange
        var ghost = GhostkeelWithBurstCannonsAt(new Vector2(0, 0));
        var marines = TenIntercessorsAt(new Vector2(12, 0)); // 10 Intercessors at 12"

        var burstProfile = ghost.Models[0].Weapons.First(w => w.Name.Contains("Burst")).Profiles[0];
        var colliderProfile = ghost.Models[0].Weapons.First(w => w.Name.Contains("Fusion Collider")).Profiles[0];

        // Dice A (Burst): script 4 unsaved wounds, then default 4s (which save on 3+)
        // Each shot uses: Hit, Wound, Save (S5 vs T4 needs 3+)
        // 1) 5,4,1 -> unsaved (1 dmg)  -> M1: 1W
        // 2) 5,4,1 -> unsaved (1 dmg)  -> M1 dead
        // 3) 5,4,1 -> unsaved (1 dmg)  -> M2: 1W
        // 4) 5,4,1 -> unsaved (1 dmg)  -> M2 dead
        var diceBurst = new ScriptedDice(d6: [5, 4, 1, 5, 4, 1, 5, 4, 1, 5, 4, 1,]);

        // Dice B (Collider): D3=2 shots, both go through and kill two more marines
        // For each shot: Hit, Wound (S8 vs T4 needs 2+), Save (6+ after clamp), Damage D6
        var diceCollider = new ScriptedDice(
            d6: [6, 3, 2, 5, 4, 4, 1, 3,], // shot1: hit6, wound3, save2(fail), dmg5; shot2: hit4, wound4, save1(fail), dmg3
            d3: [2,] // Heavy D3 -> 2 shots
        );

        // Act — fire both Burst Cannons (8+8 shots total; only the first 4 are scripted to matter)
        var unsavedFromBurst1 = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, burstProfile, 0, diceBurst);
        var unsavedFromBurst2 = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, burstProfile, 0, diceBurst);
        // Then fire the Fusion Collider
        var unsavedFromColl = AttackService.FireOneModelOneProfile(ghost.Models[0], marines, colliderProfile, 0, diceCollider);

        var totalUnsaved = unsavedFromBurst1 + unsavedFromBurst2 + unsavedFromColl;

        // Assert — we expect 4 kills total
        Assert.True(unsavedFromBurst1 + unsavedFromBurst2 >= 4); // burst caused 4 unsaved 1-damage wounds → 2 dead
        Assert.True(unsavedFromColl >= 2);                       // collider killed ~2 more
        Assert.Equal(4, marines.Models.Count(m => !m.IsAlive));  // 4 dead marines
        Assert.Equal(6, marines.Alive.Count());                  // 6 remaining
        Assert.True(totalUnsaved >= 6);                          // 4 (burst) + 2 (collider)
    }

}