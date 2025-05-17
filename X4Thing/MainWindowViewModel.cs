using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace X4Thing;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "X4Thing";
    public long Money { get; set; } = 100;

    public ObservableCollection<ShipViewModel> Ships { get; set; } = [];
    [ObservableProperty] private ShipViewModel? _selectedShip;
    public ObservableCollection<Station> Stations { get; set; } = [];

    public MainWindowViewModel()
    {
        //My proof of concept will be moving the ship to the station, depositing inventory, and creating a weapon component.
        //The starting ship will hold some things that the weapon-building station won't accept.
        //Then we use the weapon component to finish building a mining ship.
        //Then the user sends the mining ship to mine resources to finish building a trade ship.
        //Then the user sends the trade ship to trade resources to build a combat ship.
        //Then the user sends the combat ship to destroy an enemy ship.

        AddShips();
        AddStations();
    }

    private void AddStations()
    {
        Stations.Add(new Station
        {
            Name = "S1",
            StorageCapacity = 100,
            Location = new Vector3(5, 5, 5),
            Inventory =
            [
                new(){Ware = Ware.Food, Quantity = 10, },
                new(){Ware = Ware.Energy, Quantity = 05, },
            ],
            ProductionModules =
            [
                new()
                {
                    Name = "P1",
                    Input =[new(){Ware = Ware.Steel, Quantity = 1, }, new(){Ware = Ware.Energy, Quantity = 1, },],
                    Output =[new(){Ware = Ware.WeaponComponents, Quantity = 1, },],
                    TimeToBuild = 10,
                },
            ],
        });
    }
    private void AddShips()
    {
        var ship = new Ship
        {
            Name = "S1",
            StorageCapacity = 100,
            Location = new Vector3(00, 00, 00),
            Inventory =
            [
                new(){Ware = Ware.Food, Quantity = 10, },
                new(){Ware = Ware.Energy, Quantity = 05, },
            ],
            Weapons =
            [new()
                {
                    Name = "W1", Damage = 10, Range = 10, FireRate = 1,
                    Damages = [new DamageQuantity() { DamageType = DamageType.Kinetic, Quantity = 10, },],
                },
            ],
        };
        //instead of this type of flow, I should have a ShipManager class that handles all 
        Ships.Add(new() { Ship = ship, });

        var ship2 = new Ship
        {
            Name = "XX",
            StorageCapacity = 100,
            Location = new Vector3(50, 50, 50),
            Inventory =
            [
                new(){Ware = Ware.Steel, Quantity = 10, },
                new(){Ware = Ware.HullParts, Quantity = 05, },
            ],
            Weapons =
            [new()
                {
                    Name = "VVV", Damage = 10, Range = 10, FireRate = 1,
                    Damages = [new DamageQuantity() { DamageType = DamageType.Kinetic, Quantity = 10, },],
                },
            ],
        };
        //instead of this type of flow, I should have a ShipManager class that handles all 
        Ships.Add(new() { Ship = ship2, });
    }

    [RelayCommand]
    public void DoubleClickDataGridRow()
    {
        if (SelectedShip == null) { return; }
        SelectedShip.ShowInventory = !SelectedShip.ShowInventory;
    }

    public void MoveShipToStation(Ship ship, Station station)
    {
        var distance = Vector3.Distance(ship.Location, station.Location);
        if (distance > 0)
        {
            ship.Location = station.Location; // Move ship to station
            station.LandedShips.Add(ship); // Add ship to station's landed ships
        }
    }

    public void DepositInventory(Ship ship, Station station)
    {
        foreach (var item in ship.Inventory.ToList())
        {
            station.Inventory.Add(item); // Add item to station inventory
            ship.Inventory.Remove(item); // Remove item from ship inventory
        }
    }

    public void AttemptComponentProduction(Station station)
    {
        //This method should check what recipes are available, and if the station has the required resources, start building the first one.
        //If we start, we should remove the resources from the station's inventory and add a build progress to the station.
    }

    public void TickProduction(Station station)
    {
        //This method should decrement the time remaining on all build progresses, and if any reach 0, add the output to the station's inventory.
    }

    public Ship BuildShip(string name, long storageCapacity, Vector3 location)
    {
        var newShip = new Ship
        {
            Name = name,
            StorageCapacity = storageCapacity,
            Location = location,
            Inventory = new List<WareQuantity>(),
            Weapons = new List<Weapon>(),
        };
        Ships.Add(new() { Ship = newShip, });
        return newShip;
    }

    public void SendShipToMineResources(Ship ship, Ware ware, long quantity)
    {
        ship.Inventory.Add(new WareQuantity { Ware = ware, Quantity = quantity, });
    }

    public void SendShipToTradeResources(Ship ship, Ware wareToTrade, long quantity, Ware wareToReceive)
    {
        ship.Inventory.Remove(ship.Inventory.First(wq => wq.Ware == wareToTrade && wq.Quantity >= quantity));
        ship.Inventory.Add(new WareQuantity { Ware = wareToReceive, Quantity = quantity, }); // Simplified trade logic
    }

    public void SendShipToDestroyEnemyShip(Ship yourShip, Ship enemyShip)
    {
        // Simulate combat
        // In a real application, you would calculate damage, check for destruction, etc.
        // Here we simply remove the enemy ship for demonstration purposes
        Ships.Remove(Ships.First(s => s.Ship == enemyShip));
    }
}

public partial class ShipViewModel : ObservableObject
{
    [ObservableProperty] private Ship _ship;
    [ObservableProperty] private bool _showInventory;
}


public class ProductionModule
{
    public required string Name { get; set; }
    public List<WareQuantity> Input { get; set; } = [];
    public List<WareQuantity> Output { get; set; } = [];
    public long TimeToBuild { get; set; }
}

public class Station : IFollowOrders
{
    public required string Name { get; set; }
    public long StorageCapacity { get; set; }
    public Vector3 Location { get; set; }
    public List<WareQuantity> Inventory { get; set; } = [];
    public List<Ship> LandedShips { get; set; } = [];
    public List<ProductionModule> ProductionModules { get; set; } = [];
    public List<Weapon> Weapons { get; set; } = [];
    public List<BuildProgress> BuildProgresses { get; set; } = [];
}

public class Ship : IFollowOrders
{
    public required string Name { get; set; }
    public long StorageCapacity { get; set; }
    public Vector3 Location { get; set; }
    public List<WareQuantity> Inventory { get; set; } = [];
    public List<Weapon> Weapons { get; set; } = [];
    public List<QueuedOrder> Orders { get; set; } = [];
    public List<FutureWareTransaction> InventoryFutureCredits { get; set; } = [];// "Incoming"
    public List<FutureWareTransaction> InventoryFutureDebits { get; set; } = []; // "On Hold"
}

public class QueuedOrder
{
    //This class will be used to queue up orders for ships to execute.
    //probably just a bunch of actions. One of them will be to move to a location. Another will be attack or trade.
}

public interface IOrder
{
    string Name { get; set; }
    bool Completed { get; set; }
    bool CanExecute(IFollowOrders s);
    void Execute(IFollowOrders s);
}

public interface IFollowOrders
{

}

public class FutureWareTransaction
{
    public required WareQuantity WareQuantity { get; set; }
    public Ship? Ship { get; set; }
    public Station? Station { get; set; }
}

public class WareQuantity
{
    public required Ware Ware { get; set; }
    public long Quantity { get; set; }
}

public class BuildRecipe
{
    public required string Name { get; set; }
    public List<WareQuantity> WareQuantities { get; set; } = [];
    public long TimeToBuild { get; set; }
}

public class BuildProgress
{
    public required BuildRecipe BuildRecipe { get; set; }
    public long TimeRemaining { get; set; }
    public bool InProgress { get; set; } //so we know whether to decrement TimeRemaining
}

public class Ware(string name, long volume, long mass)
{
    public static readonly Ware Water = new("Water", 1, 1);
    public static readonly Ware Food = new("Food", 1, 1);
    public static readonly Ware Energy = new("Energy", 1, 1);
    public static readonly Ware HullParts = new("HullParts", 1, 1);
    public static readonly Ware WeaponComponents = new("WeaponComponents", 1, 1);
    public static readonly Ware Steel = new("Steel", 1, 1);

    public string Name { get; set; } = name;
    public long Volume { get; set; } = volume;
    public long Mass { get; set; } = mass;
}

public class Weapon
{
    public required string Name { get; set; }
    public float Damage { get; set; }
    public List<DamageQuantity> Damages { get; set; } = [];
    public float Range { get; set; }
    public float FireRate { get; set; }
}

public class DamageQuantity
{
    public required DamageType DamageType { get; set; }
    public float Quantity { get; set; }
}

public class DamageType
{
    public static DamageType Kinetic = new();
    public static DamageType Energy = new();
}