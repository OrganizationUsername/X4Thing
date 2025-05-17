using System.Numerics;

namespace FactoryCli;

public class GameData
{
    public Dictionary<string, Resource> Resources { get; } = new();
    public Dictionary<string, Recipe> Recipes { get; } = new();
    public List<ProductionFacility> Facilities { get; } = [];
    public List<Transporter> Transporters { get; } = [];

    private GameData() { }

    //public ctor with parameters for resources/recipes
    public GameData(Dictionary<string, Resource> resources, Dictionary<string, Recipe> recipes)
    {
        Resources = resources;
        Recipes = recipes;
    }

    public static GameData GetDefault()
    {
        var gameData = new GameData();
        gameData.InitializeResources();
        gameData.InitializeRecipes();
        return gameData;
    }

    private void InitializeResources()
    {
        AddResource(new Resource { Id = "ore", DisplayName = "Ore", BaseValue = 1, });
        AddResource(new Resource { Id = "energy_cell", DisplayName = "Energy Cell", BaseValue = 2, });
        AddResource(new Resource { Id = "metal_bar", DisplayName = "Metal Bar", BaseValue = 5, });
        AddResource(new Resource { Id = "wheat", DisplayName = "Wheat", BaseValue = 1, });
        AddResource(new Resource { Id = "flour", DisplayName = "Flour", BaseValue = 1.5f, });
        AddResource(new Resource { Id = "bread", DisplayName = "Bread", BaseValue = 3, });
        AddResource(new Resource { Id = "plastic", DisplayName = "Plastic", BaseValue = 2, });
        AddResource(new Resource { Id = "computer_part", DisplayName = "Computer Part", BaseValue = 20, });
    }

    public (ProductionFacility from, ProductionFacility to, Resource resource, int amount)? FindBestTrade() //this can take in an int to note PlayerId. Can also take in a ShipId so we know where it is. We can calculate TotalValueAdded/(Distance/Speed)
    {
        var pulls = GetPullRequests().ToList();
        var pushes = GetPushOffers().ToList();

        var best = pulls
            .SelectMany(pull =>
                pushes
                    .Where(push => push.resource == pull.resource)
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

        AddRecipe(new Recipe
        {
            Id = "recipe_metal_bar",
            Output = metalBar,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<Resource, int>
            {
                { ore, 2 },
                { energyCell, 1 },
            },
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_bread",
            Output = bread,
            OutputAmount = 1,
            Duration = 8,
            Inputs = new Dictionary<Resource, int>
            {
                { wheat, 2 },
                { flour, 1 },
            },
        });

        AddRecipe(new Recipe
        {
            Id = "recipe_computer_part",
            Output = computerPart,
            OutputAmount = 1,
            Duration = 10,
            Inputs = new Dictionary<Resource, int>
            {
                { metalBar, 2 },
                { plastic, 1 },
            },
        });
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