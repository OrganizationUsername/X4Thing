using System.Numerics;
using Factory.Core;

namespace Factory.Tests;

public class FactoryStrategyTests
{
    [Fact]
    public void SustainedProductionStrategy_RequestsSufficientResources_ForPlannedDuration()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBarRecipe = gameData.GetRecipe("recipe_metal_bar");

        // --- Facility setup: 2 workshops, no starting inventory
        var storage = new ResourceStorage();
        var facility = new ProductionFacility(storage, new() { { metalBarRecipe, 2 }, }) { PullRequestStrategy = new SustainedProductionStrategy(ticks: 500), };

        // --- Act: collect pull requests
        var requests = facility.GetPullRequests().ToList();

        /*
           metal_bar recipe: 2 ore + 1 energy_cell
           duration: 10
           workshops: 2
           ticks: 500
           => 500 * 2 / 10 = 100 jobs
           => 200 ore, 100 energy_cell needed
        */

        var oreReq = requests.FirstOrDefault(r => r.resource == ore);
        var energyReq = requests.FirstOrDefault(r => r.resource == energy);

        // --- Assert
        Assert.Equal(200, oreReq.amount);
        Assert.Equal(100, energyReq.amount);
        Assert.Equal(2, requests.Count); // Only requests required resources
    }

    [Fact]
    public void Transporter_GraduallySatisfiesSustainedDemand_OverMultipleTrips()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.GetRecipe("recipe_metal_bar");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 1000);
        sourceStorage.Add(energy, 1000);
        var source = new ProductionFacility(sourceStorage, []) { Name = "Source", Position = new Vector2(0, 0), };

        var targetStorage = new ResourceStorage();
        var target = new ProductionFacility(targetStorage, new() { { recipe, 2 }, }) { Name = "Target", Position = new Vector2(10, 0), PullRequestStrategy = new SustainedProductionStrategy(ticks: 500), };

        var transporter = new Transporter { Position = source.Position, SpeedPerTick = 2f, MaxVolume = 15f, Id = 1, };

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(target);
        gameData.Transporters.Add(transporter);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(target);
        ticker.Register(transporter);

        for (var tick = 0; tick < 100; tick++)
        {
            foreach (var (res, amount) in target.GetPullRequests())
            {
                if (targetStorage.GetTotalIncludingIncoming(res) >= amount) { continue; }

                var sendAmount = Math.Min(amount, 10);
                if (sourceStorage.GetAmount(res) < sendAmount) { continue; }

                targetStorage.MarkIncoming(res, sendAmount);
                transporter.AssignTask(source, target, [new ResourceAmount(res, sendAmount),], tick);
            }

            ticker.RunTicks(1);
        }

        var logs = gameData.GetAllLogs().ToList();

        // === Positive assertions ===
        Assert.Contains(logs, l => l is DeliveryLog d && d.Delivered.Any(r => r.Resource == ore));
        Assert.Contains(logs, l => l is DeliveryLog d && d.Delivered.Any(r => r.Resource == energy));
        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "metal_bar", });

        // === Optional: Log check for debugging
        //var formatted = gameData.GetAllLogsFormatted();
        /*
           [Tick 0000] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0000] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0001] Transporter 1 picked up: 5 x ore from Source
           [Tick 0001] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0001] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0002] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0002] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0003] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0003] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0004] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0004] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0005] Transporter 1 assigned to deliver 10 x ore from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0005] Transporter 1 assigned to deliver 10 x energy_cell from Source(<0, 0>) to Target(<10, 0>)
           [Tick 0006] Received 5 of ore from Transporter at <10, 0>
           [Tick 0006] Transporter 1 delivered to <10, 0>: 5 x ore
           [Tick 0006] Transporter 1 failed to deliver: 5 x ore to Target
         */

        // You could also allow failed deliveries, but confirm that they are just partials
        var failed = logs.OfType<DeliveryFailedLog>().ToList();
        Assert.Empty(failed); //FAILS

        var partials = logs.OfType<DeliveryPartialLog>().ToList();
        Assert.NotEmpty(partials); // We expect these due to limited transporter volume

        Assert.All(partials, p =>
        {
            Assert.All(p.Partial, r =>
            {
                Assert.True(r.Amount < 10, $"Expected partial delivery, but saw full failure for: {r.Resource.Id} x{r.Amount}");
            });
        });

    }

    [Fact]
    public void Facility_RecordsLastPullRequests_AfterTick()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.GetRecipe("recipe_metal_bar");

        var storage = new ResourceStorage();
        var facility = new ProductionFacility(storage, new() { { recipe, 2 }, }) { Name = "MetalWorks", PullRequestStrategy = new SustainedProductionStrategy(ticks: 300), Position = new Vector2(0, 0), };

        var ticker = new Ticker { GameData = gameData };
        ticker.Register(facility);
        ticker.RunTicks(1);

        facility.GetPullRequests(); // Must manually trigger PullRequest evaluation or LastRequests will remain empty

        var requests = facility.LastRequests;

        Assert.NotEmpty(requests);
        Assert.Equal(2, requests.Count); // Expect exactly ore + energy

        var oreReq = requests.Single(r => r.Resource == ore);
        var energyReq = requests.Single(r => r.Resource == energy);

        Assert.Equal(120, oreReq.Amount);
        Assert.Equal(60, energyReq.Amount);
    }

    [Fact]
    public void DefaultPullRequestStrategy_RequestsMinimalInputs_WhenInsufficient()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.GetRecipe("recipe_metal_bar");

        var storage = new ResourceStorage(); // empty
        var facility = new ProductionFacility(storage, new() { { recipe, 3 }, }) // 3 workshops
        {
            PullRequestStrategy = new DefaultPullRequestStrategy()
        };

        // Act
        var requests = facility.GetPullRequests().ToList();

        // Assert: strategy should request just enough to start ONE job
        Assert.Contains(requests, r => r.resource == ore && r.amount == 2);
        Assert.Contains(requests, r => r.resource == energy && r.amount == 1);
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public void SustainedProductionStrategy_RequestsSustainedInputs_OverTime()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var recipe = gameData.GetRecipe("recipe_metal_bar"); // 2 ore, 1 energy, 10 ticks

        var storage = new ResourceStorage(); // empty
        var facility = new ProductionFacility(storage, new() { { recipe, 2 }, }) // 2 workshops
        {
            PullRequestStrategy = new SustainedProductionStrategy(ticks: 200)
        };

        // Act
        var requests = facility.GetPullRequests().ToList();

        // Expect 2 workshops × 200 ticks / 10 ticks/job = 40 jobs
        // Each job needs 2 ore → 40×2 = 80
        // Each job needs 1 energy → 40×1 = 40

        Assert.Contains(requests, r => r.resource == ore && r.amount == 80);
        Assert.Contains(requests, r => r.resource == energy && r.amount == 40);
        Assert.Equal(2, requests.Count);
    }
}