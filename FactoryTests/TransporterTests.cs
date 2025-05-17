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
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0), Name = "Source", };

        var dest = new ProductionFacility(new ResourceStorage(), []) { Position = new Vector2(5, 0), Name = "Dest", };

        var transporter = new Transporter { Position = source.Position, MaxVolume = 10f, SpeedPerTick = 5f, Id = 42, };

        transporter.AssignTask(source, dest, [new ResourceAmount(metalBar, 3),]);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // Tick once to trigger pickup
        ticker.RunTicks(1);

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);
        var logs = gameData.GetAllLogs().ToList();
        gameData.GetAllLogsFormatted();

        var carried = transporter.Carrying.SingleOrDefault(r => r.Resource == metalBar);
        Assert.NotNull(carried);
        Assert.Equal(3, carried.Amount);

        Assert.Equal(2, sourceStorage.GetAmount(metalBar));

        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(p => p.Resource == metalBar && p.Amount == 3) && pl.TransporterId == transporter.Id);
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
    }

    [Fact]
    public void Transporter_DeliversResource_AndEnablesProduction()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");
        var computerPart = gameData.GetResource("computer_part");
        var recipe = gameData.GetRecipe("recipe_computer_part");

        // Source @ (0,0) with 2 metal bars
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 2);
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0), Name = "Source", };

        // Destination @ (5,0) has plastic, needs metal bars
        var destStorage = new ResourceStorage();
        destStorage.Add(plastic, 1);
        var dest = new ProductionFacility(destStorage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Position = new Vector2(5, 0), Name = "Destination", };

        // Transporter
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1.01f, Id = 1, };
        transporter.AssignTask(source, dest, [new ResourceAmount(metalBar, 2),]);

        // Register all
        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // Run: pickup + delivery + production
        ticker.RunTicks(17);

        gameData.Transporters.Add(transporter);
        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);

        var logs = gameData.GetAllLogs().ToList();
        //var formatted = gameData.GetAllLogsFormatted();

        // ✅ Log-based assertions
        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(r => r.Resource == metalBar && r.Amount == 2));
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(r => r.Resource == metalBar && r.Amount == 2));
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", } rl && rl.Position == dest.Position);
        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "computer_part", } sl && sl.Position == dest.Position);
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "computer_part", } cl && cl.Position == dest.Position);

        // ✅ Final state checks
        Assert.Empty(transporter.Carrying);
        Assert.Equal(0, sourceStorage.GetAmount(metalBar));
        Assert.Equal(0, destStorage.GetAmount(metalBar));
        Assert.Equal(0, destStorage.GetAmount(plastic));
        Assert.Equal(1, destStorage.GetAmount(computerPart));
    }

    [Fact]
    public void Transporter_ProcessesMultipleQueuedTasksInOrder()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");

        // Source A @ (0,0)
        var sourceAStorage = new ResourceStorage();
        sourceAStorage.Add(metalBar, 2);
        var sourceA = new ProductionFacility(sourceAStorage, []) { Position = new Vector2(0, 0), Name = "SourceA", };

        // Dest A @ (5,0)
        var destAStorage = new ResourceStorage();
        var destA = new ProductionFacility(destAStorage, []) { Position = new Vector2(5, 0), Name = "DestA", };

        // Source B @ (10,0)
        var sourceBStorage = new ResourceStorage();
        sourceBStorage.Add(plastic, 1);
        var sourceB = new ProductionFacility(sourceBStorage, []) { Position = new Vector2(10, 0), Name = "SourceB", };

        // Dest B @ (15,0)
        var destBStorage = new ResourceStorage();
        var destB = new ProductionFacility(destBStorage, []) { Position = new Vector2(15, 0), Name = "DestB", };

        // Transporter
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1.01f, MaxVolume = 10f, Id = 99, };

        // Queue two tasks
        transporter.AssignTask(sourceA, destA, [new ResourceAmount(metalBar, 2),]);
        transporter.AssignTask(sourceB, destB, [new ResourceAmount(plastic, 1),]);

        // Register for ticking
        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(sourceA);
        ticker.Register(destA);
        ticker.Register(sourceB);
        ticker.Register(destB);
        ticker.Register(transporter);

        gameData.Facilities.AddRange([sourceA, destA, sourceB, destB,]);
        gameData.Transporters.Add(transporter);

        // Run enough ticks to complete both deliveries
        ticker.RunTicks(25);

        var logs = gameData.GetAllLogs().ToList();

        // ✅ First delivery
        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "metal_bar", } t && t.From == sourceA && t.To == destA);
        Assert.Contains(logs, l => l is PickupLog p && p.PickedUp.Any(r => r.Resource == metalBar && r.Amount == 2));
        Assert.Contains(logs, l => l is DeliveryLog d && d.Delivered.Any(r => r.Resource == metalBar && r.Amount == 2) && d.Destination == destA.Position);
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", } r && r.Position == destA.Position);

        // ✅ Second delivery
        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "plastic", } t && t.From == sourceB && t.To == destB);
        Assert.Contains(logs, l => l is PickupLog p && p.PickedUp.Any(r => r.Resource == plastic && r.Amount == 1));
        Assert.Contains(logs, l => l is DeliveryLog d && d.Delivered.Any(r => r.Resource == plastic && r.Amount == 1) && d.Destination == destB.Position);
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "plastic", } r && r.Position == destB.Position);

        // ✅ Final state
        Assert.Equal(2, destAStorage.GetAmount(metalBar));
        Assert.Equal(1, destBStorage.GetAmount(plastic));
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
    }

    [Fact]
    public void Transporter_RespectsVolumeLimit_WhenPickingUp()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar"); // Volume = 1.5f

        // Source facility has 500 metal bars
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 500);
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0), };

        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, []) { Position = new Vector2(5, 0), };

        // Transporter has very small volume capacity
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 5f, MaxVolume = 10f, }; // Can carry up to 6 metal bars (6 x 1.5 = 9.0)

        transporter.AssignTask(source, dest, [new ResourceAmount(metalBar, 500),]);

        var ticker = new Ticker();
        ticker.Register(transporter);
        ticker.Register(dest);

        ticker.RunTicks(10); // Tick 1: pickup, Tick 2-3: movement & delivery

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);
        //var formattedLogs = gameData.GetAllLogsFormatted();
        /*
         [Tick 0000] Transporter 0 assigned to deliver 500 x metal_bar from Production(<0, 0>) to Production(<5, 0>)
           [Tick 0001] Transporter 0 picked up: 6 x metal_bar from Production
           [Tick 0002] Transporter 0 failed to deliver: 494 x metal_bar to Production
         */

        // Only 6 bars should be picked up and delivered
        Assert.Equal(6, destStorage.GetAmount(metalBar)); //FAIL 0
        Assert.Empty(transporter.Carrying);

        // Confirm that source has 494 left
        Assert.Equal(494, sourceStorage.GetAmount(metalBar));
    }

    [Fact]
    public void Transporter_IsAutomaticallyAssigned_WhenStationsHavePushAndPull()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 100);
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0), Id = 1, Name = "Source", };

        var destStorage = new ResourceStorage();
        var recipe = gameData.GetRecipe("recipe_computer_part");
        var dest = new ProductionFacility(destStorage, new Dictionary<Recipe, int> { { recipe, 1 }, }) { Position = new Vector2(5, 0), Id = 2, Name = "Destination", };

        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 5f, MaxVolume = 10f, Id = 3, };

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);

        // Fake a pull request by simulating negative metalBars at the destination
        destStorage.Add(metalBar, -10);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // Run full cycle of assignment → pickup → delivery
        ticker.RunTicks(5);

        // Collect logs at the end
        var logs = gameData.GetAllLogs().ToList();
        gameData.GetAllLogsFormatted();

        // Assert key events occurred
        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "metal_bar", } al && al.From == source && al.To == dest);
        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(p => p.Resource == metalBar));
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(d => d.Resource == metalBar));
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", } rl && rl.Position == dest.Position);

        // Final inventory checks
        Assert.Empty(transporter.Carrying);
        Assert.True(destStorage.GetAmount(metalBar) > 0, $"Expected metalBars in dest, found: {destStorage.GetAmount(metalBar)}");
    }

    [Fact]
    public void Transporter_AutoDelivers_ProducedMetalBar_ToConsumerFacility()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var plastic = gameData.GetResource("plastic");
        gameData.GetResource("metal_bar");
        var computerPart = gameData.GetResource("computer_part");

        // Source produces metal bars
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 200);
        sourceStorage.Add(energy, 100);
        var metalBarRecipe = gameData.GetRecipe("recipe_metal_bar");
        var source = new ProductionFacility(sourceStorage, new() { { metalBarRecipe, 2 }, })
        { Position = new Vector2(0, 0), Name = "Source", };

        // Destination builds computer parts
        var destStorage = new ResourceStorage();
        destStorage.Add(plastic, 10);
        var computerRecipe = gameData.GetRecipe("recipe_computer_part");
        var dest = new ProductionFacility(destStorage, new() { { computerRecipe, 1 }, }) { Position = new Vector2(5, 0), Name = "Destination", };

        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 5f, MaxVolume = 10f, Id = 0, };

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // Run ticks to let production, transport, and consumption happen
        ticker.RunTicks(25);

        var logs = gameData.GetAllLogs().ToList();

        // ✅ Assertions using structured logs
        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "metal_bar", });
        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(r => r.Resource.Id == "metal_bar"));
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(r => r.Resource.Id == "metal_bar"));
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", });

        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "computer_part", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "computer_part", });

        // ✅ Sanity check: computer part actually got produced
        Assert.True(destStorage.GetAmount(computerPart) > 0, "Expected at least one computer_part to be produced.");
    }

    [Fact]
    public void Transporter_AutoDelivers_ProducedMetalBar_ToConsumerFacility_WithSteps()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");
        var computerPart = gameData.GetResource("computer_part");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 200);
        sourceStorage.Add(energy, 100);
        var metalBarRecipe = gameData.GetRecipe("recipe_metal_bar");
        var source = new ProductionFacility(sourceStorage, new() { { metalBarRecipe, 2 }, }) { Position = new Vector2(0, 0), Name = "Source", };

        var destStorage = new ResourceStorage();
        destStorage.Add(plastic, 10);
        var computerRecipe = gameData.GetRecipe("recipe_computer_part");
        var dest = new ProductionFacility(destStorage, new() { { computerRecipe, 1 }, }) { Position = new Vector2(5, 0), Name = "Destination", };

        var transporter = new Transporter { Position = new Vector2(5, 0), SpeedPerTick = 1f, MaxVolume = 10f, Id = 0, };

        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // --- All ticks up front
        ticker.RunTicks(31);

        // Now collect logs once at the end
        var logs = gameData.GetAllLogs().ToList();

        // Log-based assertions
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "metal_bar", } pl && pl.Position == source.Position);
        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "metal_bar", } al && al.From.Position == source.Position && al.To.Position == dest.Position);
        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(p => p.Resource == metalBar) && pl.TransporterId == transporter.Id);
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(d => d.Resource == metalBar) && dl.Destination == dest.Position);
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", } rl && rl.Position == dest.Position);
        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "computer_part", } sl && sl.Position == dest.Position);
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "computer_part", } cl && cl.Position == dest.Position);
        Assert.Empty(transporter.Carrying);

        const int pre = 0;
        var post = destStorage.GetAmount(computerPart);
        Assert.True(post > pre, $"Expected more computer_parts. Before: {pre}, After: {post}");

        // Pull logs and assert order
        int GetTick<T>(Func<T, bool> predicate) where T : class, ILogLine => logs.OfType<T>().First(predicate).Tick;

        var t1 = GetTick<ProductionCompletedLog>(l => l.ResourceId == "metal_bar" && l.Position == source.Position);
        var t2 = GetTick<TransportAssignedLog>(l => l.ResourceId == "metal_bar" && l.From == source && l.To == dest);
        var t3 = GetTick<PickupLog>(l => l.PickedUp.Any(p => p.Resource == metalBar) && l.TransporterId == transporter.Id);
        var t4 = GetTick<DeliveryLog>(l => l.Delivered.Any(p => p.Resource == metalBar) && l.Destination == dest.Position);
        var t5 = GetTick<TransportReceivedLog>(l => l.ResourceId == "metal_bar" && l.Position == dest.Position);
        var t6 = GetTick<ProductionStartedLog>(l => l.ResourceId == "computer_part" && l.Position == dest.Position);
        var t7 = GetTick<ProductionCompletedLog>(l => l.ResourceId == "computer_part" && l.Position == dest.Position);

        Assert.True(t1 <= t2, "Production completed before transport assignment");
        Assert.True(t2 <= t3, "Transport assigned before pickup");
        Assert.True(t3 <= t4, "Pickup before delivery");
        Assert.True(t4 <= t5, "Delivery before facility received");
        Assert.True(t5 <= t6, "Facility received before production start");
        Assert.True(t6 <= t7, "Production start before production complete");

        // Final state check
        Assert.Empty(transporter.Carrying);
        Assert.True(destStorage.GetAmount(computerPart) > 0, "Expected at least one computer_part produced.");
    }

    [Fact]
    public void System_GeneratesCorrectLogSequence_ForMetalBarDeliveryAndUsage()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");
        gameData.GetResource("computer_part");

        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 200);
        sourceStorage.Add(energy, 100);

        var source = new ProductionFacility(sourceStorage, new() { { gameData.GetRecipe("recipe_metal_bar"), 2 }, }) { Position = new Vector2(0, 0), Name = "Source", };

        var destStorage = new ResourceStorage();
        destStorage.Add(plastic, 10);

        var dest = new ProductionFacility(destStorage, new() { { gameData.GetRecipe("recipe_computer_part"), 1 }, }) { Position = new Vector2(5, 0), Name = "Destination", };

        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1f, MaxVolume = 10f, Id = 0, };

        gameData.Facilities.AddRange([source, dest,]);
        gameData.Transporters.Add(transporter);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        ticker.RunTicks(40);

        var logs = gameData.GetAllLogs();
        //var formattedText = gameData.GetAllLogsFormatted();
        /*
           [Tick 0000] Transporter 0 assigned to deliver 2 x metal_bar from Destination(<5, 0>) to Destination(<5, 0>)
           [Tick 0000] Transporter 0 assigned to deliver 2 x metal_bar from Destination(<5, 0>) to Destination(<5, 0>)
           [Tick 0000] Transporter 0 assigned to deliver 2 x metal_bar from Destination(<5, 0>) to Destination(<5, 0>)
           [Tick 0001] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0001] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0011] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0011] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0011] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0011] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0011] Transporter 0 assigned to deliver 2 x metal_bar from Source(<0, 0>) to Destination(<5, 0>)
           [Tick 0011] Transporter 0 picked up: 2 x metal_bar from Source
           [Tick 0016] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0016] Transporter 0 delivered to <5, 0>: 2 x metal_bar
           [Tick 0017] Started job for computer_part (duration: 10) at <5, 0>
           [Tick 0021] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0021] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0021] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0021] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0021] Transporter 0 assigned to deliver 2 x metal_bar from Source(<0, 0>) to Destination(<5, 0>)
           [Tick 0025] Transporter 0 picked up: 2 x metal_bar from Source
           [Tick 0027] Completed job for computer_part, output added to storage at <5, 0>
           [Tick 0030] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0030] Transporter 0 delivered to <5, 0>: 2 x metal_bar
           [Tick 0031] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0031] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0031] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0031] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0031] Started job for computer_part (duration: 10) at <5, 0>
           [Tick 0031] Transporter 0 assigned to deliver 2 x metal_bar from Source(<0, 0>) to Destination(<5, 0>)
           [Tick 0035] Transporter 0 picked up: 2 x metal_bar from Source
           [Tick 0040] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0040] Transporter 0 delivered to <5, 0>: 2 x metal_bar
         */
        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "metal_bar", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "metal_bar", });

        Assert.Contains(logs, l => l is TransportAssignedLog { ResourceId: "metal_bar", });
        Assert.Contains(logs, l => l is PickupLog pl && pl.PickedUp.Any(p => p.Resource.Id == "metal_bar"));
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(d => d.Resource.Id == "metal_bar"));
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "metal_bar", });

        Assert.Contains(logs, l => l is ProductionStartedLog { ResourceId: "computer_part", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "computer_part", });

        // --- Ordered Tick Extraction
        int TickOf<T>(Func<T, bool> filter) where T : class, ILogLine =>
            logs.OfType<T>().First(filter).Tick;

        var tProductionStart = TickOf<ProductionStartedLog>(l => l.ResourceId == "metal_bar");
        var tProductionComplete = TickOf<ProductionCompletedLog>(l => l.ResourceId == "metal_bar");
        var tTransportAssigned = TickOf<TransportAssignedLog>(l => l.ResourceId == "metal_bar");
        var tPickup = TickOf<PickupLog>(l => l.PickedUp.Any(p => p.Resource.Id == "metal_bar"));
        var tDelivery = TickOf<DeliveryLog>(l => l.Delivered.Any(p => p.Resource.Id == "metal_bar"));
        var tReceived = TickOf<TransportReceivedLog>(l => l.ResourceId == "metal_bar");
        var tCompStart = TickOf<ProductionStartedLog>(l => l.ResourceId == "computer_part");
        var tCompComplete = TickOf<ProductionCompletedLog>(l => l.ResourceId == "computer_part");

        // --- Ensure Correct Order
        Assert.True(tProductionStart < tProductionComplete, "metal_bar must be completed after it starts");
        Assert.True(tProductionComplete <= tTransportAssigned, "transport must be assigned after metal_bar is available"); //FAIL
        Assert.True(tTransportAssigned <= tPickup, "pickup should occur after assignment");
        Assert.True(tPickup <= tDelivery, "delivery should follow pickup");
        Assert.True(tDelivery <= tReceived, "facility should receive after delivery");
        Assert.True(tReceived <= tCompStart, "computer_part job must wait for all inputs");
        Assert.True(tCompStart <= tCompComplete, "job must complete after starting");

        // --- Sanity check (optional)
        var allFormatted = string.Join(Environment.NewLine, logs.Select(l => l.Format()));
        Assert.Contains("computer_part", allFormatted);
    }

    [Fact]
    public void Transporter_ProducesAndDelivers_AiModule_FromChainedStations()
    {
        var gameData = GameData.GetDefault();

        var ore = gameData.GetResource("ore");
        var energy = gameData.GetResource("energy_cell");
        var sand = gameData.GetResource("sand");
        var plastic = gameData.GetResource("plastic");
        var aiModule = gameData.GetResource("ai_module");

        // --- Station A: Metal Bar production
        var aStorage = new ResourceStorage();
        aStorage.Add(ore, 100);
        aStorage.Add(energy, 100);
        var a = new ProductionFacility(aStorage, new() { { gameData.GetRecipe("recipe_metal_bar"), 2 }, }) { Position = new Vector2(0, 0), Name = "StationA", };

        // --- Station B: Computer Part
        var bStorage = new ResourceStorage();
        bStorage.Add(plastic, 10);
        var b = new ProductionFacility(bStorage, new() { { gameData.GetRecipe("recipe_computer_part"), 1 }, }) { Position = new Vector2(5, 0), Name = "StationB", };

        // --- Station C: Silicon Wafer
        var cStorage = new ResourceStorage();
        cStorage.Add(sand, 100);
        cStorage.Add(energy, 100);
        var c = new ProductionFacility(cStorage, new() { { gameData.GetRecipe("recipe_silicon_wafer"), 2 }, }) { Position = new Vector2(10, 0), Name = "StationC", };

        // --- Station D: Final AI Module
        var dStorage = new ResourceStorage();
        var d = new ProductionFacility(dStorage, new() { { gameData.GetRecipe("recipe_ai_module"), 1 }, }) { Position = new Vector2(15, 0), Name = "StationD", };

        // --- Single Transporter
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1f, MaxVolume = 50f, Id = 42, };

        // --- Setup
        gameData.Facilities.AddRange([a, b, c, d,]);
        gameData.Transporters.Add(transporter);

        var ticker = new Ticker { GameData = gameData, };
        ticker.Register(a);
        ticker.Register(b);
        ticker.Register(c);
        ticker.Register(d);
        ticker.Register(transporter);

        // --- Run simulation
        ticker.RunTicks(481);

        // --- Verify final output
        var logs = gameData.GetAllLogs().ToList();
        //var formatted = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0001] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0001] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0001] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0007] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0007] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0007] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0007] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0007] Transporter 42 assigned to deliver 2 x silicon_wafer from StationC(<10, 0>) to StationD(<15, 0>)
           [Tick 0011] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0011] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0011] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0011] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0013] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0013] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0013] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0013] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0016] Transporter 42 picked up: 2 x silicon_wafer from StationC
           [Tick 0019] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0019] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0019] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0019] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0021] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0021] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0021] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0021] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0021] Received 2 of silicon_wafer from Transporter at <15, 0>
           [Tick 0021] Transporter 42 delivered to <15, 0>: 2 x silicon_wafer
           [Tick 0022] Transporter 42 assigned to deliver 2 x metal_bar from StationA(<0, 0>) to StationB(<5, 0>)
           [Tick 0025] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0025] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0025] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0025] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0031] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0031] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0031] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0031] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0031] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0031] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0031] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0031] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0036] Transporter 42 picked up: 2 x metal_bar from StationA
           [Tick 0037] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0037] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0037] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0037] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0041] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0041] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0041] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0041] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0041] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0041] Transporter 42 delivered to <5, 0>: 2 x metal_bar
           [Tick 0042] Started job for computer_part (duration: 10) at <5, 0>
           [Tick 0042] Transporter 42 assigned to deliver 2 x metal_bar from StationA(<0, 0>) to StationB(<5, 0>)
           [Tick 0043] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0043] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0043] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0043] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0046] Transporter 42 picked up: 2 x metal_bar from StationA
           [Tick 0049] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0049] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0049] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0049] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0051] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0051] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0051] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0051] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0051] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0051] Transporter 42 delivered to <5, 0>: 2 x metal_bar
           [Tick 0052] Completed job for computer_part, output added to storage at <5, 0>
           [Tick 0052] Started job for computer_part (duration: 10) at <5, 0>
           [Tick 0052] Transporter 42 assigned to deliver 1 x computer_part from StationB(<5, 0>) to StationD(<15, 0>)
           [Tick 0052] Transporter 42 picked up: 1 x computer_part from StationB
           [Tick 0055] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0055] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0055] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0055] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0061] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0061] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0061] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0061] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0061] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0061] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0061] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0061] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0062] Completed job for computer_part, output added to storage at <5, 0>
           [Tick 0062] Received 1 of computer_part from Transporter at <15, 0>
           [Tick 0062] Transporter 42 delivered to <15, 0>: 1 x computer_part
           [Tick 0063] Started job for ai_module (duration: 12) at <15, 0>
           [Tick 0063] Transporter 42 assigned to deliver 2 x metal_bar from StationA(<0, 0>) to StationB(<5, 0>)
           [Tick 0067] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0067] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0067] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0067] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0071] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0071] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0071] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0071] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0073] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0073] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0073] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0073] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0075] Completed job for ai_module, output added to storage at <15, 0>
           [Tick 0077] Transporter 42 picked up: 2 x metal_bar from StationA
           [Tick 0079] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0079] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0079] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0079] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0081] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0081] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0081] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0081] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0082] Received 2 of metal_bar from Transporter at <5, 0>
           [Tick 0082] Transporter 42 delivered to <5, 0>: 2 x metal_bar
           [Tick 0083] Started job for computer_part (duration: 10) at <5, 0>
           [Tick 0083] Transporter 42 assigned to deliver 1 x computer_part from StationB(<5, 0>) to StationD(<15, 0>)
           [Tick 0083] Transporter 42 picked up: 1 x computer_part from StationB
           [Tick 0085] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0085] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0085] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0085] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0091] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0091] Completed job for metal_bar, output added to storage at <0, 0>
           [Tick 0091] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0091] Started job for metal_bar (duration: 10) at <0, 0>
           [Tick 0091] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0091] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0091] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0091] Started job for silicon_wafer (duration: 6) at <10, 0>
           [Tick 0093] Completed job for computer_part, output added to storage at <5, 0>
           [Tick 0093] Received 1 of computer_part from Transporter at <15, 0>
           [Tick 0093] Transporter 42 delivered to <15, 0>: 1 x computer_part
           [Tick 0094] Transporter 42 assigned to deliver 2 x metal_bar from StationA(<0, 0>) to StationB(<5, 0>)
           [Tick 0097] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0097] Completed job for silicon_wafer, output added to storage at <10, 0>
           [Tick 0097] Started job for silicon_wafer (duration: 6) at <10, 0>         
         */

        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "metal_bar", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "computer_part", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "silicon_wafer", });
        Assert.Contains(logs, l => l is ProductionCompletedLog { ResourceId: "ai_module", });
        Assert.Equal(10, dStorage.GetAmount(aiModule));
        Assert.Contains(logs, l => l is DeliveryLog dl && dl.Delivered.Any(resourceAmount => resourceAmount.Resource.Id == "ai_module") == false); // only intermediate deliveries
        Assert.Contains(logs, l => l is TransportReceivedLog { ResourceId: "computer_part", });

        foreach (var t in gameData.Transporters)
        {
            Assert.Empty(t.Carrying);
            Assert.False(t.HasActiveTask());
        }

        ticker.RunTicks(1000);
        Assert.Equal(10, dStorage.GetAmount(aiModule));
        Assert.Equal(465, transporter.DistanceTraveled, 0.1f); //arbitrary and can change in the future
    }

    [Fact]
    public void Transporter_LogsFailedDelivery_WhenExpectedCargoIsMissing()
    {
        var gameData = GameData.GetDefault();
        var metalBar = gameData.GetResource("metal_bar");
        var plastic = gameData.GetResource("plastic");

        // --- Source and destination setup
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(metalBar, 5);
        var source = new ProductionFacility(sourceStorage, []) { Position = new Vector2(0, 0), Name = "Source", };

        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, []) { Position = new Vector2(5, 0), Name = "Destination", };

        // --- Transporter
        var transporter = new Transporter { Position = new Vector2(0, 0), SpeedPerTick = 1f, MaxVolume = 10f, Id = 101, };

        // Use NewAssignTask instead of AssignTask
        transporter.AssignTask(source, dest, [new ResourceAmount(metalBar, 3),], currentTick: 0);

        // Sneak plastic into inventory before delivery (simulate weird state)
        transporter.Carrying.Add(new ResourceAmount(plastic, 1));

        var ticker = new Ticker { GameData = gameData, };
        gameData.Transporters.Add(transporter);
        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);

        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // --- Tick: reach destination and attempt delivery
        ticker.RunTicks(2); // transporter reaches destination and delivers

        transporter.Carrying.RemoveAll(r => r.Resource == metalBar); //remove items from Transport, would normally never happen
        ticker.RunTicks(10); // transporter reaches destination and delivers

        // --- Validate failed delivery
        var logs = gameData.GetAllLogs();
        //var formattedLogs = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Transporter 101 picked up: 3 x metal_bar from Source
           [Tick 0006] Transporter 101 failed to deliver: 3 x metal_bar to Destination     
         */

        var failedLog = logs.OfType<DeliveryFailedLog>().FirstOrDefault();

        Assert.NotNull(failedLog);
        Assert.Equal(101, failedLog.TransporterId);
        Assert.Contains(failedLog.Failed, r => r.Resource == metalBar);
        Assert.Equal(dest, failedLog.Facility);

        // Ensure the undelivered plastic is still being carried
        Assert.Contains(transporter.Carrying, c => c.Resource == plastic && c.Amount == 1);
        Assert.Equal(5, transporter.DistanceTraveled, 0.1f);
        //Assert.True(transporter.HasActiveTask(), "Transporter should still have an active task.");
    }

    [Fact]
    public void ResourceStorage_TracksIncomingCorrectly_DuringSlowTransport_ManualAssignment()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");

        // --- Source
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 100);
        var source = new ProductionFacility(sourceStorage, []) { Name = "Source", Position = new Vector2(0, 0), };

        // --- Destination (empty at start)
        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, []) { Name = "Dest", Position = new Vector2(100, 0), };

        // --- Transporter (speed = 1 → 100 ticks to travel 100 units)
        var transporter = new Transporter { Id = 1, Position = new Vector2(0, 0), SpeedPerTick = 1f, MaxVolume = 100f, };

        // --- Setup system
        var ticker = new Ticker { GameData = gameData, };
        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // --- Assign transport of 10 ore
        transporter.AssignTask(source, dest, [new ResourceAmount(ore, 10),], currentTick: 0); //This is a bit more manual than I'd like

        // --- Run for 50 ticks (halfway)
        ticker.RunTicks(50);

        // --- Assert storage states
        Assert.Equal(90, sourceStorage.GetAmount(ore)); // ore removed
        Assert.Equal(0, destStorage.GetAmount(ore)); // not delivered yet
        Assert.Equal(10, destStorage.GetIncomingAmount(ore)); // ore is on the way
        Assert.Equal(10, destStorage.GetTotalIncludingIncoming(ore)); // correct combined view

        ticker.RunTicks(55);

        Assert.Equal(10, destStorage.GetAmount(ore));
        Assert.Equal(0, destStorage.GetIncomingAmount(ore));
        Assert.Equal(10, destStorage.GetTotalIncludingIncoming(ore));
    }

    [Fact]
    public void ResourceStorage_TracksIncomingCorrectly_DuringSlowTransport_AutoAssigned()
    {
        var gameData = GameData.GetDefault();
        var ore = gameData.GetResource("ore");
        var oreRecipe = gameData.GetRecipe("recipe_metal_bar");

        // --- Source with ore and a dummy recipe just to be a valid facility
        var sourceStorage = new ResourceStorage();
        sourceStorage.Add(ore, 34);
        var source = new ProductionFacility(sourceStorage, []) { Name = "Source", Position = new Vector2(0, 0), };

        // --- Destination has no ore but asks for it via PullRequestStrategy
        var destStorage = new ResourceStorage();
        var dest = new ProductionFacility(destStorage, new() { { oreRecipe, 1 }, }) { Name = "Dest", Position = new Vector2(100, 0), PullRequestStrategy = new SustainedProductionStrategy(ticks: 1000), };// Will request ore
        var transporter = new Transporter { Id = 1, Position = new Vector2(0, 0), SpeedPerTick = 1f, MaxVolume = 100f, }; // --- Transporter with just enough volume, slow movement

        // --- Register all
        var ticker = new Ticker { GameData = gameData, };
        gameData.Facilities.Add(source);
        gameData.Facilities.Add(dest);
        gameData.Transporters.Add(transporter);
        ticker.Register(source);
        ticker.Register(dest);
        ticker.Register(transporter);

        // --- Simulate decision logic that assigns tasks based on demand/supply
        ticker.RunTicks(50);

        // --- Mid-delivery: should be in transit
        Assert.Equal(01, sourceStorage.GetAmount(ore)); //FAIL this is 67 instead... why?
        Assert.Equal(00, destStorage.GetAmount(ore));
        Assert.Equal(33, destStorage.GetIncomingAmount(ore));
        Assert.Equal(33, destStorage.GetTotalIncludingIncoming(ore));
        Assert.Equal(33, transporter.Carrying.FirstOrDefault(r => r.Resource == ore)?.Amount ?? 0);

        // --- Complete delivery
        ticker.RunTicks(51);
        //var debugText = gameData.GetAllLogsFormatted();
        /*
           [Tick 0001] Transporter 1 assigned to deliver 33 x ore from Source(<0, 0>) to Dest(<100, 0>)
           [Tick 0001] Transporter 1 picked up: 33 x ore from Source
           [Tick 0101] Received 33 of ore from Transporter at <100, 0>
           [Tick 0101] Transporter 1 delivered to <100, 0>: 33 x ore
           [Tick 0102] Transporter 1 assigned to deliver 1 x ore from Source(<0, 0>) to Dest(<100, 0>)         
         */

        Assert.Equal(33, destStorage.GetAmount(ore));
        Assert.Equal(00, destStorage.GetIncomingAmount(ore));
        Assert.Equal(33, destStorage.GetTotalIncludingIncoming(ore));

        ticker.RunTicks(51);
        Assert.Equal(33, destStorage.GetAmount(ore));
        Assert.Equal(01, destStorage.GetIncomingAmount(ore));
        Assert.Equal(34, destStorage.GetTotalIncludingIncoming(ore));
    }
}