namespace Factory.Core;

public class Ticker
{
    private readonly List<IUpdatable> _tickables = [];
    public int CurrentTick { get; private set; }
    public void Register(IUpdatable tickable) => _tickables.Add(tickable);
    public void RegisterAll()
    {
        if (GameData == null) { return; }
        foreach (var f in GameData.Facilities) Register(f);
        foreach (var t in GameData.Transporters) Register(t);
        foreach (var f in GameData.Fighters) Register(f);
    }
    public GameData? GameData { get; set; }

    public void Tick()
    {
        CurrentTick++;
        //Add a new tick where we update what stations want, what's on the way, and what's expected to leave
        GameData?.DemandManagerTick();
        foreach (var tickable in _tickables)
        {
            GameData?.Tick(CurrentTick); //ToDo: This probably shouldn't be in the loop
            tickable.Tick(CurrentTick);
        }
    }

    public void RunTicks(int count)
    {
        for (var i = 0; i < count; i++) { Tick(); }
    }

    public void TryToRegisterEverything()
    {
        if (GameData == null) { return; }
        foreach (var f in GameData.Facilities) Register(f);
        foreach (var t in GameData.Transporters) Register(t);
        foreach (var f in GameData.Fighters) Register(f);
    }
}