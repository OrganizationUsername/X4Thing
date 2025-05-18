using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactoryCli;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;

namespace Factory.Wpf;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;

    private float _zoom = 1f;
    private SKPoint _pan = new(0, 0);
    private SKPoint _lastDrag;
    private bool _isDragging;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _viewModel = vm;

        _viewModel.RequestRedraw += () => Canvas.InvalidateVisual();
        _viewModel.ResetCanvasView += ResetView;

        Canvas.MouseWheel += Canvas_MouseWheel;
        Canvas.MouseDown += Canvas_MouseDown;
        Canvas.MouseMove += Canvas_MouseMove;
        Canvas.MouseUp += Canvas_MouseUp;

        Loaded += (_, _) => Canvas.Focus(); // 👈 Force focus on startup
    }

    private void ResetView()
    {
        _zoom = 1f;
        _pan = new SKPoint(0, 0);
        Canvas.InvalidateVisual();
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { return; }

        _lastDrag = e.GetPosition(Canvas).ToSKPoint();
        _isDragging = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) { return; }

        var current = e.GetPosition(Canvas).ToSKPoint();
        var delta = current - _lastDrag;
        _pan += delta;
        _lastDrag = current;

        Canvas.InvalidateVisual();
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? 1.1f : 0.9f;
        _zoom *= delta;

        var pos = e.GetPosition(Canvas);
        var mouse = new SKPoint((float)pos.X, (float)pos.Y);

        _pan = new SKPoint(mouse.X - (mouse.X - _pan.X) * delta, mouse.Y - (mouse.Y - _pan.Y) * delta);

        Canvas.InvalidateVisual();
    }

    SKFont font = new SKFont { Size = 14, };
    SKPaint textPaint = new SKPaint { Color = SKColors.Black, TextSize = 14, IsAntialias = true, }; //'SKPaint.TextSize' is obsolete: 'Use SKFont.Size instead.'
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Translate(_pan.X, _pan.Y);
        canvas.Scale(_zoom);

        foreach (var entity in _viewModel.Entities)
        {
            var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = entity.IsStation ? SKColors.SteelBlue : SKColors.OrangeRed, };
            var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Black, };

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
            canvas.DrawText(entity.IsStation ? "Station" : "Ship", entity.X + 20, entity.Y + 5, SKTextAlign.Left, font, textPaint);
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
    public event Action? ResetCanvasView;

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
    private void ResetView()
    {
        ResetCanvasView?.Invoke();
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