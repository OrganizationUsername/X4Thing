using System.Numerics;

namespace FactoryCli;

public class Transporter : IUpdatable
{
    public Vector2 Position { get; set; }
    public float SpeedPerTick { get; set; } = 1f;
    public string Log { get; private set; } = ""; //probably replace this with List<string>
    public int PlayerId { get; set; } = 0;
    public int Id { get; set; } = 0;
    public float MaxVolume { get; set; } = 10f;
    //ToDo: maybe make it so they have a bulk volume and a container volume, that way they can carry a bunch of asteroids but fewer finished/containerized goods

    public List<ResourceAmount> Carrying { get; } = [];

    private readonly Queue<TransportTask> _taskQueue = new();
    private TransportTask? _currentTask;
    private Vector2? _target;

    public bool HasActiveTask() => _currentTask != null || _taskQueue.Count > 0;

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
            var usedVolume = Carrying.Sum(c => c.Resource.Volume * c.Amount);
            var remainingVolume = MaxVolume - usedVolume;

            foreach (var item in task.Cargo)
            {
                var volumePerUnit = item.Resource.Volume;
                if (volumePerUnit <= 0) continue;

                var maxUnits = (int)(remainingVolume / volumePerUnit);
                if (maxUnits == 0) break;

                var amountToTake = Math.Min(item.Amount, maxUnits);

                if (amountToTake > 0 && task.Source.TryExport(item.Resource, amountToTake))
                {
                    var existing = Carrying.FirstOrDefault(x => x.Resource == item.Resource);
                    if (existing != null) { existing.Amount += amountToTake; }
                    else { Carrying.Add(new ResourceAmount(item.Resource, amountToTake)); }

                    remainingVolume -= amountToTake * volumePerUnit;
                }
                else { Log += $"[Tick {tick}] Failed to pick up {item.Amount} x {item.Resource.Id}\n"; }
            }

            Log += $"[Tick {tick}] Picked up {string.Join(", ", Carrying)}\n";
            task.HasPickedUp = true;
            _target = task.Destination.Position;
        }
        else
        {
            foreach (var item in Carrying) //we shouldn't be iterating through what it's carrying, but what it's current task is and delivering as many as possible
            {
                var amountToTransfer = _currentTask.Cargo.FirstOrDefault(x => x.Resource == item.Resource)?.Amount;
                if (amountToTransfer is null) { continue; } // No need to transfer this item
                if (item.Amount < amountToTransfer) { amountToTransfer = item.Amount; }
                task.Destination.ReceiveImport(item.Resource, amountToTransfer.Value);
                Log += $"[Tick {tick}] Delivered {item.Amount} x {item.Resource.Id} to {task.Destination.Position}\n";
                item.Amount -= amountToTransfer.Value;
            }
            Carrying.RemoveAll(item => item.Amount == 0);
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