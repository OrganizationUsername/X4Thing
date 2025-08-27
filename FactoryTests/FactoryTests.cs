using Factory.Core;
using System.Numerics;

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 5 }, }) { Name = "Facility", };  // 5 workshops, but resources restrict to 1 job

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { metalBarRecipe, 1 }, { breadRecipe, 1 }, }) { Name = "Facility", };

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 2 }, }) { Name = "Facility", }; // Two workshops = two jobs in parallel

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { metalBarRecipe, 2 }, { breadRecipe, 1 }, }) { Name = "Facility", };

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
        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Name = "Facility", };

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

        var facility = new ProductionFacility(storage, new Dictionary<Recipe, int> { { metalBarRecipe, 1 }, { computerPartRecipe, 1 }, }) { Name = "Facility", };

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
        var facility = new ProductionFacility(storage, []) { Name = "Facility", };

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

        var facility = new ProductionFacility(storage, []) { Name = "Facility", };

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
        var stationA = new ProductionFacility(storageA, new Dictionary<Recipe, int> { { metalBarRecipe, 1 }, }) { Name = "Facility", };

        // Station B: ComputerPart
        var storageB = new ResourceStorage();
        storageB.Add(plastic, 1);
        storageB.Add(metalBar, 1); // Preloaded to simulate partial progress
        var stationB = new ProductionFacility(storageB, new Dictionary<Recipe, int> { { computerPartRecipe, 1 }, }) { Name = "Facility", };

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

    [Fact]
    public void SolarPlant_ProducesEnergyCellWithoutInputs()
    {
        var gameData = GameData.GetDefault();
        var energyCell = gameData.GetResource("energy_cell");
        var solarRecipe = gameData.GetRecipe("recipe_energy_solar");

        var storage = new ResourceStorage();
        var solarPlant = new ProductionFacility(storage, new() { { solarRecipe, 1 }, }) { Name = "Solar", Position = new Vector2(0, 0), Id = 1, };

        var ticker = new Ticker { GameData = gameData, };
        gameData.Facilities.Add(solarPlant);
        ticker.Register(solarPlant);

        var ticksToRun = solarRecipe.Duration * 3 + 1; // run enough ticks to produce 3 energy cells. 1 tick to start or something
        ticker.RunTicks(ticksToRun);

        var amount = storage.GetAmount(energyCell);
        var logs = gameData.GetAllLogs();
        //var debugLogs = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0007] Completed job for energy_cell, output added to storage at <0, 0> at station Solar
           [Tick 0007] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0013] Completed job for energy_cell, output added to storage at <0, 0> at station Solar
           [Tick 0013] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0019] Completed job for energy_cell, output added to storage at <0, 0> at station Solar
           [Tick 0019] Started job for energy_cell (duration: 6) at <0, 0>         
         */

        Assert.True(amount >= 3, $"Expected at least 3 energy cells, got {amount}");
        Assert.Equal(0, storage.GetIncomingAmount(energyCell));

        Assert.Contains(logs, l => l is ProductionCompletedLog pl && pl.ResourceId == energyCell.Id);
    }


    [Fact]
    public void Facility_ProducesMetalBars_AfterGeneratingEnergyCells()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energyCell = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");

        var solarRecipe = gameData.GetRecipe("recipe_energy_solar");
        var metalRecipe = gameData.GetRecipe("recipe_metal_bar");

        var storage = new ResourceStorage();
        storage.Add(ore, 10); // Enough for 5 metal bars, but initially no energy cells

        var facility = new ProductionFacility(storage, new() { { solarRecipe, 1 }, { metalRecipe, 1 }, }) { Name = "DualFacility", Position = new Vector2(0, 0), Id = 1, };

        var ticker = new Ticker { GameData = gameData, };
        gameData.Facilities.Add(facility);
        ticker.Register(facility);
        var ticksToRun = 100;
        ticker.RunTicks(ticksToRun);

        var logs = gameData.GetAllLogs();
        var metal = storage.GetAmount(metalBar);
        var oreLeft = storage.GetAmount(ore);

        //var debug = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0007] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0007] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0007] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0013] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0013] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0017] Completed job for metal_bar, output added to storage at <0, 0> at station DualFacility
           [Tick 0017] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0019] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0019] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0025] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0025] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0027] Completed job for metal_bar, output added to storage at <0, 0> at station DualFacility
           [Tick 0027] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0031] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0031] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0037] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0037] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0037] Completed job for metal_bar, output added to storage at <0, 0> at station DualFacility
           [Tick 0037] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0043] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0043] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0047] Completed job for metal_bar, output added to storage at <0, 0> at station DualFacility
           [Tick 0047] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0049] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0049] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0055] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0055] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0057] Completed job for metal_bar, output added to storage at <0, 0> at station DualFacility
           [Tick 0061] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0061] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0067] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0067] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0073] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0073] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0079] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0079] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0085] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0085] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0091] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0091] Started job for energy_cell (duration: 6) at <0, 0>
           [Tick 0097] Completed job for energy_cell, output added to storage at <0, 0> at station DualFacility
           [Tick 0097] Started job for energy_cell (duration: 6) at <0, 0>         
         */

        Assert.Equal(5, metal);
        Assert.Equal(0, oreLeft);
        Assert.Equal(0, storage.GetIncomingAmount(energyCell));

        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "metal_bar", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "energy_cell", });
    }

    [Fact]
    public void SolarGenerator_ProducesEnergyCell_UsingNewWorkshopSystem()
    {
        var gameData = GameData.GetDefault();
        var energyCell = gameData.GetResource("energy_cell");
        var solarShop = gameData.GetWorkshop("solar_generator");

        var productionFacility = new ProductionFacility { Name = "Solar Plant", Position = new Vector2(0, 0), };
        productionFacility.AddProductionModule(solarShop);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(productionFacility);
        gameData.Facilities.Add(productionFacility);

        var ticksToRun = 20;
        ticker.RunTicks(ticksToRun);

        var amountProduced = productionFacility.GetStorage().GetAmount(energyCell);
        var logs = gameData.GetAllLogs();

        Assert.True(amountProduced >= 3, $"Expected at least 3 energy cells, got {amountProduced}");
        Assert.Contains(logs, l => l is ProductionCompletedLog log && log.ResourceId == energyCell.Id);
    }

    [Fact]
    public void MetalForge_UsesDesperateBulkRecipe_WithDesperateStrategy()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalForge = gameData.GetWorkshop("metal_forge");
        metalForge.Strategy = new DesperateProductionStrategy();

        var facility = new ProductionFacility { Name = "Emergency Forge", Position = new Vector2(0, 0), };
        facility.AddProductionModule(metalForge);

        facility.GetStorage().Add(ore, 50);
        facility.GetStorage().Add(energy, 20);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(facility);
        gameData.Facilities.Add(facility);

        ticker.RunTicks(2); // enough for one job to start

        var logs = gameData.GetAllLogs();
        var startedLog = logs.OfType<ProductionStartedLog>().FirstOrDefault();

        Assert.NotNull(startedLog);
        Assert.Equal("recipe_metal_bar_bulk", startedLog.Recipe.Id);
    }

    [Fact]
    public void MetalForge_UsesLeanRecipe_WithHighestBenefitStrategy()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalForge = gameData.GetWorkshop("metal_forge");

        var facility = new ProductionFacility { Name = "Efficient Forge", Position = new Vector2(0, 0), };
        facility.AddProductionModule(metalForge);

        facility.GetStorage().Add(ore, 50);
        facility.GetStorage().Add(energy, 20);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(facility);
        gameData.Facilities.Add(facility);

        ticker.RunTicks(2);

        var logs = gameData.GetAllLogs();
        var startedLog = logs.OfType<ProductionStartedLog>().FirstOrDefault();

        Assert.NotNull(startedLog);
        Assert.Equal("recipe_metal_bar", startedLog.Recipe.Id);
    }

    [Fact]
    public void ComputerAssembler_TracksTimeSinceLastJobStart_WhenMetalBarsMissing()
    {
        var gameData = GameData.GetDefault();
        var computerAssembler = gameData.GetWorkshop("computer_assembler");
        var plastic = gameData.GetResource("plastic");

        // Create a facility with one assembler but no metal bars
        var facility = new ProductionFacility { Name = "Starved Assembler", Position = new Vector2(0, 0), };
        facility.AddProductionModule(computerAssembler);
        facility.GetStorage().Add(plastic, 100); // Add enough plastic

        var ticker = new Ticker { GameData = gameData, };
        gameData.Facilities.Add(facility);
        ticker.RegisterAll();
        ticker.RunTicks(50); // Run a number of ticks where metal bars are unavailable

        var instance = facility.WorkshopInstances.FirstOrDefault(w => w.Module.Name == "computer_assembler");

        Assert.NotNull(instance);
        Assert.Null(instance.ActiveJob); // Should not have started
        Assert.True(instance.TimeSinceLastStarted >= 50, $"Expected starvation of at least 50 ticks, got {instance.TimeSinceLastStarted}");

        // Optionally, confirm a ProductionStartedLog did NOT exist
        var logs = gameData.GetAllLogs();
        var anyStarted = logs.OfType<ProductionStartedLog>().Any(l => l.Recipe.Output.Id == "computer_part");
        Assert.False(anyStarted, "Expected no computer part jobs to start due to missing metal bars");
    }

    [Fact]
    public void GameData_SwitchesToDesperate_WhenStationStarvesForMetalBars()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");

        var forgeModule = gameData.GetWorkshop("metal_forge");
        forgeModule.Strategy = new HighestBenefitProductionStrategy(); // Initially lean

        var station = new ProductionFacility { Name = "Solo Assembler", Position = new Vector2(10, 0), };

        station.AddProductionModule(forgeModule);

        // Fill with plastic for assemblers, and materials for forge
        station.GetStorage().Add(ore, 10); //Enough to run it for 50 ticks
        station.GetStorage().Add(energy, 2_000);
        gameData.Facilities.Add(station);

        // Enable metal bar shortage detection
        //gameData.EnableShortageDetectionFor(metalBar, triggerAfterTicks: 80); //this method doesn't exist
        //This doesn't work yet. I should 

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(station);

        // Act
        ticker.RunTicks(150);

        // Assert
        var strategy = forgeModule.Strategy;
        Assert.IsType<DesperateProductionStrategy>(strategy);

        // Optionally inspect logs for debugging
        var logs = gameData.GetAllLogsFormatted();
        Assert.Contains("desperate", logs.ToLower()); // if you later log the switch
    }

    //ToDo: At some point I have to figure out when to calm down. Maybe it depends on how many I have on hand and how long I've had a large stock.
}