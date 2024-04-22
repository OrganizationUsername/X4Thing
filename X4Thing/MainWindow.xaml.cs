using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace X4Thing;

public partial class MainWindow
{
    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        DataContext = mainWindowViewModel;
        InitializeComponent();
    }
}

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "X4Thing";
    public long Money { get; set; } = 100;

    public ObservableCollection<Ship> Ships { get; set; } = [];
    [ObservableProperty] private Ship? _selectedShip;
    public ObservableCollection<Station> Stations { get; set; } = [];

    public MainWindowViewModel()
    {
        //My proof of concept will be moving the ship to the station, depositing inventory, and creating a weapon component.
        //The starting ship will hold some things that the weapon-building station won't accept.
        //Then we use the weapon component to finish building a mining ship.
        //Then the user sends the mining ship to mine resources to finish building a trade ship.
        //Then the user sends the trade ship to trade resources to build a combat ship.
        //Then the user sends the combat ship to destroy an enemy ship.

        Ships.Add(new Ship
        {
            Name = "S1",
            StorageCapacity = 100,
            Location = new Vector3(0, 0, 0),
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
        });

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

    //add a method to move the ship to the station

    //add a method to deposit inventory

    //add a method to create a weapon component

    //add a method to build a ship

    //add a method to send a ship to mine resources

    //add a method to send a ship to trade resources

    //add a method to send a ship to destroy an enemy ship
}

public class ProductionModule
{
    public required string Name { get; set; }
    public List<WareQuantity> Input { get; set; } = [];
    public List<WareQuantity> Output { get; set; } = [];
    public long TimeToBuild { get; set; }
}

public class Station
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

public class Ship
{
    public required string Name { get; set; }
    public long StorageCapacity { get; set; }
    public Vector3 Location { get; set; }
    public List<WareQuantity> Inventory { get; set; } = [];
    public List<Weapon> Weapons { get; set; } = [];
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
    public static Ware Water = new("Water", 1, 1);
    public static Ware Food = new("Food", 1, 1);
    public static Ware Energy = new("Energy", 1, 1);
    public static Ware HullParts = new("HullParts", 1, 1);
    public static Ware WeaponComponents = new("WeaponComponents", 1, 1);
    public static Ware Steel = new("Steel", 1, 1);

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