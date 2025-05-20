namespace Factory.Core;


public class HighestBenefitProductionStrategy : IProductionStrategy
{
    public Recipe? SelectRecipe(ResourceStorage storage, List<Recipe> availableRecipes)
    {
        return availableRecipes
            .Where(r => r.Inputs.All(i => storage.GetAmount(i.Key) >= i.Value))
            .OrderByDescending(r =>
            {
                var inputCost = r.Inputs.Sum(i => i.Key.BaseValue * i.Value);
                var outputValue = r.Output.BaseValue * r.OutputAmount;
                var netValue = outputValue - inputCost;
                var valuePerTick = netValue / r.Duration;
                return valuePerTick;
            })
            .FirstOrDefault();
    }
}

public class DesperateProductionStrategy : IProductionStrategy
{
    //maybe this could also take in the resource that we're desperate for. Then we can just use that to pick the recipe.
    public Recipe? SelectRecipe(ResourceStorage storage, List<Recipe> availableRecipes)
    {
        return availableRecipes
            .Where(r => r.Inputs.All(i => storage.GetAmount(i.Key) >= i.Value))
            .OrderBy(r =>
            {
                var outputValue = r.Output.BaseValue * r.OutputAmount;
                var desperationScore = r.Duration / outputValue;  // Lower = better
                return desperationScore;
            })
            .FirstOrDefault();
    }
}

public interface IProductionStrategy
{
    Recipe? SelectRecipe(ResourceStorage storage, List<Recipe> availableRecipes);
}

public class ProductionFacility : Entity, IUpdatable
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<Recipe, int> _workshops;
    private readonly Dictionary<Recipe, List<ProductionJob>> _activeJobs;

    public readonly List<WorkshopInstance> WorkshopInstances = [];
    private bool UseFlexibleWorkshops => _workshops.Count == 0;


    public ProductionFacility()
    {
        _storage = new ResourceStorage();
        _workshops = new Dictionary<Recipe, int>();
        _activeJobs = [];
    }

    public ProductionFacility(ResourceStorage storage, Dictionary<Recipe, int> recipeWorkshopAssignments)
    {
        _storage = storage;
        _workshops = [];
        _activeJobs = [];

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
    public List<ResourceRequest> LastRequests { get; set; } = [];

    public bool TryExport(Resource res, int amountToTake, int tick, Transporter receiver)
    {
        var success = _storage.Consume(res, amountToTake);
        if (success) { LogLines.Add(new TransportSentLog(tick, res.Id, amountToTake, Position, receiver)); }
        else { LogLines.Add(new TransportFailedLog(tick, this, res.Id, amountToTake)); }
        return success;
    }

    public void ReceiveImport(Resource res, int amountToTransfer, int tick, Transporter transporter)
    {
        LogLines.Add(new TransportReceivedLog(tick, res.Id, amountToTransfer, Position, transporter));
        _storage.Add(res, amountToTransfer);
    }

    public void AddWorkshops(Recipe recipe, int count)
    {
        if (!_workshops.TryAdd(recipe, count)) { _workshops[recipe] += count; }
        if (!_activeJobs.ContainsKey(recipe)) { _activeJobs[recipe] = []; }
        LogLines.Add(new WorkshopAddedLog(0, this, recipe.Output.Id, count));
    }

    public void AddProductionModule(ProductionModule productionModule)
    {
        WorkshopInstances.Add(new WorkshopInstance { Module = productionModule });
        //LogLines.Add(new FlexibleWorkshopAddedLog(0, this, productionModule.Recipes.Select(r => r.Output.Id).ToList()));
    }

    public void Tick(int currentTick)
    {
        if (UseFlexibleWorkshops) { TickFlexible(currentTick); }
        else { TickLegacy(currentTick); }
    }

    private void TickFlexible(int currentTick)
    {
        foreach (var instance in WorkshopInstances)
        {
            var job = instance.ActiveJob;

            if (job != null)
            {
                job.Elapsed++;
                if (job.Elapsed >= job.Recipe.Duration)
                {
                    _storage.Add(job.Recipe.Output, job.Recipe.OutputAmount);
                    LogLines.Add(new ProductionCompletedLog(currentTick, this, job.Recipe.Output.Id, job.Recipe.OutputAmount));
                    instance.ActiveJob = null;
                }
            }

            if (instance.ActiveJob == null)
            {
                var chosen = instance.Module.Strategy.SelectRecipe(_storage, instance.Module.Recipes);
                if (chosen != null && CanConsumeInputs(chosen.Inputs))
                {
                    ConsumeInputs(chosen.Inputs);
                    instance.ActiveJob = new ProductionJob { Recipe = chosen, };
                    LogLines.Add(new ProductionStartedLog(currentTick, this, chosen.Output.Id, chosen.Duration, Position, chosen));
                    instance.TimeSinceLastStarted = 0;
                }
            }

            if (instance.ActiveJob is null) { instance.TimeSinceLastStarted++; }
        }
    }

    public void TickLegacy(int currentTick)
    {
        foreach (var (recipe, jobs) in _activeJobs)
        {
            var output = recipe.Output;
            for (var i = jobs.Count - 1; i >= 0; i--) // Step 1: Progress jobs
            {
                var job = jobs[i];
                job.Elapsed++;

                if (job.Elapsed < recipe.Duration) { continue; }

                _storage.Add(output, recipe.OutputAmount);
                jobs.RemoveAt(i);
                LogLines.Add(new ProductionCompletedLog(currentTick, this, output.Id, recipe.OutputAmount));
            }

            var availableWorkshops = _workshops[recipe] - jobs.Count; // Step 2: Start new jobs
            for (var i = 0; i < availableWorkshops; i++)
            {
                if (CanConsumeInputs(recipe.Inputs))
                {
                    ConsumeInputs(recipe.Inputs);
                    jobs.Add(new ProductionJob());
                    LogLines.Add(new ProductionStartedLog(currentTick, this, output.Id, recipe.Duration, Position, recipe));
                }
                else { break; }
            }
        }
    }

    private bool CanConsumeInputs(Dictionary<Resource, int> inputs)
    {
        foreach (var (resource, amount) in inputs) { if (_storage.GetAmount(resource) < amount) { return false; } }
        return true;
    }

    private void ConsumeInputs(Dictionary<Resource, int> inputs) { foreach (var (resource, amount) in inputs) { _storage.Consume(resource, amount); } }

    public IEnumerable<(Resource resource, int amount)> GetPushOffers()
    {
        var inputResources = _workshops.Keys.SelectMany(recipe => recipe.Inputs.Keys).ToHashSet();
        foreach (var (res, amount) in _storage.GetAll()) // Only offer to push resources not used as inputs, for now. Later I'll have another strategy that allows me to figure out what to give up.
        {
            if (!inputResources.Contains(res) && amount > 0) { yield return (res, amount); }
        }
    }

    public IEnumerable<(Resource resource, int amount)> GetPullRequests()
    {
        var result = PullRequestStrategy.GetRequests(this).ToList();
        LastRequests = [.. result.Select(r => new ResourceRequest(r.resource, r.amount)),];
        return result;
    }

    public void SayWhatsOnTheWay(List<ResourceAmount> cargo)
    {
        foreach (var item in cargo) { _storage.MarkIncoming(item.Resource, item.Amount); } //This should just let storage increase _incoming
    }

    public int? GetTicksUntilNextEvent()
    {
        int? soonestCompletion = null;
        foreach (var (recipe, jobs) in _activeJobs)
        {
            var availableWorkshops = _workshops[recipe] - jobs.Count;
            foreach (var job in jobs)
            {
                if (soonestCompletion == null || recipe.Duration - job.Elapsed < soonestCompletion) { soonestCompletion = recipe.Duration - job.Elapsed; }
            }
            if (availableWorkshops > 0 && CanConsumeInputs(recipe.Inputs)) { return 0; }
        }
        return soonestCompletion;
    }
}

public class WorkshopInstance
{
    public required ProductionModule Module { get; set; }
    public ProductionJob? ActiveJob { get; set; }
    public int TimeSinceLastStarted { get; set; }
}

public class ResourceRequest(Resource resource, int amount)
{
    public Resource Resource { get; } = resource;
    public int Amount { get; } = amount;
}

public class ResourceStorage
{
    private readonly Dictionary<Resource, int> _resources = [];
    private readonly Dictionary<Resource, int> _incoming = [];
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

    public void MarkIncoming(Resource type, int amount) { _incoming.TryAdd(type, 0); _incoming[type] += amount; }
    public int GetTotalIncludingIncoming(Resource type) => GetAmount(type) + GetIncomingAmount(type);
    public int GetAmount(Resource type) => _resources.GetValueOrDefault(type, 0);
    public int GetIncomingAmount(Resource type) => _incoming.GetValueOrDefault(type, 0);

    public static ResourceStorage GetClone(ResourceStorage original)
    {
        var clone = new ResourceStorage();
        foreach (var (res, amount) in original._resources) { clone.Add(res, amount); }
        foreach (var (res, amount) in original._incoming) { clone.MarkIncoming(res, amount); }
        return clone;
    }

}

public class ProductionJob
{
    public Recipe Recipe { get; set; } = null!;
    public int Elapsed;
}
