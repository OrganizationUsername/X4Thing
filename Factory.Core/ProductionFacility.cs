using System.Numerics;

namespace Factory.Core;

public class Entity
{
    public List<ILogLine> LogLines { get; } = [];
    public int Id { get; set; }
    public string Name { get; set; } = "Ship";
    public Vector2 Position { get; set; }
    public int PlayerId { get; set; } = 0; //for factionId later

}

public class ProductionFacility : Entity, IUpdatable, IHasName
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<Recipe, int> _workshops;
    private readonly Dictionary<Recipe, List<ProductionJob>> _activeJobs;

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

    public Dictionary<Recipe, List<ProductionJob>> GetProductionJobs() => _activeJobs;
    public ResourceStorage GetStorage() => _storage;
    public Dictionary<Recipe, int> GetWorkshops() => _workshops;
    public IPullRequestStrategy PullRequestStrategy { get; set; } = new DefaultPullRequestStrategy();

    public bool TryExport(Resource res, int amountToTake, int tick, Transporter receiver) => _storage.Consume(res, amountToTake);

    public void ReceiveImport(Resource res, int amountToTransfer, int tick, Transporter transporter)
    {
        LogLines.Add(new TransportReceivedLog(tick, Id, res.Id, amountToTransfer, Position, transporter));
        _storage.Add(res, amountToTransfer);
    }

    public void AddWorkshops(Recipe recipe, int count)
    {
        if (!_workshops.TryAdd(recipe, count)) { _workshops[recipe] += count; }
        if (!_activeJobs.ContainsKey(recipe)) { _activeJobs[recipe] = []; }
        LogLines.Add(new WorkshopAddedLog(0, Id, recipe.Output.Id, count, Position));
    }

    public void Tick(int currentTick)
    {

        foreach (var (recipe, jobs) in _activeJobs)
        {
            var output = recipe.Output;

            // Step 1: Progress jobs
            for (var i = jobs.Count - 1; i >= 0; i--)
            {
                var job = jobs[i];
                job.Elapsed++;

                if (job.Elapsed < recipe.Duration) { continue; }

                _storage.Add(output, recipe.OutputAmount);
                jobs.RemoveAt(i);
                LogLines.Add(new ProductionCompletedLog(currentTick, Id, output.Id, recipe.OutputAmount, Position));
            }

            // Step 2: Start new jobs
            var availableWorkshops = _workshops[recipe] - jobs.Count;
            for (var i = 0; i < availableWorkshops; i++)
            {
                if (CanConsumeInputs(recipe.Inputs))
                {
                    ConsumeInputs(recipe.Inputs);
                    jobs.Add(new ProductionJob());
                    LogLines.Add(new ProductionStartedLog(currentTick, Id, output.Id, recipe.Duration, Position));
                }
                else { break; }
            }
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
        foreach (var (resource, amount) in inputs) { _storage.Consume(resource, amount); }
    }


    public class ProductionJob
    {
        public int Elapsed;
    }

    public IEnumerable<(Resource resource, int amount)> GetPushOffers()
    {
        var inputResources = _workshops.Keys
            .SelectMany(recipe => recipe.Inputs.Keys)
            .ToHashSet();

        // Only offer to push resources not used as inputs, for now. Later I'll have another strategy that allows me to figure out what to give up.
        foreach (var (res, amount) in _storage.GetAll())
        {
            if (!inputResources.Contains(res) && amount > 0)
            {
                yield return (res, amount);
            }
        }
    }

    public IEnumerable<(Resource resource, int amount)> GetPullRequests()
    {
        var result = PullRequestStrategy.GetRequests(this).ToList();
        LastRequests = result.Select(r => new ResourceRequest(r.resource, r.amount)).ToList();
        return result;
    }

    public List<ResourceRequest> LastRequests { get; set; } = [];


    public void SayWhatsOnTheWay(List<ResourceAmount> cargo)
    {
        //This should just let storage increase _incoming
        foreach (var item in cargo)
        {
            _storage.MarkIncoming(item.Resource, item.Amount);
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

public class ResourceRequest(Resource resource, int amount)
{
    public Resource Resource { get; } = resource;
    public int Amount { get; } = amount;
}

public class ResourceStorage
{
    private readonly Dictionary<Resource, int> _resources = new();
    private readonly Dictionary<Resource, int> _incoming = new();

    public Dictionary<Resource, int> GetInventory() => _resources; //ToDo: This should be a copy instead of the real thing

    public Dictionary<Resource, int> GetAll() => new(_resources);

    public void Add(Resource type, int amount)
    {
        _resources.TryAdd(type, 0);
        _resources[type] += amount;

        if (!_incoming.TryGetValue(type, out var incomingAmt)) { return; }

        var deduction = Math.Min(amount, incomingAmt);
        _incoming[type] -= deduction;
        if (_incoming[type] <= 0) { _incoming.Remove(type); }
    }

    public bool Consume(Resource type, int amount)
    {
        if (!_resources.TryGetValue(type, out var current) || current < amount) { return false; }

        _resources[type] -= amount;
        return true;
    }

    public void MarkIncoming(Resource type, int amount)
    {
        _incoming.TryAdd(type, 0);
        _incoming[type] += amount;
    }

    public int GetTotalIncludingIncoming(Resource type) => GetAmount(type) + GetIncomingAmount(type);

    public int GetAmount(Resource type) => _resources.GetValueOrDefault(type, 0);

    public int GetIncomingAmount(Resource type) => _incoming.GetValueOrDefault(type, 0);
}