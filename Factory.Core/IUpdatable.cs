namespace Factory.Core;

public interface IUpdatable
{
    void Tick(int currentTick);
}

public class Resource
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public float BaseValue { get; init; }
    public float Volume { get; init; }

    public override string ToString() => DisplayName;
}

public class Recipe
{
    public required string Id { get; init; }
    public Resource Output { get; init; } = null!;
    public int OutputAmount { get; init; }
    public Dictionary<Resource, int> Inputs { get; init; } = [];
    public int Duration { get; init; }
    public float Benefit { get; init; } //kind of a heuristic to help show that creating these things in-house is worth it because it gets us closer to some economic/endProduct goal
}
