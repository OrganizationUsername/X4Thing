using FactoryCli;

namespace FactoryTests;

public class UnitTest1
{
    [Fact]
    public void ProductionFacility_Produces_One_MetalBar_With_Sufficient_Resources_And_One_Workshop()
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

        ticker.RunTicks(10);

        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));

        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar)); // No more resources for additional production
    }

    [Fact]
    public void ProductionFacility_Uses_Multiple_Workshops_To_Produce_Parallel()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 10);
        storage.Add(ResourceType.EnergyCell, 5);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 2 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(10);

        Assert.Equal(2, storage.GetAmount(ResourceType.MetalBar)); // Two jobs completed in parallel
    }


    [Fact]
    public void Facility_Produces_Bread_And_MetalBar_Simultaneously()
    {
        var storage = new ResourceStorage();

        // Enough for 1 bread and 2 metal bars
        storage.Add(ResourceType.Wheat, 2);
        storage.Add(ResourceType.Flour, 1);
        storage.Add(ResourceType.Ore, 4);
        storage.Add(ResourceType.EnergyCell, 2);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 2 },
            { ResourceType.Bread, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(10);

        Assert.Equal(2, storage.GetAmount(ResourceType.MetalBar));
        Assert.Equal(1, storage.GetAmount(ResourceType.Bread));
    }

    [Fact]
    public void ProductionFacility_Starts_Production_When_Resources_Arrive_MidTick()
    {
        var storage = new ResourceStorage();

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(5);
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        // Inject inputs at tick 5
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));
    }

    [Fact]
    public void ProductionFacility_Produces_ComputerPart_From_Upstream_MetalBars()
    {
        var storage = new ResourceStorage();

        // Only 1 metal bar to start, so we need to produce the second during the test
        storage.Add(ResourceType.MetalBar, 1);
        storage.Add(ResourceType.Plastic, 1);
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },     // Can produce one more MetalBar
            { ResourceType.ComputerPart, 1 }, // Needs 2 MetalBars + Plastic
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Total time breakdown:
        // - MetalBar takes 10 ticks
        // - Once 2nd bar is produced, ComputerPart can begin
        // - ComputerPart takes 12 ticks
        // → We expect result after 22 ticks
        ticker.RunTicks(20);

        Assert.Equal(1, storage.GetAmount(ResourceType.ComputerPart));
    }

}