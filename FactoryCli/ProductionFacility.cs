using System.Numerics;
using JetBrains.Annotations;

namespace FactoryCli;

public class ProductionFacility : IUpdatable, IHasName
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<Recipe, int> _workshops;
    private readonly Dictionary<Recipe, List<ProductionJob>> _activeJobs;
    public int PlayerId { get; set; } = 0;
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "Production";
    public List<string> DebugLog { get; } = []; //ToDo: Make another log that is `List<(int tick, string log)>` so I can sort it by tick and see what happened at a certain time. Maybe not `string log`, though. Maybe it's a structured type that's easier to query.

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

    public bool TryExport(Resource res, int amountToTake, int tick, IHasName receiver)
    {
        DebugLog.Add($"[Tick {tick}] Exporting {amountToTake} of {res.Id} to {receiver.Name}");
        return _storage.Consume(res, amountToTake);
    }

    public void ReceiveImport(Resource res, int amountToTransfer, int tick, IHasName giver)
    {
        DebugLog.Add($"[Tick {tick}] Received {amountToTransfer} of {res.Id} from {giver.Name}");
        _storage.Add(res, amountToTransfer);
    }

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

                //tempLog.Add($"  Job for {output.Id} ticked to {job.Elapsed}/{recipe.Duration}");

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
            DebugLog.AddRange(tempLog); //These should probably be records with an interface or something so I can sort them by tick and see what happened at a certain time and filter by event types.
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
        // Collect all input resources used by this facility’s recipes
        var inputResources = _workshops.Keys
            .SelectMany(recipe => recipe.Inputs.Keys)
            .ToHashSet();

        // Only offer to push resources not used as inputs, for now. 
        foreach (var (res, amount) in _storage.GetAll())
        {
            if (!inputResources.Contains(res) && amount > 0)
            {
                yield return (res, amount);
            }
        }
    }

    public IEnumerable<(Resource resource, int amount)> GetPullRequests() //maybe this should take in a number of ticks. For example: we can say that we want to send a transport to satisfy it for 500 ticks.
    {
        //We can also look for an imbalance in resources.
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
            foreach (var job in jobs)
            {
                var ticksLeft = recipe.Duration - job.Elapsed;
                if (soonestCompletion == null || ticksLeft < soonestCompletion) { soonestCompletion = ticksLeft; }
            }
            if (availableWorkshops > 0 && CanConsumeInputs(recipe.Inputs)) { return 0; }
        }
        return soonestCompletion;
    }


}

public class ResourceStorage
{
    private readonly Dictionary<Resource, int> _resources = new();
    public Dictionary<Resource, int> GetAll() => new(_resources);

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