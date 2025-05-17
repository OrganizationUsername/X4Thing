using FactoryCli;

namespace FactoryTests;

public class TickerTests
{

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsNull_WhenFacilityIsIdle()
    {
        var storage = new ResourceStorage();
        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>()); // no workshops

        Assert.Null(facility.GetTicksUntilNextEvent()); // No workshops or inputs, nothing possible
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsZero_WhenJobCanStartNow()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
        });

        Assert.Equal(0, facility.GetTicksUntilNextEvent()); // Can start now
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsTicksUntilNextCompletion()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Start job
        ticker.RunTicks(1); // Tick 1: job starts

        // Now, job has Elapsed = 0, needs 10 ticks
        Assert.Equal(10, facility.GetTicksUntilNextEvent());

        // Simulate 3 more ticks
        ticker.RunTicks(3); // now Elapsed = 3
        Assert.Equal(7, facility.GetTicksUntilNextEvent()); // 10 - 3 = 7
    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsSoonestCompletionOfMultipleJobs()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 8);
        storage.Add(ResourceType.EnergyCell, 4);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 2 }, // 2 jobs in parallel
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Run 1 tick to start both jobs
        ticker.RunTicks(1); // Tick 1: jobs started

        // Run 3 more ticks, Elapsed = 3
        ticker.RunTicks(3); // Now Elapsed = 4
        Assert.Equal(7, facility.GetTicksUntilNextEvent());


    }

    [Fact]
    public void GetTicksUntilNextEvent_ReturnsZero_WhenJobBecomesPossible()
    {
        var storage = new ResourceStorage();

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
        });

        Assert.Null(facility.GetTicksUntilNextEvent()); // No input yet

        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        Assert.Equal(0, facility.GetTicksUntilNextEvent()); // Can start job now
    }
}