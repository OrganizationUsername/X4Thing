using System.Numerics;

namespace Factory.Core;

public class GameData : IUpdatable
{
    public Dictionary<string, Resource> Resources { get; } = new();
    public Dictionary<string, Recipe> Recipes { get; } = new();
    public List<ProductionFacility> Facilities { get; } = [];
    public List<Transporter> Transporters { get; } = [];
    public List<Fighter> Fighters { get; } = [];

    // ReSharper disable once EmptyStatement
    private GameData() {; }

    public GameData(Dictionary<string, Resource> resources, Dictionary<string, Recipe> recipes)
    {
        Resources = resources;
        Recipes = recipes;
    }

    public static GameData GetDefault() //later on, I should have the default stuff in another class and load those in using the public ctor
    {
        var gameData = new GameData();
        gameData.InitializeResources();
        gameData.InitializeRecipes();
        return gameData;
    }

    public List<ILogLine> GetAllLogs(int tick)
    {
        var allLogs = new List<ILogLine>();
        foreach (var facility in Facilities) { allLogs.AddRange(facility.LogLines.Where(l => l.Tick >= tick)); }
        foreach (var transporter in Transporters) { allLogs.AddRange(transporter.LogLines.Where(l => l.Tick >= tick)); }
        return [.. allLogs.OrderBy(l => l.Tick),];
    }

    public List<ILogLine> GetAllLogs()
    {
        var allLogs = new List<ILogLine>();
        foreach (var facility in Facilities) { allLogs.AddRange(facility.LogLines); }
        foreach (var transporter in Transporters) { allLogs.AddRange(transporter.LogLines); }
        foreach (var fighter in Fighters) { allLogs.AddRange(fighter.LogLines); }

        return [.. allLogs.OrderBy(l => l.Tick),];
    }

    public string GetAllLogsFormatted() => string.Join(Environment.NewLine, GetAllLogs().Select(l => l.Format()));

    public void Tick(int currentTick)
    {
        AssignTransportersToBestTrades(currentTick);
        //Should try to assign fighters here
        AssignFightersToTargets(currentTick);
    }

    private void AssignFightersToTargets(int currentTick)
    {
        foreach (var fighter in Fighters.Where(f => f.Target == null))
        {
            var bestTarget = Transporters
                .Where(t => t.TotalHull > 0)
                .Where(t => t.PlayerId != fighter.PlayerId)
                .OrderByDescending(t => fighter.GetTransportValue(t))
                .FirstOrDefault(t => fighter.GetTransportValue(t) >= fighter.MinimumValue);

            if (bestTarget != null)
            {
                fighter.SetTarget(bestTarget, currentTick);
            }
        }
    }

    public void AssignTransportersToBestTrades(int currentTick)
    {
        foreach (var transporter in Transporters.Where(t => !t.HasActiveTask() && t.TotalHull > 0))
        {
            var trade = FindBestTrade();
            if (trade is null) continue;

            var (from, to, resource, amount) = trade.Value;
            var resourceVolume = resource.Volume;
            var maxAmount = (int)(transporter.MaxVolume / resourceVolume);

            if (maxAmount <= 0) continue;

            var toSend = Math.Min(amount, maxAmount);
            transporter.AssignTask(from, to, [new ResourceAmount(resource, toSend)], currentTick);
        }
    }

    public (ProductionFacility from, ProductionFacility to, Resource resource, int amount)? FindBestTrade() //this can take in an int to note PlayerId. Can also take in a ShipId so we know where it is. We can calculate TotalValueAdded/(Distance/Speed)
    {
        var pulls = GetPullRequests().ToList();
        var pushes = GetPushOffers().ToList();

        var best = pulls
            .SelectMany(pull =>
                pushes
                    .Where(push =>
                        push.resource == pull.resource &&
                        push.facility != pull.facility) // ❗ prevent self-delivery
                    .Select(push => new
                    {
                        From = push.facility,
                        To = pull.facility,
                        Resource = pull.resource,
                        Amount = Math.Min(push.amount, pull.amount),
                        Value = pull.resource.BaseValue / Vector2.Distance(push.facility.Position, pull.facility.Position),
                    }))
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();

        return best is null ? null : (best.From, best.To, best.Resource, best.Amount);
    }

    private void InitializeRecipes()
    {
        var ore = GetResource("ore");
        var energyCell = GetResource("energy_cell");
        var metalBar = GetResource("metal_bar");
        var wheat = GetResource("wheat");
        var flour = GetResource("flour");
        var bread = GetResource("bread");
        var plastic = GetResource("plastic");
        var computerPart = GetResource("computer_part");
        var siliconWafer = GetResource("silicon_wafer");
        var aiModule = GetResource("ai_module");
        var sand = GetResource("sand");

        /*
           Station	    Produces        Needs
           A	        metal_bar	    ore, energy_cell
           B	        computer_part	metal_bar, plastic
           C	        silicon_wafer	sand, energy_cell
           D	        ai_module	    computer_part, silicon_wafer
         */

        AddRecipe(new Recipe
        {
            Id = "recipe_ai_module",
            Output = aiModule,
            OutputAmount = 1,
            Duration = 12,
            Inputs = new Dictionary<Resource, int> { { computerPart, 1 }, { siliconWafer, 2 }, },
            Benefit = 22f, // e.g., $28 input → $50 value
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_silicon_wafer",
            Output = siliconWafer,
            OutputAmount = 1,
            Duration = 6,
            Inputs = new Dictionary<Resource, int> { { sand, 3 }, { energyCell, 1 }, },
            Benefit = 2f, // Example: $1.5 inputs → $4 output
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_metal_bar",
            Output = metalBar,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<Resource, int> { { ore, 2 }, { energyCell, 1 }, },
            Benefit = 2f, // From 2×$1 (ore) + $2 (cell) → $5 bar = $1 net + strategic value
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_bread",
            Output = bread,
            OutputAmount = 1,
            Duration = 8,
            Inputs = new Dictionary<Resource, int> { { wheat, 2 }, { flour, 1 }, },
            Benefit = 1.5f, // $3.5 → $5 (or just gameplay-tuned)
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_computer_part",
            Output = computerPart,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<Resource, int> { { metalBar, 2 }, { plastic, 1 }, },
            Benefit = 8f, // $12 → $20 = +$8
        });
    }

    private void InitializeResources()
    {
        AddResource(new Resource { Id = "ore", DisplayName = "Ore", BaseValue = 1, Volume = 3.0f, });
        AddResource(new Resource { Id = "energy_cell", DisplayName = "Energy Cell", BaseValue = 2, Volume = 0.4f, });
        AddResource(new Resource { Id = "metal_bar", DisplayName = "Metal Bar", BaseValue = 5, Volume = 1.5f, });
        AddResource(new Resource { Id = "wheat", DisplayName = "Wheat", BaseValue = 1, Volume = 2.0f, });
        AddResource(new Resource { Id = "flour", DisplayName = "Flour", BaseValue = 1.5f, Volume = 1.0f, });
        AddResource(new Resource { Id = "bread", DisplayName = "Bread", BaseValue = 3, Volume = 1.2f, });
        AddResource(new Resource { Id = "plastic", DisplayName = "Plastic", BaseValue = 2, Volume = 0.8f, });
        AddResource(new Resource { Id = "computer_part", DisplayName = "Computer Part", BaseValue = 20, Volume = 0.2f, });
        AddResource(new Resource { Id = "silicon_wafer", DisplayName = "Silicon Wafer", BaseValue = 4, Volume = 0.5f, });
        AddResource(new Resource { Id = "ai_module", DisplayName = "AI Module", BaseValue = 50, Volume = 1.0f, });
        AddResource(new Resource { Id = "sand", DisplayName = "Sand", BaseValue = 0.5f, Volume = 1.0f, });
    }

    public IEnumerable<(ProductionFacility facility, Resource resource, int amount)> GetPushOffers()
    {
        foreach (var facility in Facilities)
        {
            foreach (var offer in facility.GetPushOffers())
            {
                yield return (facility, offer.resource, offer.amount);
            }
        }
    }

    public IEnumerable<(ProductionFacility facility, Resource resource, int amount)> GetPullRequests()
    {
        foreach (var facility in Facilities)
        {
            foreach (var request in facility.GetPullRequests())
            {
                yield return (facility, request.resource, request.amount);
            }
        }
    }

    public void AddResource(Resource res) => Resources[res.Id] = res;
    public void AddRecipe(Recipe rec) => Recipes[rec.Id] = rec;

    public Resource GetResource(string id) => Resources[id];
    public Recipe GetRecipe(string id) => Recipes[id];
}