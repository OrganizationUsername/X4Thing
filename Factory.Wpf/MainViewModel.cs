using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factory.Core;

namespace Factory.Wpf;

public partial class MainViewModel : ObservableObject
{
    public event Action? RequestRedraw;
    public event Action? ResetCanvasView;

    [ObservableProperty] private Entity? _hoveredEntity;
    [ObservableProperty] private bool _isAutoTicking;

    private readonly GameData _gameData;
    private readonly Ticker _ticker;
    private readonly DispatcherTimer _dispatcherTimer;
    public MainViewModel()
    {
        _gameData = GameData.GetDefault();
        _ticker = new Ticker { GameData = _gameData, };

        SetupSimulation();

        foreach (var f in _gameData.Facilities) _ticker.Register(f);
        foreach (var t in _gameData.Transporters) _ticker.Register(t);

        _dispatcherTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100), };
        _dispatcherTimer.Tick += (_, _) => Tick();
    }

    [ObservableProperty] private int _tickStep = 25;

    partial void OnIsAutoTickingChanged(bool value) { if (value) { _dispatcherTimer.Start(); } else { _dispatcherTimer.Stop(); } }

    public ObservableCollection<Entity> Entities { get; set; } = [];

    private void SetupSimulation()
    {
        var ore = _gameData.GetResource("ore");
        var energy = _gameData.GetResource("energy_cell");
        var sand = _gameData.GetResource("sand");
        var plastic = _gameData.GetResource("plastic");

        var a = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_metal_bar"), 2 }, }) { Name = "A", Position = new Vector2(150, 150), };
        var b = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_computer_part"), 1 }, }) { Name = "B", Position = new Vector2(95, 230), };
        var c = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_silicon_wafer"), 2 }, }) { Name = "C", Position = new Vector2(30, 60), };
        var d = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_ai_module"), 1 }, }) { Name = "D", Position = new Vector2(165, 20), };

        a.GetStorage().Add(ore, 100);
        a.GetStorage().Add(energy, 100);
        b.GetStorage().Add(plastic, 10);
        c.GetStorage().Add(sand, 100);
        c.GetStorage().Add(energy, 100);

        _gameData.Facilities.AddRange([a, b, c, d,]);

        _gameData.Transporters.Add(new Transporter { Id = 1, Position = new Vector2(30, 30), SpeedPerTick = 1f, MaxVolume = 50f, Name = "1", });
        _gameData.Transporters.Add(new Transporter { Id = 2, Position = new Vector2(230, 230), SpeedPerTick = 1f, MaxVolume = 50f, Name = "2", });

        foreach (var f in _gameData.Facilities)
        {
            Entities.Add(new FacilityEntity { X = f.Position.X, Y = f.Position.Y, IsStation = true, Name = f.Name, });
        }

        foreach (var t in _gameData.Transporters)
        {
            Entities.Add(new TransporterEntity { X = t.Position.X, Y = t.Position.Y, IsStation = false, Name = t.Name, });
        }
    }

    [RelayCommand] private void ResetView() => ResetCanvasView?.Invoke();

    [ObservableProperty] private string _debugText = string.Empty;

    private int _cumulativeTick;
    [RelayCommand]
    private void Tick()
    {
        _ticker.RunTicks(TickStep);

        DebugText = string.Join(Environment.NewLine, _gameData.GetAllLogs(_cumulativeTick).Select(l => l.Format()));
        _cumulativeTick += TickStep;
        UpdateTransporters();
        UpdateFacilities();

        RequestRedraw?.Invoke();
    }

    private void UpdateFacilities()
    {
        foreach (var entity in Entities.OfType<FacilityEntity>())
        {
            var matching = _gameData.Facilities.FirstOrDefault(f => f.Name == entity.Name);
            if (matching is null) continue;

            entity.X = matching.Position.X;
            entity.Y = matching.Position.Y;

            var inventoryLines = matching.GetStorage().GetInventory();
            entity.Inventory = string.Join(", ", inventoryLines);

            entity.ProductionProgresses.Clear();

            var allWorkshops = matching.GetWorkshops();
            var activeJobs = matching.GetProductionJobs();

            foreach (var (recipe, totalWorkshops) in allWorkshops)
            {
                var jobs = activeJobs.TryGetValue(recipe, out var j) ? j : [];
                var activeCount = jobs.Count;
                var idleCount = totalWorkshops - activeCount;

                // Add active jobs
                foreach (var job in jobs)
                {
                    entity.ProductionProgresses.Add(new ProductionProgress { Tick = job.Elapsed, Duration = recipe.Duration, Recipe = recipe, });
                }

                // Add idle "placeholder" jobs
                for (var i = 0; i < idleCount; i++)
                {
                    entity.ProductionProgresses.Add(new ProductionProgress { Tick = 0, Duration = recipe.Duration, Recipe = recipe, });
                }
            }
        }
    }


    private void UpdateTransporters()
    {
        foreach (var entity in Entities.OfType<TransporterEntity>())
        {
            var matching = _gameData.Transporters.FirstOrDefault(t => t.Name == entity.Name);
            if (matching is null) { continue; }
            entity.Carrying = string.Join(", ", matching.Carrying.Select(c => $"{c.Resource.DisplayName} ({c.Amount})"));
            entity.Destination = matching.GetCurrentDestination ?? "";

            entity.X = matching.Position.X;
            entity.Y = matching.Position.Y;
        }
    }
}

public partial class Entity : ObservableObject
{
    [ObservableProperty] private float _x;
    [ObservableProperty] private float _y;
    [ObservableProperty] private bool _isStation;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _name = string.Empty;
}

public partial class TransporterEntity : Entity
{
    [ObservableProperty] private string _carrying = string.Empty;
    [ObservableProperty] private string _destination = string.Empty;
}

public partial class FacilityEntity : Entity
{
    [ObservableProperty] private string _inventory = string.Empty;
    public List<ProductionProgress> ProductionProgresses { get; set; } = [];
}

public class ProductionProgress
{
    public int Tick { get; set; }
    public int Duration { get; set; }
    public Recipe Recipe { get; set; } = null!;
}