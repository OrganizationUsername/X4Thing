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

public class ResourceDef
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public float BaseValue { get; init; } = 1.0f;

    public override string ToString() => DisplayName;
}
public class RecipeDef
{
    public string Id { get; init; } = "";
    public ResourceDef Output { get; init; } = null!;
    public int OutputAmount { get; init; }
    public Dictionary<ResourceDef, int> Inputs { get; init; } = new();
    public int Duration { get; init; }
}


public class GameData
{
    public Dictionary<string, ResourceDef> Resources { get; } = new();
    public Dictionary<string, RecipeDef> Recipes { get; } = new();

    public GameData()
    {
        // Define resources
        var ore = new ResourceDef { Id = "ore", DisplayName = "Ore", BaseValue = 1 };
        var energyCell = new ResourceDef { Id = "energy_cell", DisplayName = "Energy Cell", BaseValue = 2 };
        var metalBar = new ResourceDef { Id = "metal_bar", DisplayName = "Metal Bar", BaseValue = 5 };
        var wheat = new ResourceDef { Id = "wheat", DisplayName = "Wheat", BaseValue = 1 };
        var flour = new ResourceDef { Id = "flour", DisplayName = "Flour", BaseValue = 1.5f };
        var bread = new ResourceDef { Id = "bread", DisplayName = "Bread", BaseValue = 3 };
        var plastic = new ResourceDef { Id = "plastic", DisplayName = "Plastic", BaseValue = 2 };
        var computerPart = new ResourceDef { Id = "computer_part", DisplayName = "Computer Part", BaseValue = 10 };

        // Register resources
        foreach (var r in new[] { ore, energyCell, metalBar, wheat, flour, bread, plastic, computerPart })
        {
            AddResource(r);
        }

        // Define recipes
        AddRecipe(new RecipeDef
        {
            Id = "recipe_metal_bar",
            Output = metalBar,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<ResourceDef, int>
            {
                { ore, 2 },
                { energyCell, 1 }
            }
        });

        AddRecipe(new RecipeDef
        {
            Id = "recipe_bread",
            Output = bread,
            OutputAmount = 1,
            Duration = 8,
            Inputs = new Dictionary<ResourceDef, int>
            {
                { wheat, 2 },
                { flour, 1 }
            }
        });

        AddRecipe(new RecipeDef
        {
            Id = "recipe_computer_part",
            Output = computerPart,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<ResourceDef, int>
            {
                { metalBar, 2 },
                { plastic, 1 }
            }
        });
    }

    public void AddResource(ResourceDef res) => Resources[res.Id] = res;
    public void AddRecipe(RecipeDef rec) => Recipes[rec.Id] = rec;

    public ResourceDef GetResource(string id) => Resources[id];
    public RecipeDef GetRecipe(string id) => Recipes[id];
}


public class ResourceStorage
{
    private readonly Dictionary<ResourceDef, int> _resources = new();

    public void Add(ResourceDef type, int amount)
    {
        if (!_resources.ContainsKey(type)) _resources[type] = 0;
        _resources[type] += amount;
    }

    public bool Consume(ResourceDef type, int amount)
    {
        if (!_resources.TryGetValue(type, out var current) || current < amount)
            return false;

        _resources[type] -= amount;
        return true;
    }

    public int GetAmount(ResourceDef type) => _resources.GetValueOrDefault(type, 0);
}


public class ProductionFacility : IUpdatable
{
    private readonly ResourceStorage _storage;
    private readonly Dictionary<RecipeDef, int> _workshops;
    private readonly Dictionary<RecipeDef, List<ProductionJob>> _activeJobs;
    public List<string> DebugLog { get; } = [];

    public Vector2 Position { get; set; } = new(0, 0);

    public ProductionFacility(ResourceStorage storage, Dictionary<RecipeDef, int> recipeWorkshopAssignments)
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


    [UsedImplicitly] public string GetDebugLog() => string.Join(Environment.NewLine, DebugLog);

    public void AddWorkshops(RecipeDef recipe, int count)
    {
        if (!_workshops.TryAdd(recipe, count))
            _workshops[recipe] += count;

        if (!_activeJobs.ContainsKey(recipe))
            _activeJobs[recipe] = [];

        DebugLog.Add($"  Added {count} workshop(s) for {recipe.Output.Id}");
    }


    public void Tick(int currentTick)
    {
        var tempLog = new List<string>();

        foreach (var (recipe, jobs) in _activeJobs)
        {
            var output = recipe.Output;

            // Step 1: Progress jobs
            for (int i = jobs.Count - 1; i >= 0; i--)
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
            for (int i = 0; i < availableWorkshops; i++)
            {
                if (CanConsumeInputs(recipe.Inputs))
                {
                    ConsumeInputs(recipe.Inputs);
                    jobs.Add(new ProductionJob());
                    tempLog.Add($"  Started job for {output.Id} (duration: {recipe.Duration})");
                }
                else
                {
                    break;
                }
            }
        }

        if (tempLog.Count > 0)
        {
            DebugLog.Add($"[Tick {currentTick}]");
            DebugLog.AddRange(tempLog);
        }
    }
    private bool CanConsumeInputs(Dictionary<ResourceDef, int> inputs)
    {
        foreach (var (resource, amount) in inputs)
        {
            if (_storage.GetAmount(resource) < amount)
                return false;
        }
        return true;
    }

    private void ConsumeInputs(Dictionary<ResourceDef, int> inputs)
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

    public int? GetTicksUntilNextEvent()
    {
        int? soonestCompletion = null;

        foreach (var (recipe, jobs) in _activeJobs)
        {
            int availableWorkshops = _workshops[recipe] - jobs.Count;

            // Completion
            foreach (var job in jobs)
            {
                int ticksLeft = recipe.Duration - job.Elapsed;
                if (soonestCompletion == null || ticksLeft < soonestCompletion)
                    soonestCompletion = ticksLeft;
            }

            // Can start?
            if (availableWorkshops > 0 && CanConsumeInputs(recipe.Inputs))
                return 0;
        }

        return soonestCompletion;
    }


}