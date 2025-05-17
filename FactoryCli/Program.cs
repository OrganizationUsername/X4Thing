namespace FactoryCli;

internal class Program
{
    static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}

public interface IUpdatable
{
    void Tick(int currentTick);
}

public enum ResourceType { Ore = 0, EnergyCell = 1, MetalBar = 2, Wheat = 3, Flour = 4, Bread = 5, Plastic = 6, ComputerPart = 7, }

public static class RecipeDatabase
{
    private static readonly Dictionary<ResourceType, Recipe> Recipes = new();

    static RecipeDatabase()
    {
        Recipes[ResourceType.MetalBar] = new Recipe(
            output: ResourceType.MetalBar,
            outputAmount: 1,
            inputs: new Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, 2 },
                { ResourceType.EnergyCell, 1 },
            },
            duration: 10,
            requiredWorkshops: 1
        );

        Recipes[ResourceType.Bread] = new Recipe(
            output: ResourceType.Bread,
            outputAmount: 1,
            inputs: new Dictionary<ResourceType, int>
            {
                { ResourceType.Wheat, 2 },
                { ResourceType.Flour, 1 },
            },
            duration: 8,
            requiredWorkshops: 1
        );
        Recipes[ResourceType.ComputerPart] = new Recipe(
            output: ResourceType.ComputerPart,
            outputAmount: 1,
            inputs: new Dictionary<ResourceType, int>
            {
                { ResourceType.MetalBar, 2 },
                { ResourceType.Plastic, 1 },
            },
            duration: 10,
            requiredWorkshops: 1
        );
    }

    public static Recipe? GetRecipe(ResourceType outputType)
    {
        Recipes.TryGetValue(outputType, out var recipe);
        return recipe;
    }
}

public class Recipe(ResourceType output, int outputAmount, Dictionary<ResourceType, int> inputs, int duration, int requiredWorkshops)
{
    public ResourceType Output { get; } = output;
    public int OutputAmount { get; } = outputAmount;
    public Dictionary<ResourceType, int> Inputs { get; } = inputs;
    public int Duration { get; } = duration;
    public int RequiredWorkshops { get; } = requiredWorkshops;
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

public class ProductionFacility : IUpdatable
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<ResourceType, Recipe> _recipes;
    private readonly Dictionary<ResourceType, int> _workshops;
    private readonly Dictionary<ResourceType, List<ProductionJob>> _activeJobs;

    public List<string> DebugLog { get; } = [];

    public ProductionFacility(ResourceStorage storage, Dictionary<ResourceType, int> recipeWorkshopAssignments)
    {
        _storage = storage;
        _recipes = new();
        _workshops = new();
        _activeJobs = new();

        foreach (var (product, count) in recipeWorkshopAssignments)
        {
            var recipe = RecipeDatabase.GetRecipe(product) ?? throw new Exception($"Recipe not found for {product}");
            _recipes[product] = recipe;
            _workshops[product] = count;
            _activeJobs[product] = [];
        }
    }

    public void AddWorkshops(ResourceType product, int count)
    {
        if (!_recipes.ContainsKey(product))
        {
            var recipe = RecipeDatabase.GetRecipe(product)
                ?? throw new Exception($"Cannot add workshops for {product}: recipe not found.");

            _recipes[product] = recipe;
            _activeJobs[product] = [];
        }

        if (_workshops.ContainsKey(product))
            _workshops[product] += count;
        else
            _workshops[product] = count;

        DebugLog.Add($"  Added {count} workshop(s) for {product}");
    }

    public void Tick(int currentTick)
    {
        DebugLog.Add($"[Tick {currentTick}]");

        foreach (var (product, recipe) in _recipes)
        {
            var jobs = _activeJobs[product];

            // Step 1: Progress jobs
            for (var i = jobs.Count - 1; i >= 0; i--)
            {
                var job = jobs[i];
                job.Elapsed++;

                DebugLog.Add($"  Job for {product} ticked to {job.Elapsed}/{recipe.Duration}");

                if (job.Elapsed >= recipe.Duration)
                {
                    _storage.Add(recipe.Output, recipe.OutputAmount);
                    jobs.RemoveAt(i);
                    DebugLog.Add($"  Completed job for {product}, output added to storage");
                }
            }

            // Step 2: Start new jobs
            var availableWorkshops = _workshops[product] - jobs.Count;
            for (var i = 0; i < availableWorkshops; i++)
            {
                if (CanConsumeInputs(recipe.Inputs))
                {
                    ConsumeInputs(recipe.Inputs);
                    jobs.Add(new ProductionJob());
                    DebugLog.Add($"  Started job for {product} (duration: {recipe.Duration})");
                }
                else
                {
                    DebugLog.Add($"  Not enough inputs to start job for {product}");
                    break;
                }
            }
        }
    }

    private bool CanConsumeInputs(Dictionary<ResourceType, int> inputs)
    {
        foreach (var kvp in inputs)
        {
            if (_storage.GetAmount(kvp.Key) < kvp.Value) return false;
        }
        return true;
    }

    private void ConsumeInputs(Dictionary<ResourceType, int> inputs)
    {
        foreach (var kvp in inputs)
        {
            _storage.Consume(kvp.Key, kvp.Value);
        }
    }

    private class ProductionJob
    {
        public int Elapsed;
    }
}



public class Ticker
{
    private readonly List<IUpdatable> _tickables = [];
    public int CurrentTick { get; private set; }

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