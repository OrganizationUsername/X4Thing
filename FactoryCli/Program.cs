namespace FactoryCli;

internal class Program
{
    private static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}

//ToDo: Make it so the logs from the production facilities and transporters are stored in a sortable way so I can sort them by tick and see what happened at a certain time.
//ToDo: Add volume to resources

public interface IUpdatable
{
    void Tick(int currentTick);
}

public class Resource
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public float BaseValue { get; init; } = 1.0f;
    public float Volume { get; init; } = 1.0f;

    public override string ToString() => DisplayName;
}
public class Recipe
{
    public string Id { get; init; } = "";
    public Resource Output { get; init; } = null!;
    public int OutputAmount { get; init; }
    public Dictionary<Resource, int> Inputs { get; init; } = new();
    public int Duration { get; init; }
}