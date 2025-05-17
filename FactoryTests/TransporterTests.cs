using System.Numerics;
using FactoryCli;

namespace FactoryTests;

public class TransporterTests
{
    [Fact]
    public void Transporter_PicksUpResource_WhenAvailable()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 5);
        var source = new ProductionFacility(sourceStorage, []);

        var dest = new ProductionFacility(new ResourceStorage(), []);

        var transporter = new Transporter { Position = source.Position, };
        transporter.AssignTask(source, dest, [new(metalBar, 3),]);

        transporter.Tick(1); // Should pick up

        var carried = transporter.Carrying.SingleOrDefault(r => r.Resource == metalBar);

        Assert.NotNull(carried);
        Assert.Equal(3, carried.Amount);
        Assert.Contains("Picked up 3 x metal_bar", transporter.Log);
        Assert.Equal(2, sourceStorage.GetAmount(metalBar));
    }

    [Fact]
    public void Transporter_DeliversResource_ToDestination()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 3);
        var source = new ProductionFacility(sourceStorage, []);

        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, []);

        var transporter = new Transporter { Position = source.Position, };
        transporter.AssignTask(source, dest, [new(metalBar, 3),]);

        transporter.Tick(1); // Pickup
        transporter.Tick(2); // Deliver

        Assert.Empty(transporter.Carrying);
        Assert.Equal(3, destStorage.GetAmount(metalBar));
        Assert.Contains("Delivered 3 x metal_bar", transporter.Log);
    }

    [Fact]
    public void Transporter_DeliversResource_AndEnablesProduction()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");
        var computerPart = gameData.GetResource("computer_part");
        var recipe = gameData.GetRecipe("recipe_computer_part");

        // Source facility @ (0,0) with 2 metal bars
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 2);
        var source = new ProductionFacility(sourceStorage, []);
        source.Position = new Vector2(0, 0);

        // Destination facility @ (5,0), has plastic but needs metal bars
        var destStorage = new ResourceStorage();
        destStorage.Add(plastic, 1);
        var dest = new ProductionFacility(destStorage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Position = new Vector2(5, 0), };

        // Transporter to move 2 metal bars
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1.01f, }; // 1.01f ensure 5 steps is enough
        transporter.AssignTask(source, dest, [new(metalBar, 2),]);

        // Register all in the ticker
        var ticker = new Ticker();
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // Total ticks needed:
        // - ~5 ticks to reach source (positioned at 0, already there)
        // - 1 tick pickup
        // - ~5 ticks to reach dest
        // - 1 tick delivery
        // - 10 ticks to produce computer part
        ticker.RunTicks(17);

        Assert.Contains("Delivered", transporter.Log);
        Assert.Empty(transporter.Carrying);
        Assert.Equal(0, sourceStorage.GetAmount(metalBar));
        Assert.Equal(0, destStorage.GetAmount(metalBar)); // should be consumed
        Assert.Equal(0, destStorage.GetAmount(plastic));  // should be consumed
        Assert.Equal(1, destStorage.GetAmount(computerPart));
    }

    [Fact]
    public void Transporter_ProcessesMultipleQueuedTasksInOrder()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");


        var sourceAStorage = new ResourceStorage(); // --- Source A @ (0,0), has 2 metal bars
        sourceAStorage.Add(metalBar, 2);
        var sourceA = new ProductionFacility(sourceAStorage, []) { Position = new Vector2(0, 0), };

        var destAStorage = new ResourceStorage(); // --- Destination A @ (5,0), empty
        var destA = new ProductionFacility(destAStorage, []) { Position = new Vector2(5, 0), };


        var sourceBStorage = new ResourceStorage(); // --- Source B @ (10,0), has 1 plastic
        sourceBStorage.Add(plastic, 1);
        var sourceB = new ProductionFacility(sourceBStorage, []) { Position = new Vector2(10, 0), };

        var destBStorage = new ResourceStorage(); // --- Destination B @ (15,0), empty
        var destB = new ProductionFacility(destBStorage, []) { Position = new Vector2(15, 0), };

        // --- Transporter
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1.01f, };

        // Queue two transport tasks
        transporter.AssignTask(sourceA, destA, [new(metalBar, 2),]);
        transporter.AssignTask(sourceB, destB, [new(plastic, 1),]);

        var ticker = new Ticker();
        ticker.Register(sourceA);
        ticker.Register(destA);
        ticker.Register(sourceB);
        ticker.Register(destB);
        ticker.Register(transporter);

        // Total ticks:
        // - Task 1: move to sourceA (0), pickup (1), move to destA (~5), deliver (1) = 7 ticks
        // - Task 2: move to sourceB (~5), pickup (1), move to destB (~5), deliver (1) = 12 ticks
        // Total ~19–20 ticks should cover it
        ticker.RunTicks(20);

        Assert.Equal(2, destAStorage.GetAmount(metalBar)); // First delivery done
        Assert.Equal(1, destBStorage.GetAmount(plastic));  // Second delivery done
        Assert.Contains("Delivered", transporter.Log);
        Assert.DoesNotContain("Failed", transporter.Log);
        Assert.Empty(transporter.Carrying);
    }

    [Fact]
    public void Transporter_ProcessesMultipleQueuedTasksInOrder_WithIntermediateChecks()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");

        var sourceAStorage = new ResourceStorage();
        sourceAStorage.Add(metalBar, 2);
        var sourceA = new ProductionFacility(sourceAStorage, []) { Position = new Vector2(0, 0), };

        var destAStorage = new ResourceStorage();
        var destA = new ProductionFacility(destAStorage, []) { Position = new Vector2(5, 0), };

        var sourceBStorage = new ResourceStorage();
        sourceBStorage.Add(plastic, 1);
        var sourceB = new ProductionFacility(sourceBStorage, []) { Position = new Vector2(10, 0), };

        var destBStorage = new ResourceStorage();
        var destB = new ProductionFacility(destBStorage, []) { Position = new Vector2(15, 0), };

        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1.01f, };
        transporter.AssignTask(sourceA, destA, [new(metalBar, 2),]);
        transporter.AssignTask(sourceB, destB, [new(plastic, 1),]);

        var ticker = new Ticker();
        ticker.Register(transporter);

        // --- Tick 1: Pickup from Source A (already at 0,0)
        // --- Tick 1: Pickup from Source A (already at 0,0)
        ticker.RunTicks(1);

        // ✅ Assert immediately after pickup
        Assert.Equal(2, transporter.Carrying.Sum(x => x.Amount));
        Assert.Equal(metalBar, transporter.Carrying[0].Resource);
        Assert.Equal(new Vector2(0, 0), transporter.Position);

        // --- Tick 2–6: Moving to Destination A
        for (var i = 2; i <= 5; i++) ticker.RunTicks(1);

        // ✅ During transport, still carrying
        Assert.True(transporter.Position.X < 5f);
        Assert.Equal(2, transporter.Carrying.Sum(x => x.Amount));


        // --- Tick 7: Deliver to Destination A
        ticker.RunTicks(1);
        Assert.Empty(transporter.Carrying);
        Assert.Equal(2, destAStorage.GetAmount(metalBar));

        // --- Tick 8–12: Moving to Source B
        for (var i = 8; i <= 12; i++) ticker.RunTicks(1);
        Assert.True(transporter.Position.X >= 10f - transporter.SpeedPerTick);

        // --- Tick 13: Pickup from Source B
        ticker.RunTicks(1);
        Assert.Single(transporter.Carrying);
        Assert.Equal(plastic, transporter.Carrying[0].Resource);
        Assert.Equal(1, transporter.Carrying[0].Amount);

        // --- Tick 14–18: Moving to Destination B
        for (var i = 14; i <= 18; i++) ticker.RunTicks(1);
        Assert.True(transporter.Position.X >= 15f - transporter.SpeedPerTick);

        // --- Tick 19: Deliver to Destination B
        ticker.RunTicks(1);
        Assert.Empty(transporter.Carrying);
        Assert.Equal(1, destBStorage.GetAmount(plastic));

        // --- Final validations
        Assert.DoesNotContain("Failed", transporter.Log);
        Assert.Contains("Delivered", transporter.Log);
    }


    [Fact]
    public void Transporter_RespectsVolumeLimit_WhenPickingUp()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar"); // Volume = 1.5f

        // Source facility has 500 metal bars
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 500);
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0) };

        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, []) { Position = new Vector2(5, 0) };

        // Transporter has very small volume capacity
        var transporter = new Transporter
        {
            Position = new Vector2(0, 0),
            SpeedPerTick = 5f,
            MaxVolume = 10f, // Can carry up to 6 metal bars (6 x 1.5 = 9.0)
        };

        transporter.AssignTask(source, dest, [new ResourceAmount(metalBar, 500)]);

        var ticker = new Ticker();
        ticker.Register(transporter);
        ticker.Register(dest);

        ticker.RunTicks(3); // Tick 1: pickup, Tick 2-3: movement & delivery

        // Only 6 bars should be picked up and delivered
        Assert.Equal(6, destStorage.GetAmount(metalBar));
        Assert.Empty(transporter.Carrying);
        Assert.DoesNotContain("Failed", transporter.Log);
        Assert.Contains("Delivered", transporter.Log);

        // Confirm that source has 494 left
        Assert.Equal(494, sourceStorage.GetAmount(metalBar));
    }

}