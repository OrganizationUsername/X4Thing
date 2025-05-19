using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;

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

        var clicked = GetClickedEntity(world);

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
            return entity is FacilityEntity ? Math.Abs(dx) <= 15 && Math.Abs(dy) <= 15 : Math.Sqrt(dx * dx + dy * dy) <= 15;
        });

        if (hovered == _viewModel.HoveredEntity) { return; }

        _viewModel.HoveredEntity = hovered;
        Canvas.InvalidateVisual();

    }
    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        //var scaledFont = new SKFont { Size = 14 * _zoom, }; //Should get this working at some point
        _textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, };
        var delta = e.Delta > 0 ? 1.1f : 0.9f; _zoom *= delta; var pos = e.GetPosition(Canvas); var mouse = new SKPoint((float)pos.X, (float)pos.Y); _pan = new SKPoint(mouse.X - (mouse.X - _pan.X) * delta, mouse.Y - (mouse.Y - _pan.Y) * delta); Canvas.InvalidateVisual();
    }

    private readonly SKFont _font = new() { Size = 14, };
    private SKPaint _textPaint = new() { Color = SKColors.Black, IsAntialias = true, };
    private readonly SKPaint _stationFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.SteelBlue, };
    private readonly SKPaint _shipFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.DarkBlue, };
    private readonly SKPaint _fighterFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.Orange, };

    private readonly SKPaint _border = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Black, };
    private readonly SKPaint _highlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = SKColors.Magenta, };

    private readonly SKPaint _deadShipFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.Gray, };
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Translate(_pan.X, _pan.Y);
        canvas.Scale(_zoom);
        DrawFacilities(canvas);
        DrawTransporters(canvas);
        DrawFighters(canvas);

        //ShowTooltip(canvas);

        if (_debugPoint == default) { return; }

        canvas.DrawCircle(_debugPoint.X, _debugPoint.Y, 3, _highlightPaint); //draw a small circle at _debugPoint
        canvas.DrawText("Click", _debugPoint.X + 5, _debugPoint.Y + 5, SKTextAlign.Left, _font, _textPaint);
    }

    private void DrawFighters(SKCanvas canvas)
    {
        foreach (var entity in _viewModel.Entities.OfType<FighterEntity>())
        {
            var x = entity.X;
            var y = entity.Y;
            canvas.DrawCircle(x, y, 15, entity.HullRemaining > 0 ? _fighterFill : _deadShipFill);
            if (entity.IsSelected) { canvas.DrawCircle(x, y, 15, _highlightPaint); }
            canvas.DrawCircle(x, y, 15, _border);
            canvas.DrawText(entity.Name, x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
        }
    }

    private void DrawTransporters(SKCanvas canvas)
    {
        foreach (var entity in _viewModel.Entities.OfType<TransporterEntity>())
        {
            var x = entity.X;
            var y = entity.Y;
            canvas.DrawCircle(x, y, 15, entity.HullRemaining > 0 ? _shipFill : _deadShipFill);
            if (entity.IsSelected) canvas.DrawCircle(x, y, 15, _highlightPaint);
            canvas.DrawCircle(x, y, 15, _border);
            canvas.DrawText(entity.Name, x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
            canvas.DrawText(entity.Carrying, x + 20, y + 15, SKTextAlign.Left, _font, _textPaint);
            canvas.DrawText(entity.Destination, x + 20, y + 25, SKTextAlign.Left, _font, _textPaint);
        }
    }

    private void DrawFacilities(SKCanvas canvas)
    {
        foreach (var entity in _viewModel.Entities.OfType<FacilityEntity>())
        {
            var x = entity.X;
            var y = entity.Y;

            var rect = new SKRect(x - 15, y - 15, x + 15, y + 15);
            canvas.DrawRect(rect, _stationFill);
            if (entity.IsSelected) { canvas.DrawRect(rect, _highlightPaint); }
            canvas.DrawRect(rect, _border);

            canvas.DrawText(entity.Name, x + 20, y + 5, SKTextAlign.Left, _font, _textPaint);
            var textYOffset = 20f;
            if (_viewModel.ShowInventory || _viewModel.HoveredEntity == entity)
            {
                canvas.DrawText(entity.Inventory, x + 20, y + 20, SKTextAlign.Left, _font, _textPaint);
                textYOffset += 15;
            }

            if (!_viewModel.ShowAllProduction && _viewModel.HoveredEntity != entity) { continue; }

            const float spacing = 20f;

            foreach (var progress in entity.ProductionProgresses)
            {
                var recipeName = progress.Recipe.Output.DisplayName;
                _font.MeasureText(recipeName, out var textBounds);
                var textX = x + 20;
                var barX = textX + textBounds.Width + 5;
                var barY = y + textYOffset;
                const float barHeight = 5f;
                const float barWidth = 50f;

                var fillPercent = Math.Clamp(progress.Tick / (float)progress.Duration, 0f, 1f);
                var fillRect = new SKRect(barX, barY, barX + fillPercent * barWidth, barY + barHeight);
                canvas.DrawText(recipeName, textX, barY + barHeight, SKTextAlign.Left, _font, _textPaint);
                canvas.DrawRect(fillRect, _highlightPaint);
                textYOffset += spacing;
            }
        }
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
            IsAntialias = true,
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

    private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = e.GetPosition(this); // Relative to the window

        var contextMenu = new ContextMenu
        {
            Placement = PlacementMode.Relative,
            PlacementTarget = this,
            HorizontalOffset = screenPoint.X,
            VerticalOffset = screenPoint.Y,
            StaysOpen = false,
        };

        // Always-available menu item
        var resetItem = new MenuItem { Header = "Reset View", };
        resetItem.Click += (_, _) => ResetView();
        contextMenu.Items.Add(resetItem);
        contextMenu.Items.Add(new Separator());

        // Check for clicked entity
        var screen = e.GetPosition(Canvas).ToSKPoint();
        var world = ScreenToWorld(screen);

        var clickedEntity = GetClickedEntity(world);

        if (clickedEntity is not null)
        {
            var inspectItem = new MenuItem { Header = $"Inspect {clickedEntity.Name}", };
            inspectItem.Click += (_, _) => MessageBox.Show($"Inspecting {clickedEntity.Name}");
            contextMenu.Items.Add(inspectItem);
        }
        else
        {
            var noneItem = new MenuItem { Header = "No entity here", IsEnabled = false, };
            contextMenu.Items.Add(noneItem);
        }

        contextMenu.IsOpen = true;
    }

    private Entity? GetClickedEntity(SKPoint world) => _viewModel.Entities.FirstOrDefault(entity => { var dx = entity.X - world.X; var dy = entity.Y - world.Y; return entity is FacilityEntity ? Math.Abs(dx) <= 15 && Math.Abs(dy) <= 15 : Math.Sqrt(dx * dx + dy * dy) <= 15; });
}