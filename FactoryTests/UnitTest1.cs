using FactoryCli;

namespace FactoryTests;

public class UnitTest1
{
    [Fact]
    public void ProductionFacility_DoesNotStartJobs_WhenResourcesAreInsufficient()
    {
        var storage = new ResourceStorage(); // No inputs

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(20);

        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));
    }

    [Fact]
    public void ProductionFacility_UsesOnlyEnoughWorkshops_AsResourcesAllow()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 4);         // Enough for 2 jobs
        storage.Add(ResourceType.EnergyCell, 1);  // Only enough for 1 job

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 5 }, // 5 workshops, but not enough resources
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(11);

        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar)); // Only 1 job ran
    }

    [Fact]
    public void ProductionFacility_DoesNotStarve_OneRecipe_WhenSharingInputs()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);
        storage.Add(ResourceType.Flour, 1);
        storage.Add(ResourceType.Wheat, 2);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
            { ResourceType.Bread, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(11); // Bread done at 9, MetalBar at 11

        Assert.Equal(1, storage.GetAmount(ResourceType.Bread));
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));
    }


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

        ticker.RunTicks(10); // Run 10 ticks — MetalBar job still in progress (Elapsed = 9)
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        ticker.RunTicks(1); // Tick 11 — job completes, output added
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));

        // Run more ticks — no new resources, no new jobs
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));
    }

    [Fact]
    public void ProductionFacility_Uses_Multiple_Workshops_To_Produce_Parallel()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 10);
        storage.Add(ResourceType.EnergyCell, 5);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 2 }, // Two workshops = two jobs in parallel
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        ticker.RunTicks(10); // Tick 1–10: Jobs in progress
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar)); // Still running

        ticker.RunTicks(1); // Tick 11: Both jobs complete
        Assert.Equal(2, storage.GetAmount(ResourceType.MetalBar));
    }

    [Fact]
    public void Facility_Produces_Bread_And_MetalBar_Simultaneously()
    {
        var storage = new ResourceStorage();
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

        // Tick 1–8: Jobs progressing, nothing completed yet
        ticker.RunTicks(8);
        Assert.Equal(0, storage.GetAmount(ResourceType.Bread));
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        // Tick 9: Bread completes
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(ResourceType.Bread));
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        // Tick 10: MetalBars still not done
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(ResourceType.Bread));
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        // Tick 11: MetalBars complete
        ticker.RunTicks(1);
        Assert.Equal(2, storage.GetAmount(ResourceType.MetalBar));
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

        // Tick 1–5: no resources
        ticker.RunTicks(5);
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar));

        // Add inputs after tick 5
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        // Tick 6–15: 10 ticks (elapsed = 9)
        ticker.RunTicks(10);
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar)); // still in progress

        // Tick 16: job completes
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));

        // Add input for second MetalBar
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        // Tick 17–26: second job in progress
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar)); // not done yet

        // Tick 27: second job completes
        ticker.RunTicks(1);
        Assert.Equal(2, storage.GetAmount(ResourceType.MetalBar));
    }

    [Fact]
    public void ProductionFacility_Produces_ComputerPart_From_Upstream_MetalBars()
    {
        var storage = new ResourceStorage();

        // Arrange: we start with:
        // - 1 pre-existing MetalBar
        // - resources to produce 1 more MetalBar (Ore + EnergyCell)
        // - 1 Plastic (ComputerPart requires 2 MetalBars + 1 Plastic)
        storage.Add(ResourceType.MetalBar, 1);
        storage.Add(ResourceType.Plastic, 1);
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        var facility = new ProductionFacility(storage, new Dictionary<ResourceType, int>
        {
            { ResourceType.MetalBar, 1 },
            { ResourceType.ComputerPart, 1 },
        });

        var ticker = new Ticker();
        ticker.Register(facility);

        // Tick 1–10: MetalBar is still in progress
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar)); // Only the original bar is still available
        Assert.Equal(0, storage.GetAmount(ResourceType.ComputerPart)); // Not started

        // Tick 11: MetalBar #2 finishes, ComputerPart starts
        ticker.RunTicks(1);
        Assert.Equal(0, storage.GetAmount(ResourceType.MetalBar)); // Both bars consumed
        Assert.Equal(0, storage.GetAmount(ResourceType.ComputerPart)); // Still in progress

        // Tick 12–21: ComputerPart in progress
        ticker.RunTicks(9);
        Assert.Equal(0, storage.GetAmount(ResourceType.ComputerPart)); // Still not done

        // Tick 22: ComputerPart completes
        ticker.RunTicks(1);
        Assert.Equal(1, storage.GetAmount(ResourceType.ComputerPart)); // ✅ Done
    }
}