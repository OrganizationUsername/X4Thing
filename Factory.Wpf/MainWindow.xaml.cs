using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FactoryCli;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Threading;

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
        Canvas.MouseUp += (_, _) => _isDragging = false;
        Canvas.MouseLeave += (_, _) => _isDragging = false;

        Loaded += (_, _) => Canvas.Focus();
    }

    private void ResetView() { _zoom = 1f; _pan = new SKPoint(0, 0); Canvas.InvalidateVisual(); }

    private SKPoint ScreenToWorld(SKPoint screen) => new((screen.X - _pan.X) / _zoom, (screen.Y - _pan.Y) / _zoom); //_debugPoint = world; //Trace.WriteLine($"[Pan] X: {_pan.X:0.00}, Y: {_pan.Y:0.00} | [Zoom] {_zoom:0.00}"); //Trace.WriteLine($"[Click] Screen: {screen}, World: {world}");

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var screen = e.GetPosition(Canvas).ToSKPoint();
        var world = ScreenToWorld(screen);

        _isDragging = true;
        _lastDrag = screen;

        if (e.LeftButton != MouseButtonState.Pressed) { return; }

        foreach (var entity in _viewModel.Entities)
        {
            var dx = entity.X - world.X;
            var dy = entity.Y - world.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            Trace.WriteLine($"  → Entity @ ({entity.X}, {entity.Y}) | Δ=({dx:0.00}, {dy:0.00}) | Distance: {dist:0.00}");
        }

        var clicked = _viewModel.Entities.FirstOrDefault(entity =>
        {
            var dx = entity.X - world.X;
            var dy = entity.Y - world.Y;
            return entity.IsStation ? Math.Abs(dx) <= 15 && Math.Abs(dy) <= 15 : Math.Sqrt(dx * dx + dy * dy) <= 15;
        });

        foreach (var entity in _viewModel.Entities)
        {
            entity.IsSelected = false;
        }

        if (clicked is not null) { clicked.IsSelected = true; Trace.WriteLine($"✅ Selected: {clicked}"); }
        else { Trace.WriteLine("❌ No match found."); }

        Canvas.InvalidateVisual();
    }

    /* ReSharper disable once FieldCanBeMadeReadOnly.Local */
    private Vector2 _debugPoint = default;

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var current = e.GetPosition(Canvas).ToSKPoint();
            var delta = current - _lastDrag;
            _pan += delta;
            _lastDrag = current;
            Canvas.InvalidateVisual();
        }
        var screen = e.GetPosition(Canvas).ToSKPoint();
        var world = ScreenToWorld(screen);

        var hovered = _viewModel.Entities.FirstOrDefault(entity =>
        {
            var dx = entity.X - world.X;
            var dy = entity.Y - world.Y;
            return entity.IsStation ? Math.Abs(dx) <= 15 && Math.Abs(dy) <= 15 : Math.Sqrt(dx * dx + dy * dy) <= 15;
        });

        if (hovered != _viewModel.HoveredEntity)
        {
            _viewModel.HoveredEntity = hovered;
            Canvas.InvalidateVisual();
        }

    }
    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e) { var delta = e.Delta > 0 ? 1.1f : 0.9f; _zoom *= delta; var pos = e.GetPosition(Canvas); var mouse = new SKPoint((float)pos.X, (float)pos.Y); _pan = new SKPoint(mouse.X - (mouse.X - _pan.X) * delta, mouse.Y - (mouse.Y - _pan.Y) * delta); Canvas.InvalidateVisual(); }

    private readonly SKFont _font = new SKFont { Size = 14, };
    private readonly SKPaint _textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, }; //'SKPaint.TextSize' is obsolete: 'Use SKFont.Size instead.'
    private readonly SKPaint _stationFill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.SteelBlue, };
    private readonly SKPaint _shipFill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.OrangeRed, };
    private readonly SKPaint _border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Black, };
    private readonly SKPaint _highlightPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = SKColors.Magenta, };
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Translate(_pan.X, _pan.Y);
        canvas.Scale(_zoom);

        foreach (var entity in _viewModel.Entities)
        {
            var x = entity.X;
            var y = entity.Y;

            if (entity.IsStation)
            {
                var rect = new SKRect(x - 15, y - 15, x + 15, y + 15);
                canvas.DrawRect(rect, _stationFill);
                if (entity.IsSelected) canvas.DrawRect(rect, _highlightPaint);
                canvas.DrawRect(rect, _border); //canvas.DrawCircle(x, y, 2, _highlightPaint);
            }
            else
            {
                canvas.DrawCircle(x, y, 15, _shipFill);
                if (entity.IsSelected) canvas.DrawCircle(x, y, 15, _highlightPaint);
                canvas.DrawCircle(x, y, 15, _border); //canvas.DrawCircle(x, y, 2, _highlightPaint);
            }

            canvas.DrawText(entity.IsStation ? "Station" : "Ship", x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
        }


        //ShowTooltip(canvas);


        if (_debugPoint == default) { return; }

        canvas.DrawCircle(_debugPoint.X, _debugPoint.Y, 3, _highlightPaint); //draw a small circle at _debugPoint
        canvas.DrawText("Click", _debugPoint.X + 5, _debugPoint.Y + 5, SKTextAlign.Left, _font, _textPaint);
    }

    private void ShowTooltip(SKCanvas canvas)
    {
        if (_viewModel.HoveredEntity is not { } hovered) { return; } //Really shitty tooltip

        var tooltip = $"{hovered.Name} @ ({hovered.X:0}, {hovered.Y:0})";
        var x = hovered.X + 20;
        var y = hovered.Y - 20;

        var bgPaint = new SKPaint
        {
            Color = SKColors.Gold,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Measure text using the font
        _font.MeasureText(tooltip, out var bounds);

        // Offset bounds to match text position
        bounds.Offset(x, y);

        // Inflate for padding
        bounds.Inflate(4, 4);

        // Draw tooltip background and text
        canvas.DrawRect(bounds, bgPaint);
        canvas.DrawText(tooltip, x, y + _font.Size, SKTextAlign.Left, _font, _textPaint);
    }
}

public partial class Entity : ObservableObject
{
    [ObservableProperty] private float x;
    [ObservableProperty] private float y;
    [ObservableProperty] private bool _isStation;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _name = string.Empty;
}

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

    [ObservableProperty] private int _tickCount = 1;

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

        var transporter = new Transporter { Id = 1, Position = new Vector2(30, 30), SpeedPerTick = 1f, MaxVolume = 50f, };
        _gameData.Transporters.Add(transporter);

        foreach (var f in _gameData.Facilities)
        {
            Entities.Add(new Entity { X = f.Position.X, Y = f.Position.Y, IsStation = true, Name = f.Name, });
        }

        foreach (var t in _gameData.Transporters)
        {
            Entities.Add(new Entity { X = t.Position.X, Y = t.Position.Y, IsStation = false, Name = t.Name, });
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetCanvasView?.Invoke();
    }

    [ObservableProperty] private string _debugText = string.Empty;

    private int _cumulativeTick = 0;
    [RelayCommand]
    private void Tick()
    {
        _ticker.RunTicks(TickCount);

        foreach (var log in _gameData.GetAllLogs(_cumulativeTick))
        {
            Trace.WriteLine(log.Format());
        }
        DebugText = string.Join(Environment.NewLine, _gameData.GetAllLogs(_cumulativeTick).Select(l => l.Format()));
        _cumulativeTick += TickCount;
        var transporters = _gameData.Transporters;
        foreach (var entity in Entities.Where(e => !e.IsStation))
        {
            var corresponding = transporters.First(); //There's no way this is right.
            entity.X = corresponding.Position.X;
            entity.Y = corresponding.Position.Y;
        }

        RequestRedraw?.Invoke();
    }
}