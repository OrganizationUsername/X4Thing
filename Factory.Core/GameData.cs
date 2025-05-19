using System.Numerics;

namespace Factory.Core;

public class GameData(Dictionary<string, Resource> resources, Dictionary<string, Recipe> recipes)
{
    public Dictionary<string, Resource> Resources { get; } = resources;
    public Dictionary<string, Recipe> Recipes { get; } = recipes;
    public List<ProductionFacility> Facilities { get; } = [];
    public List<Transporter> Transporters { get; } = [];
    public List<Fighter> Fighters { get; } = [];

    public static GameData GetDefault()
    {
        var resources = CreateInitialResourcesDictionary();
        return new GameData(resources, CreateRecipesDictionary(resources));
    }

    public List<ILogLine> GetAllLogsSinceTickNumber(int tick)
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
        AssignFightersToTargets(currentTick);
    }

    private void AssignFightersToTargets(int currentTick)
    {
        //ToDo: This whole thing is only useful if there is a transporter that's available on the attacker's side who can pick up the broken wares.
        foreach (var fighter in Fighters.Where(f => f.Target == null))
        {
            var bestTarget = Transporters
                .Where(t => t.TotalHull > 0)
                .Where(t => t.PlayerId != fighter.PlayerId)
                .OrderByDescending(Fighter.GetTransportValue)
                .FirstOrDefault(t => Fighter.GetTransportValue(t) >= fighter.MinimumValue);
            if (bestTarget != null) { fighter.SetTarget(bestTarget, currentTick); }
        }
    }

    public void AssignTransportersToBestTrades(int currentTick)
    {
        foreach (var transporter in Transporters.Where(t => !t.HasActiveTask() && t.TotalHull > 0))
        {
            var trade = FindBestTrade();
            if (trade is null) continue;

            var resourceVolume = trade.Resource.Volume;
            var maxAmount = (int)(transporter.MaxVolume / resourceVolume);
            if (maxAmount <= 0) continue;

            var toSend = Math.Min(trade.Amount, maxAmount);
            transporter.AssignTask(trade.From, trade.To, [new ResourceAmount(trade.Resource, toSend),], currentTick);
        }
    }

    public TradeMission? FindBestTrade() =>
        GetPullRequests()
            .SelectMany(pull =>
                GetPushOffers()
                    .Where(push => push.resource == pull.resource)
                    .Where(push => push.facility != pull.facility)
                    .Where(push => push.playerId == pull.playerId)
            //ToDo: Later this can be a bit more advanced where we get the sum of pull and see if a push can do it all
                    .Select(push => new TradeMission(push.facility, pull.facility, pull.resource, Math.Min(push.amount, pull.amount))))
            .OrderByDescending(x => x.Value)
            //ToDo: Why aren't we returning all of the results and letting them pick?
            .FirstOrDefault();

    private static Dictionary<string, Recipe> CreateRecipesDictionary(Dictionary<string, Resource> resources)
    {
        var ore = resources["ore"];
        var energyCell = resources["energy_cell"];
        var metalBar = resources["metal_bar"];
        var wheat = resources["wheat"];
        var flour = resources["flour"];
        var bread = resources["bread"];
        var plastic = resources["plastic"];
        var computerPart = resources["computer_part"];
        var siliconWafer = resources["silicon_wafer"];
        var aiModule = resources["ai_module"];
        var sand = resources["sand"];


        var recipes = new Dictionary<string, Recipe>
        {
            { "recipe_ai_module", new Recipe { Id = "recipe_ai_module", Output = aiModule, OutputAmount = 1, Duration = 12, Inputs = new Dictionary<Resource, int> { { computerPart, 1 }, { siliconWafer, 2 }, }, Benefit = 22f, } },
            { "recipe_silicon_wafer", new Recipe { Id = "recipe_silicon_wafer", Output = siliconWafer, OutputAmount = 1, Duration = 6, Inputs = new Dictionary<Resource, int> { { sand, 3 }, { energyCell, 1 }, }, Benefit = 2f, } },
            { "recipe_metal_bar", new Recipe { Id = "recipe_metal_bar", Output = metalBar, OutputAmount = 1, Duration = 10, Inputs = new Dictionary<Resource, int> { { ore, 2 }, { energyCell, 1 }, }, Benefit = 2f, } },
            { "recipe_bread", new Recipe { Id = "recipe_bread", Output = bread, OutputAmount = 1, Duration = 8, Inputs = new Dictionary<Resource, int> { { wheat, 2 }, { flour, 1 }, }, Benefit = 1.5f, } },
            { "recipe_computer_part", new Recipe { Id = "recipe_computer_part", Output = computerPart, OutputAmount = 1, Duration = 10, Inputs = new Dictionary<Resource, int> { { metalBar, 2 }, { plastic, 1 }, }, Benefit = 8f, }
            },
        };
        return recipes;
    }

    private static Dictionary<string, Resource> CreateInitialResourcesDictionary() => new()
        {
            { "ore", new Resource { Id = "ore", DisplayName = "Ore", BaseValue = 1, Volume = 3.0f, } },
            { "energy_cell", new Resource { Id = "energy_cell", DisplayName = "Energy Cell", BaseValue = 2, Volume = 0.4f, } },
            { "metal_bar", new Resource { Id = "metal_bar", DisplayName = "Metal Bar", BaseValue = 5, Volume = 1.5f, } },
            { "wheat", new Resource { Id = "wheat", DisplayName = "Wheat", BaseValue = 1, Volume = 2.0f, } },
            { "flour", new Resource { Id = "flour", DisplayName = "Flour", BaseValue = 1.5f, Volume = 1.0f, } },
            { "bread", new Resource { Id = "bread", DisplayName = "Bread", BaseValue = 3, Volume = 1.2f, } },
            { "plastic", new Resource { Id = "plastic", DisplayName = "Plastic", BaseValue = 2, Volume = 0.8f, } },
            { "computer_part", new Resource { Id = "computer_part", DisplayName = "Computer Part", BaseValue = 20, Volume = 0.2f, } },
            { "silicon_wafer", new Resource { Id = "silicon_wafer", DisplayName = "Silicon Wafer", BaseValue = 4, Volume = 0.5f, } },
            { "ai_module", new Resource { Id = "ai_module", DisplayName = "AI Module", BaseValue = 50, Volume = 1.0f, } },
            { "sand", new Resource { Id = "sand", DisplayName = "Sand", BaseValue = 0.5f, Volume = 1.0f, } },
        };

    public IEnumerable<(ProductionFacility facility, Resource resource, int amount, int playerId)> GetPushOffers()
    {
        foreach (var facility in Facilities)
        {
            foreach (var (resource, amount) in facility.GetPushOffers())
            {
                yield return (facility, resource, amount, facility.PlayerId);
            }
        }
    }

    public IEnumerable<(ProductionFacility facility, Resource resource, int amount, int playerId)> GetPullRequests()
    {
        foreach (var facility in Facilities)
        {
            foreach (var (resource, amount) in facility.GetPullRequests())
            {
                yield return (facility, resource, amount, facility.PlayerId);
            }
        }
    }

    public Resource GetResource(string id) => Resources[id];
    public Recipe GetRecipe(string id) => Recipes[id];
}

public class TradeMission(ProductionFacility from, ProductionFacility to, Resource resource, int amount)
{
    public ProductionFacility From { get; } = from;
    public ProductionFacility To { get; } = to;
    public Resource Resource { get; } = resource;
    public int Amount { get; } = amount;
    public float Value { get; } = resource.BaseValue / Vector2.Distance(from.Position, to.Position);
}