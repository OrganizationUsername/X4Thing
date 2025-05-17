using System.Numerics;

namespace FactoryCli;

public class Transporter : IUpdatable
{
    public Vector2 Position { get; set; }
    public float SpeedPerTick { get; set; } = 1f;
    public string Log { get; private set; } = ""; //probably replace this with List<string>
    public int PlayerId { get; set; } = 0;
    public int Id { get; set; } = 0;

    public List<ResourceAmount> Carrying { get; } = [];

    private readonly Queue<TransportTask> _taskQueue = new();
    private TransportTask? _currentTask;
    private Vector2? _target;

    public void AssignTask(ProductionFacility from, ProductionFacility to, List<ResourceAmount> cargo)
    {
        var task = new TransportTask(from, to, cargo);
        _taskQueue.Enqueue(task);
        Log += $"Enqueued transport: {string.Join(", ", cargo)} from {from.Position} to {to.Position}\n";
    }

    public void Tick(int tick)
    {
        if (_currentTask is null)
        {
            if (_taskQueue.Count == 0) { return; }

            _currentTask = _taskQueue.Dequeue();
            _target = _currentTask.Source.Position;
        }

        if (_target is null) { return; }

        var task = _currentTask!;
        var direction = _target.Value - Position;
        var distance = direction.Length();

        if (distance > SpeedPerTick)
        {
            Position += Vector2.Normalize(direction) * SpeedPerTick;
            return;
        }

        Position = _target.Value; // Arrived at target

        if (!task.HasPickedUp)
        {
            Carrying.Clear();
            foreach (var item in task.Cargo)
            {
                if (task.Source.TryExport(item.Resource, item.Amount)) { Carrying.Add(new ResourceAmount(item.Resource, item.Amount)); }
                else { Log += $"[Tick {tick}] Failed to pick up {item.Amount} x {item.Resource.Id}\n"; }
            }

            Log += $"[Tick {tick}] Picked up {string.Join(", ", Carrying)}\n";
            task.HasPickedUp = true;
            _target = task.Destination.Position;
        }
        else
        {
            foreach (var item in Carrying) { task.Destination.ReceiveImport(item.Resource, item.Amount); }

            Log += $"[Tick {tick}] Delivered {string.Join(", ", Carrying)}\n";
            Carrying.Clear();
            _currentTask = null;
            _target = null;
        }
    }
}

public class ResourceAmount(Resource resource, int amount)
{
    public Resource Resource { get; set; } = resource;
    public int Amount { get; set; } = amount;

    public override string ToString() => $"{Amount} x {Resource.Id}";
}

public class TransportTask(ProductionFacility source, ProductionFacility dest, List<ResourceAmount> cargo)
{
    public ProductionFacility Source { get; } = source;
    public ProductionFacility Destination { get; } = dest;
    public List<ResourceAmount> Cargo { get; } = cargo;

    public bool HasPickedUp { get; set; } = false;
}