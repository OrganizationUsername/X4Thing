namespace Factory.Core;

public class Ticker
{
    private readonly List<IUpdatable> _tickables = [];
    public int CurrentTick { get; private set; }

    public void Register(IUpdatable tickable) => _tickables.Add(tickable);
    public GameData? GameData { get; set; }


    public void Tick()
    {
        CurrentTick++;
        foreach (var tickable in _tickables)
        {
            GameData?.Tick(CurrentTick); //This probably shouldn't be in the loop
            tickable.Tick(CurrentTick);
        }
    }

    public void RunTicks(int count)
    {
        for (var i = 0; i < count; i++) { Tick(); }
    }
}