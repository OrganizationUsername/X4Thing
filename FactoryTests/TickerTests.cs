using Factory.Core;

namespace Factory.Tests;

public class TickerTests
{
    [Fact]
    public void GetTicksUntilNextEvent_ReturnsNull_WhenFacilityIsIdle()
    {
        var storage = new ResourceStorage();

        var facility = new ProductionFacility(storage, []) { Name = "Facility", }; // No workshops

        Assert.Null(facility.GetTicksUntilNextEvent());
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsZero_WhenJobCanStartNow()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

        Assert.Equal(0, facility.GetTicksUntilNextEvent());
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsTicksUntilNextCompletion()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

        var ticker = new Ticker();
        ticker.Register(facility);

        // Start job
        ticker.RunTicks(1); // Elapsed = 0
        Assert.Equal(10, facility.GetTicksUntilNextEvent());

        // Simulate 3 more ticks
        ticker.RunTicks(3); // Elapsed = 3
        Assert.Equal(7, facility.GetTicksUntilNextEvent());
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsSoonestCompletionOfMultipleJobs()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 8);
        storage.Add(energy, 4);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 2 }, }) { Name = "Facility", };

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(1); // Both jobs started
        ticker.RunTicks(3); // Elapsed = 4
        Assert.Equal(7, facility.GetTicksUntilNextEvent()); // NEW: job already ticked on first run
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsZero_WhenJobBecomesPossible()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

        Assert.Null(facility.GetTicksUntilNextEvent()); // No inputs

        storage.Add(ore, 2);
        storage.Add(energy, 1);

        Assert.Equal(0, facility.GetTicksUntilNextEvent()); // Now it can start
    }
}