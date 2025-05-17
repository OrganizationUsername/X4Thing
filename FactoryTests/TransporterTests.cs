using System.Numerics;
using FactoryCli;

namespace FactoryTests;

public class TransporterTests
{
    [Fact]
    public void Transporter_PicksUpResource_WhenAvailable()
    {
        var gameData = new GameData();
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
        var gameData = new GameData();
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
        var gameData = new GameData();
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
        Assert.Equal(0, transporter.Carrying.Count);
        Assert.Equal(0, sourceStorage.GetAmount(metalBar));
        Assert.Equal(0, destStorage.GetAmount(metalBar)); // should be consumed
        Assert.Equal(0, destStorage.GetAmount(plastic));  // should be consumed
        Assert.Equal(1, destStorage.GetAmount(computerPart));
    }


}