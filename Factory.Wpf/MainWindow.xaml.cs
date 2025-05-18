using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactoryCli;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Factory.Wpf;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _viewModel = vm;

        _viewModel.RequestRedraw += () => { canvas.InvalidateVisual(); };
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        foreach (var entity in _viewModel.Entities)
        {
            using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = entity.IsStation ? SKColors.SteelBlue : SKColors.OrangeRed, };

            using var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Black, };

            if (entity.IsStation)
            {
                canvas.DrawRect(entity.X - 15, entity.Y - 15, 30, 30, fill);
                canvas.DrawRect(entity.X - 15, entity.Y - 15, 30, 30, border);
            }
            else
            {
                canvas.DrawCircle(entity.X, entity.Y, 15, fill);
                canvas.DrawCircle(entity.X, entity.Y, 15, border);
            }

            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 14, IsAntialias = true, };

            canvas.DrawText(entity.IsStation ? "Station" : "Ship", entity.X + 20, entity.Y + 5, textPaint);
        }
    }
}

public partial class Entity : ObservableObject
{
    [ObservableProperty] private float x;
    [ObservableProperty] private float y;
    [ObservableProperty] private bool _isStation;
}

public partial class MainViewModel : ObservableObject
{
    public event Action? RequestRedraw;

    private readonly GameData _gameData;
    private readonly Ticker _ticker;
    private const int Scale = 5;

    public MainViewModel()
    {
        _gameData = GameData.GetDefault();
        _ticker = new Ticker { GameData = _gameData, };

        // Setup simulation
        SetupSimulation();

        // Register everything
        foreach (var f in _gameData.Facilities) _ticker.Register(f);
        foreach (var t in _gameData.Transporters) _ticker.Register(t);
    }

    public ObservableCollection<Entity> Entities { get; set; } = [];

    private void SetupSimulation()
    {
        var ore = _gameData.GetResource("ore");
        var energy = _gameData.GetResource("energy_cell");
        var sand = _gameData.GetResource("sand");
        var plastic = _gameData.GetResource("plastic");

        var a = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_metal_bar"), 2 }, }) { Name = "A", Position = new Vector2(50, 50), };
        var b = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_computer_part"), 1 }, }) { Name = "B", Position = new Vector2(55, 50), };
        var c = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_silicon_wafer"), 2 }, }) { Name = "C", Position = new Vector2(60, 50), };
        var d = new ProductionFacility(new ResourceStorage(), new() { { _gameData.GetRecipe("recipe_ai_module"), 1 }, }) { Name = "D", Position = new Vector2(65, 50), };

        a.GetStorage().Add(ore, 100);
        a.GetStorage().Add(energy, 100);
        b.GetStorage().Add(plastic, 10);
        c.GetStorage().Add(sand, 100);
        c.GetStorage().Add(energy, 100);

        _gameData.Facilities.AddRange([a, b, c, d,]);

        var transporter = new Transporter { Id = 1, Position = new Vector2(30, 30), SpeedPerTick = 1f, MaxVolume = 50f, };
        _gameData.Transporters.Add(transporter);

        // Link simulation entities to UI
        foreach (var f in _gameData.Facilities)
        {
            Entities.Add(new Entity { X = f.Position.X * Scale, Y = f.Position.Y * Scale, IsStation = true, });
        }

        foreach (var t in _gameData.Transporters)
        {
            Entities.Add(new Entity { X = t.Position.X * Scale, Y = t.Position.Y * Scale, IsStation = false, });
        }
    }


    [RelayCommand]
    private void Tick()
    {
        _ticker.RunTicks(1);
        var transporters = _gameData.Transporters;
        foreach (var entity in Entities.Where(e => !e.IsStation))
        {
            var corresponding = transporters.First();
            entity.X = corresponding.Position.X * Scale;
            entity.Y = corresponding.Position.Y * Scale;
        }

        RequestRedraw?.Invoke();
    }
}