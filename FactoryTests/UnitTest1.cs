using FactoryCli;

namespace FactoryTests;

public class UnitTest1
{
    [Fact]
    public void Factory_Produces_MetalBar_Once_Every_10_Ticks()
    {
        var storage = new ResourceStorage();
        storage.Add(ResourceType.Ore, 2);
        storage.Add(ResourceType.EnergyCell, 1);

        var factory = new Factory(storage);
        var ticker = new Ticker();
        ticker.Register(factory);

        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));
        ticker.RunTicks(10);
        Assert.Equal(1, storage.GetAmount(ResourceType.MetalBar));
    }
}