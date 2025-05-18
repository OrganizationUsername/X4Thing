using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Collections.ObjectModel;

namespace Factory.Wpf;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;
    public MainWindow(MainViewModel vm)
    {
        DataContext = vm;
        _viewModel = vm;
        InitializeComponent();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        foreach (var entity in _viewModel.Entities)
        {
            var fill = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = entity.IsStation ? SKColors.SteelBlue : SKColors.OrangeRed
            };

            var border = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = SKColors.Black
            };

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

            var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 14, IsAntialias = true };

            canvas.DrawText(entity.IsStation ? "Station" : "Ship", entity.X + 20, entity.Y, textPaint); //this is obsolete?
        }
    }
}

public class RenderableEntity
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsStation { get; set; }
    public string Label => IsStation ? "Station" : "Ship";
}


public partial class Entity : ObservableObject
{
    [ObservableProperty] private float x;
    [ObservableProperty] private float y;
    [ObservableProperty] private bool isStation;
}

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<Entity> Entities { get; } =
    [
        new() { X = 100, Y = 100, IsStation = true, },
        new() { X = 200, Y = 150, IsStation = false, },
        new() { X = 300, Y = 200, IsStation = true, },
        new() { X = 250, Y = 250, IsStation = false, },
    ];

    [RelayCommand]
    private void Tick()
    {

    }
}