using JetBrains.Annotations;
using System.Numerics;

namespace FactoryCli;

internal class Program
{
    private static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}

public interface IUpdatable
{
    void Tick(int currentTick);
}

public class Resource
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public float BaseValue { get; init; } = 1.0f;

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

public class ResourceStorage
{
    private readonly Dictionary<Resource, int> _resources = new();

    public IEnumerable<KeyValuePair<Resource, int>> DumpExcess()
    {
        foreach (var (res, amount) in _resources)
        {
            if (amount > 0) { yield return new KeyValuePair<Resource, int>(res, amount); }
        }
    }

    public void Add(Resource type, int amount)
    {
        _resources.TryAdd(type, 0);
        _resources[type] += amount;
    }

    public bool Consume(Resource type, int amount)
    {
        if (!_resources.TryGetValue(type, out var current) || current < amount) { return false; }

        _resources[type] -= amount;
        return true;
    }

    public int GetAmount(Resource type) => _resources.GetValueOrDefault(type, 0);
}

public class ResourceAmount(Resource resource, int amount)
{
    public Resource Resource { get; set; } = resource;
    public int Amount { get; set; } = amount;

    public override string ToString() => $"{Amount} x {Resource.Id}";
}

public class Transporter : IUpdatable
{
    public Vector2 Position { get; set; }
    public float SpeedPerTick { get; set; } = 1f;
    public string Log { get; private set; } = ""; //probably replace this with List<string>

    public List<ResourceAmount> Carrying { get; } = [];

    private (ProductionFacility Source, ProductionFacility Dest, List<ResourceAmount> Cargo)? _task; //This isn't good. I think it should be an actual queue instead.
    private Vector2? _target;

    public void AssignTask(ProductionFacility from, ProductionFacility to, List<ResourceAmount> cargo)
    {
        _task = (from, to, cargo);
        Carrying.Clear();
        _target = from.Position;

        Log += $"Assigned to move {string.Join(", ", cargo)} from {from.Position} to {to.Position}\n";
    }

    public void Tick(int tick)
    {
        if (_task == null || _target == null) return;

        var (from, to, cargo) = _task.Value;

        var direction = _target.Value - Position;
        var distance = direction.Length();

        if (!(distance <= SpeedPerTick)) { Position += Vector2.Normalize(direction) * SpeedPerTick; return; }

        Position = _target.Value;

        if (Carrying.Count == 0) // Pickup. This isn't quite right. In the future, I might want to pick up 5 things along the way.
        {
            foreach (var item in cargo)
            {
                if (from.TryExport(item.Resource, item.Amount)) { Carrying.Add(new ResourceAmount(item.Resource, item.Amount)); }
                else { Log += $"[Tick {tick}] Failed to pick up {item.Amount} x {item.Resource.Id}\n"; }
            }

            _target = to.Position;
            Log += $"[Tick {tick}] Picked up {string.Join(", ", Carrying)}\n";
        }
        else // Deliver
        {
            foreach (var item in Carrying) { to.ReceiveImport(item.Resource, item.Amount); }

            Log += $"[Tick {tick}] Delivered {string.Join(", ", Carrying)}\n";
            Carrying.Clear();
            _task = null;
            _target = null;
        }
    }
}

public class ProductionFacility : IUpdatable
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<Recipe, int> _workshops;
    private readonly Dictionary<Recipe, List<ProductionJob>> _activeJobs;
    public List<string> DebugLog { get; } = [];

    public Vector2 Position { get; set; } = new(0, 0);

    public ProductionFacility(ResourceStorage storage, Dictionary<Recipe, int> recipeWorkshopAssignments)
    {
        _storage = storage;
        _workshops = new();
        _activeJobs = new();

        foreach (var (recipe, count) in recipeWorkshopAssignments)
        {
            _workshops[recipe] = count;
            _activeJobs[recipe] = [];
        }
    }

    public bool TryExport(Resource res, int amt) => _storage.Consume(res, amt);
    public void ReceiveImport(Resource res, int amt) => _storage.Add(res, amt);


    [UsedImplicitly] public string GetDebugLog() => string.Join(Environment.NewLine, DebugLog);

    public void AddWorkshops(Recipe recipe, int count)
    {
        if (!_workshops.TryAdd(recipe, count)) { _workshops[recipe] += count; }
        if (!_activeJobs.ContainsKey(recipe)) { _activeJobs[recipe] = []; }
        DebugLog.Add($"  Added {count} workshop(s) for {recipe.Output.Id}");
    }

    public void Tick(int currentTick)
    {
        var tempLog = new List<string>();

        foreach (var (recipe, jobs) in _activeJobs)
        {
            var output = recipe.Output;

            // Step 1: Progress jobs
            for (var i = jobs.Count - 1; i >= 0; i--)
            {
                var job = jobs[i];
                job.Elapsed++;

                tempLog.Add($"  Job for {output.Id} ticked to {job.Elapsed}/{recipe.Duration}");

                if (job.Elapsed >= recipe.Duration)
                {
                    _storage.Add(output, recipe.OutputAmount);
                    jobs.RemoveAt(i);
                    tempLog.Add($"  Completed job for {output.Id}, output added to storage");
                }
            }

            // Step 2: Start new jobs
            var availableWorkshops = _workshops[recipe] - jobs.Count;
            for (var i = 0; i < availableWorkshops; i++)
            {
                if (CanConsumeInputs(recipe.Inputs))
                {
                    ConsumeInputs(recipe.Inputs);
                    jobs.Add(new ProductionJob());
                    tempLog.Add($"  Started job for {output.Id} (duration: {recipe.Duration})");
                }
                else { break; }
            }
        }

        if (tempLog.Count > 0)
        {
            DebugLog.Add($"[Tick {currentTick}]");
            DebugLog.AddRange(tempLog);
        }
    }
    private bool CanConsumeInputs(Dictionary<Resource, int> inputs)
    {
        foreach (var (resource, amount) in inputs)
        {
            if (_storage.GetAmount(resource) < amount) { return false; }
        }
        return true;
    }

    private void ConsumeInputs(Dictionary<Resource, int> inputs)
    {
        foreach (var (resource, amount) in inputs)
        {
            _storage.Consume(resource, amount);
        }
    }


    private class ProductionJob
    {
        public int Elapsed;
    }

    public IEnumerable<(Resource resource, int amount)> GetPushOffers()
    {
        foreach (var (res, amt) in _storage.DumpExcess())
        {
            yield return (res, amt);
        }
    }



    public IEnumerable<(Resource resource, int amount)> GetPullRequests()
    {
        foreach (var (recipe, _) in _workshops)
        {
            foreach (var input in recipe.Inputs)
            {
                var current = _storage.GetAmount(input.Key);
                if (current < input.Value) { yield return (input.Key, input.Value - current); }
            }
        }
    }

    public int? GetTicksUntilNextEvent()
    {
        int? soonestCompletion = null;

        foreach (var (recipe, jobs) in _activeJobs)
        {
            var availableWorkshops = _workshops[recipe] - jobs.Count;

            // Completion
            foreach (var job in jobs)
            {
                var ticksLeft = recipe.Duration - job.Elapsed;
                if (soonestCompletion == null || ticksLeft < soonestCompletion) { soonestCompletion = ticksLeft; }
            }

            // Can start?
            if (availableWorkshops > 0 && CanConsumeInputs(recipe.Inputs)) { return 0; }
        }

        return soonestCompletion;
    }
}