using Factory.Core;

namespace Factory.Tests;

public class FactoryTests
{
    [Fact]
    public void ProductionFacility_DoesNotStartJobs_WhenResourcesAreInsufficient()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage(); // No inputs

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int>
        {
            { recipe, 1 }, // One workshop for MetalBar production
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(20); // Run enough ticks to see if any job starts
        Assert.Equal(0, storage.GetAmount(metalBar)); // No MetalBar should be produced
    }

    [Fact]
    public void ProductionFacility_UsesOnlyEnoughWorkshops_AsResourcesAllow()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 4);        // Enough for 2 jobs
        storage.Add(energy, 1);     // Only enough for 1 job

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 5 }, });  // 5 workshops, but resources restrict to 1 job

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(11);

        Assert.Equal(1, storage.GetAmount(metalBar)); // Only 1 MetalBar should be produced
    }

    [Fact]
    public void ProductionFacility_DoesNotStarve_OneRecipe_WhenSharingInputs()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var bread = gameData.GetResource("bread");

        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var flour = gameData.GetResource("flour");
        var wheat = gameData.GetResource("wheat");

        var metalBarRecipe = gameData.Recipes.Values.First(r => r.Output == metalBar);
        var breadRecipe = gameData.Recipes.Values.First(r => r.Output == bread);

        var storage = new ResourceStorage();
        storage.Add(ore, 2);
        storage.Add(energy, 1);
        storage.Add(flour, 1);
        storage.Add(wheat, 2);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { metalBarRecipe, 1 }, { breadRecipe, 1 }, });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(11); // Bread done at 9, MetalBar at 11

        Assert.Equal(1, storage.GetAmount(bread));
        Assert.Equal(1, storage.GetAmount(metalBar));
    }

    [Fact]
    public void ProductionFacility_Produces_One_MetalBar_With_Sufficient_Resources_And_One_Workshop()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Run 10 ticks — job still in progress
        ticker.RunTicks(10);
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Tick 11 — job completes
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(metalBar));

        // Further ticks don't trigger new jobs (no more inputs)
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(metalBar));
    }

    [Fact]
    public void ProductionFacility_Uses_Multiple_Workshops_To_Produce_Parallel()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 10);         // Enough for 5 jobs
        storage.Add(energy, 5);       // Enough for 5 jobs

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int>
        {
            { recipe, 2 }, // Two workshops = two jobs in parallel
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(10); // Tick 1–10: jobs in progress
        Assert.Equal(0, storage.GetAmount(metalBar));

        ticker.RunTicks(1); // Tick 11: both jobs complete
        Assert.Equal(2, storage.GetAmount(metalBar));
    }

    [Fact]
    public void Facility_Produces_Bread_And_MetalBar_Simultaneously()
    {
        var gameData = GameData.GetDefault();

        var wheat = gameData.GetResource("wheat");
        var flour = gameData.GetResource("flour");
        var bread = gameData.GetResource("bread");

        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");

        var breadRecipe = gameData.Recipes.Values.First(r => r.Output == bread);
        var metalBarRecipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(wheat, 2);
        storage.Add(flour, 1);
        storage.Add(ore, 4);
        storage.Add(energy, 2);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int>
        {
            { metalBarRecipe, 2 },
            { breadRecipe, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–8: Bread and MetalBars still in progress
        ticker.RunTicks(8);
        Assert.Equal(0, storage.GetAmount(bread));
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Tick 9: Bread completes
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(bread));
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Tick 10: MetalBars still not done
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(bread));
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Tick 11: MetalBars complete
        ticker.RunTicks(1);
        Assert.Equal(2, storage.GetAmount(metalBar));
    }

    [Fact]
    public void ProductionFacility_Starts_Production_When_Resources_Arrive_MidTick()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–5: no resources
        ticker.RunTicks(5);
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Add inputs after tick 5
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        // Tick 6–15: in progress
        ticker.RunTicks(10);
        Assert.Equal(0, storage.GetAmount(metalBar)); // still running

        // Tick 16: complete
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(metalBar));

        // Second job
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        // Tick 17–26: working
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(metalBar)); // still 1

        // Tick 27: done
        ticker.RunTicks(1);
        Assert.Equal(2, storage.GetAmount(metalBar));
    }

    [Fact]
    public void ProductionFacility_Produces_ComputerPart_From_Upstream_MetalBars()
    {
        var gameData = GameData.GetDefault();

        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");
        var computerPart = gameData.GetResource("computer_part");

        var metalBarRecipe = gameData.Recipes.Values.First(r => r.Output == metalBar);
        var computerPartRecipe = gameData.Recipes.Values.First(r => r.Output == computerPart);

        var storage = new ResourceStorage();
        storage.Add(metalBar, 1);       // pre-made
        storage.Add(plastic, 1);
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { metalBarRecipe, 1 }, { computerPartRecipe, 1 }, });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–10: MetalBar is still in progress
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(metalBar));
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 11: MetalBar completes, ComputerPart starts
        ticker.RunTicks(1);
        Assert.Equal(0, storage.GetAmount(metalBar));
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 12–21: in progress
        ticker.RunTicks(9);
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 22: complete
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(computerPart));
    }

    [Fact]
    public void ProductionFacility_BeginsProduction_AfterWorkshopIsAdded()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.Recipes.Values.First(r => r.Output == metalBar);

        var storage = new ResourceStorage();
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        // No workshops yet
        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int>());

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–5: idle
        ticker.RunTicks(5);
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Add workshop
        facility.AddWorkshops(recipe, 1);

        // Tick 6–15: running
        ticker.RunTicks(10);
        Assert.Equal(0, storage.GetAmount(metalBar));

        // Tick 16: done
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(metalBar));
    }

    [Fact]
    public void ProductionFacility_CreatesComputerPart_WhenWorkshopAdded_Later()
    {
        var gameData = GameData.GetDefault();

        var metalBar = gameData.GetResource("metal_bar");
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var plastic = gameData.GetResource("plastic");
        var computerPart = gameData.GetResource("computer_part");

        var metalBarRecipe = gameData.Recipes.Values.First(r => r.Output == metalBar);
        var computerPartRecipe = gameData.Recipes.Values.First(r => r.Output == computerPart);

        var storage = new ResourceStorage();
        storage.Add(metalBar, 1);       // pre-existing
        storage.Add(plastic, 1);
        storage.Add(ore, 2);
        storage.Add(energy, 1);

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int>());

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–5: No workshops
        ticker.RunTicks(5);
        Assert.Equal(1, storage.GetAmount(metalBar));
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 6: Add workshop for MetalBar
        facility.AddWorkshops(metalBarRecipe, 1);

        // Tick 6–15: Producing 2nd MetalBar
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(metalBar));
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 16: MetalBar completes
        ticker.RunTicks(1);
        Assert.Equal(2, storage.GetAmount(metalBar));
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 17: Add workshop for ComputerPart
        facility.AddWorkshops(computerPartRecipe, 1);

        // Tick 17–26: Producing ComputerPart
        ticker.RunTicks(10);
        Assert.Equal(0, storage.GetAmount(computerPart));

        // Tick 27: Done
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(computerPart));
    }

    [Fact]
    public void ProductionFacilities_Cooperate_ViaManualTransport()
    {
        var gameData = GameData.GetDefault();

        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var plastic = gameData.GetResource("plastic");
        var metalBar = gameData.GetResource("metal_bar");
        var computerPart = gameData.GetResource("computer_part");

        var metalBarRecipe = gameData.Recipes.Values.First(r => r.Output == metalBar);
        var computerPartRecipe = gameData.Recipes.Values.First(r => r.Output == computerPart);

        // Station A: MetalBar
        var storageA = new ResourceStorage();
        storageA.Add(ore, 2);
        storageA.Add(energy, 1);
        var stationA = new ProductionFacility(storageA, new Dictionary<Recipe, int>
        {
            { metalBarRecipe, 1 },
        });

        // Station B: ComputerPart
        var storageB = new ResourceStorage();
        storageB.Add(plastic, 1);
        storageB.Add(metalBar, 1); // Preloaded to simulate partial progress
        var stationB = new ProductionFacility(storageB, new Dictionary<Recipe, int> { { computerPartRecipe, 1 }, });

        var ticker = new Ticker();
        ticker.Register(stationA);
        ticker.Register(stationB);

        // Tick 1–10: MetalBar in progress
        ticker.RunTicks(10);
        Assert.Equal(0, storageA.GetAmount(metalBar));
        Assert.Equal(0, storageB.GetAmount(computerPart));

        // Tick 11: MetalBar complete
        ticker.RunTicks(1);
        Assert.Equal(1, storageA.GetAmount(metalBar));

        // Transport from A → B
        var transferred = storageA.Consume(metalBar, 1);
        Assert.True(transferred);
        storageB.Add(metalBar, 1);

        // Tick 12–21: ComputerPart in progress
        ticker.RunTicks(9);
        Assert.Equal(0, storageB.GetAmount(computerPart));

        // Tick 22: still not done
        ticker.RunTicks(1);
        Assert.Equal(0, storageB.GetAmount(computerPart));

        // Tick 23: done
        ticker.RunTicks(1);
        Assert.Equal(1, storageB.GetAmount(computerPart));
    }
}