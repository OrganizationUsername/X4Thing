namespace FactoryCli;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}

public interface IUpdatable
{
    void Tick(int currentTick);
}

public enum ResourceType
{
    Ore,
    EnergyCell,
    MetalBar,
}

public class ResourceStorage
{
    private readonly Dictionary<ResourceType, int> _resources = new();

    public void Add(ResourceType type, int amount)
    {
        _resources.TryAdd(type, 0);
        _resources[type] += amount;
    }

    public bool Consume(ResourceType type, int amount)
    {
        if (!_resources.TryGetValue(type, out var current) || current < amount) { return false; }

        _resources[type] -= amount;
        return true;
    }

    public int GetAmount(ResourceType type) => _resources.GetValueOrDefault(type, 0);
}

public class Factory(ResourceStorage storage) : IUpdatable //this syntax works. Always use this.
{
    private int _progress = 0;

    public void Tick(int currentTick)
    {
        //This is way too hardcoded. Make it so I can pass in recipes or something.
        if (_progress > 0)
        {
            _progress++;
            if (_progress < 10) { return; }

            storage.Add(ResourceType.MetalBar, 1);
            _progress = 0;
            return;
        }

        if (storage.Consume(ResourceType.Ore, 2) && storage.Consume(ResourceType.EnergyCell, 1)) { _progress = 1; }
    }
}

public class Ticker
{
    private readonly List<IUpdatable> _tickables = [];
    public int CurrentTick { get; private set; } = 0;

    public void Register(IUpdatable tickable) => _tickables.Add(tickable);

    public void Tick()
    {
        CurrentTick++;
        foreach (var tickable in _tickables)
        {
            tickable.Tick(CurrentTick);
        }
    }

    public void RunTicks(int count)
    {
        for (var i = 0; i < count; i++) { Tick(); }
    }
}
