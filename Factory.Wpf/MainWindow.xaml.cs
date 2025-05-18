using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Input;
using JetBrains.Annotations;

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

    private readonly SKFont _font = new() { Size = 14, };
    private readonly SKPaint _textPaint = new() { Color = SKColors.Black, IsAntialias = true, };
    private readonly SKPaint _stationFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.SteelBlue, };
    private readonly SKPaint _shipFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.OrangeRed, };
    private readonly SKPaint _border = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Black, };
    private readonly SKPaint _highlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = SKColors.Magenta, };
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Translate(_pan.X, _pan.Y);
        canvas.Scale(_zoom);
        foreach (var entity in _viewModel.Entities.OfType<FacilityEntity>())
        {
            var x = entity.X;
            var y = entity.Y;

            var rect = new SKRect(x - 15, y - 15, x + 15, y + 15);
            canvas.DrawRect(rect, _stationFill);
            if (entity.IsSelected) canvas.DrawRect(rect, _highlightPaint);
            canvas.DrawRect(rect, _border); //canvas.DrawCircle(x, y, 2, _highlightPaint);
            canvas.DrawText(entity.Name, x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
            canvas.DrawText(entity.Inventory, x + 20, y + 15, SKTextAlign.Left, _font, _textPaint);
            //then, for each ProductionProgresses, show a small bar, 50 pixels wide showing the progress
            var yOffset = 0;
            foreach (var progress in entity.ProductionProgresses)
            {
                //var progressRect = new SKRect(x - 15, y + 30 + yOffset, x + 35, y + 35);
                //canvas.DrawRect(progressRect, _stationFill);
                //canvas.DrawRect(progressRect, _highlightPaint);
                //canvas.DrawRect(progressRect, _border); //canvas.DrawCircle(x, y, 2, _highlightPaint);
                //canvas.DrawText($"{progress.RecipeId} ({progress.Progress:0.00})", x + 20, y + 35, SKTextAlign.Left, _font, _textPaint);
                //fill bar according to 
                var percentage = 1d * progress.Tick / progress.Duration;
                var fillRect = new SKRect(x - 15, y + 30 + yOffset, x - 15 + (float)(percentage * 50), y + 35);
                canvas.DrawRect(fillRect, _highlightPaint);

                yOffset += 15;
            }
        }

        foreach (var entity in _viewModel.Entities.OfType<TransporterEntity>())
        {
            var x = entity.X;
            var y = entity.Y;
            canvas.DrawCircle(x, y, 15, _shipFill);
            if (entity.IsSelected) canvas.DrawCircle(x, y, 15, _highlightPaint);
            canvas.DrawCircle(x, y, 15, _border); //canvas.DrawCircle(x, y, 2, _highlightPaint);
            canvas.DrawText(entity.Name, x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
            canvas.DrawText(entity.Carrying, x + 20, y + 15, SKTextAlign.Left, _font, _textPaint);
            canvas.DrawText(entity.Destination, x + 20, y + 25, SKTextAlign.Left, _font, _textPaint);
        }

        //ShowTooltip(canvas);

        if (_debugPoint == default) { return; }

        canvas.DrawCircle(_debugPoint.X, _debugPoint.Y, 3, _highlightPaint); //draw a small circle at _debugPoint
        canvas.DrawText("Click", _debugPoint.X + 5, _debugPoint.Y + 5, SKTextAlign.Left, _font, _textPaint);
    }

    [UsedImplicitly]
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