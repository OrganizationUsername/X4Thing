using System.Numerics;
using Factory.Core;

namespace Factory.Tests;

public class FighterTests
{
    [Fact]
    public void Fighter_DestroysTransporter_WithValuableCargo()
    {
        var gameData = GameData.GetDefault();
        var aiModule = gameData.GetResource("ai_module");

        var transporter = new Transporter { Id = 1, Name = "Target", Position = new Vector2(0, 0), TotalHull = 30f, MaxVolume = 100f, };
        transporter.Carrying.Add(new ResourceAmount(aiModule, 5)); // Value = 50 * 10 = 500

        var fighter = new Fighter { Id = 99, Name = "Hunter", Position = new Vector2(50, 0), AttackDamage = 10f, SpeedPerTick = 10f, AttackRange = 5f, MinimumValue = 20f, };

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(fighter);
        ticker.Register(transporter);

        gameData.Transporters.Add(transporter);
        gameData.Fighters.Add(fighter);

        ticker.RunTicks(1);
        fighter.SetTarget(transporter, ticker.CurrentTick);

        ticker.RunTicks(10); // should be more than enough to chase + destroy

        var logs = gameData.GetAllLogs().ToList();
        var destroyedLog = logs.OfType<TransporterDestroyedLog>().FirstOrDefault();
        var lostCargoLogs = logs.OfType<TransporterLostCargoLog>().ToList();

        var text = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Fighter 99 assigned to target 1 at <0, 0>
           [Tick 0007] Transporter 1 damaged (10) at <0, 0> by Hunter
           [Tick 0007] Transporter 99 hit (10) at <0, 0> by Hunter
           [Tick 0008] Transporter 1 damaged (10) at <0, 0> by Hunter
           [Tick 0008] Transporter 99 hit (10) at <0, 0> by Hunter
           [Tick 0009] Transporter 1 damaged (10) at <0, 0> by Hunter
           [Tick 0009] Transporter 1 destroyed at <0, 0>
           [Tick 0009] Transporter 1 lost cargo: 5 of ai_module
           [Tick 0009] Transporter 99 hit (10) at <0, 0> by Hunter
           [Tick 0010] Transporter 1 damaged (10) at <0, 0> by Hunter
           [Tick 0010] Transporter 1 destroyed at <0, 0>
           [Tick 0010] Transporter 99 hit (10) at <0, 0> by Hunter
           [Tick 0011] Transporter 1 damaged (10) at <0, 0> by Hunter
           [Tick 0011] Transporter 1 destroyed at <0, 0>
           [Tick 0011] Transporter 99 hit (10) at <0, 0> by Hunter
         */

        Assert.NotNull(destroyedLog);
        Assert.Equal(transporter.Id, destroyedLog.TransporterId);
        Assert.Empty(transporter.Carrying); // Cargo cleared
        Assert.Contains(lostCargoLogs, log => log.ResourceId == aiModule.Id && log.Amount == 5);
    }

    [Fact]
    public void Fighter_IgnoresTransporter_WithLowValueCargo()
    {
        var gameData = GameData.GetDefault();
        var sand = gameData.GetResource("sand");

        var transporter = new Transporter { Id = 1, Name = "Dummy", Position = new Vector2(0, 0) };
        transporter.Carrying.Add(new ResourceAmount(sand, 1)); // Value is too low

        var fighter = new Fighter { Id = 2, Name = "Patrol", Position = new Vector2(10, 0), MinimumValue = 20f, };

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(fighter);
        ticker.Register(transporter);

        ticker.RunTicks(5);
        fighter.SetTarget(transporter, 1);
        ticker.RunTicks(5);

        var logs = gameData.GetAllLogs().ToList();
        Assert.Empty(logs.OfType<EntityAttackedLog>());
        Assert.NotEqual(transporter, fighter.Target);
    }
}