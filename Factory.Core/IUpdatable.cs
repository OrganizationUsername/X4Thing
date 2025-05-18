namespace Factory.Core;

public interface IUpdatable
{
    void Tick(int currentTick);
}


public interface IHasName
{
    public string Name { get; set; }
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
    public float Benefit { get; init; } //kind of a heuristic to help show that creating these things in-house is worth it because it gets us closer to some economic/endProduct goal
}
