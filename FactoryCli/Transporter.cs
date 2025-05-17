using System.Numerics;

namespace FactoryCli;

public class Transporter : IUpdatable, IHasName
{
    public Vector2 Position { get; set; }
    public float SpeedPerTick { get; set; } = 1f;
    public int PlayerId { get; set; } = 0;
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "Transporter";
    public float MaxVolume { get; set; } = 10f;
    //ToDo: maybe make it so they have a bulk volume and a container volume, that way they can carry a bunch of asteroids but fewer finished/containerized goods

    public List<ILogLine> LogLines { get; } = [];

    public List<ResourceAmount> Carrying { get; } = [];

    private readonly Queue<TransportTask> _taskQueue = new();
    private TransportTask? _currentTask;
    private Vector2? _target;

    public bool HasActiveTask() => _currentTask != null || _taskQueue.Count > 0;

    public void AssignTask(ProductionFacility from, ProductionFacility to, List<ResourceAmount> cargo, int? currentTick = null)
    {
        var task = new TransportTask(from, to, cargo);
        _taskQueue.Enqueue(task);
        LogLines.Add(new TransportAssignedLog(currentTick ?? 0, Id, cargo.First().Resource.Id, cargo.Sum(x => x.Amount), from, to));
    }

    public void NewAssignTask(ProductionFacility source, ProductionFacility dest, List<ResourceAmount> cargo, int currentTick = 0)
    {
        var availableVolume = MaxVolume - Carrying.Sum(c => c.Resource.Volume * c.Amount);
        var fitting = new List<ResourceAmount>();
        var overflow = new List<ResourceAmount>();

        foreach (var item in cargo)
        {
            var perUnitVol = item.Resource.Volume;
            if (perUnitVol <= 0) continue;

            var maxUnits = (int)(availableVolume / perUnitVol);
            var assignAmount = Math.Min(maxUnits, item.Amount);

            if (assignAmount > 0)
            {
                fitting.Add(new ResourceAmount(item.Resource, assignAmount));
                availableVolume -= assignAmount * perUnitVol;
            }

            var remaining = item.Amount - assignAmount;
            if (remaining > 0)
            {
                overflow.Add(new ResourceAmount(item.Resource, remaining));
            }
        }

        if (fitting.Count > 0) { _taskQueue.Enqueue(new TransportTask(source, dest, fitting)); }

        if (overflow.Count > 0)
        {
            // Optional: delay re-queueing for game balance purposes
            _taskQueue.Enqueue(new TransportTask(source, dest, overflow));
            LogLines.Add(new TransportSplitLog(tick: currentTick, transporterId: this.Id, originalCargo: cargo, assignedNow: fitting, remaining: overflow));
        }
    }

    private float _distanceTraveled = 0f;

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
            _distanceTraveled += SpeedPerTick;
            return;
        }
        else
        {
            _distanceTraveled += distance;
            Position = _target.Value;
            _target = null;
        }

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

                if (amountToTake > 0 && task.Source.TryExport(item.Resource, amountToTake, tick, this))
                {
                    var existing = Carrying.FirstOrDefault(x => x.Resource == item.Resource);
                    if (existing != null) { existing.Amount += amountToTake; }
                    else { Carrying.Add(new ResourceAmount(item.Resource, amountToTake)); }
                    remainingVolume -= amountToTake * volumePerUnit;

                    LogLines.Add(new PickupLog(tick, Id, [new ResourceAmount(item.Resource, amountToTake),], _currentTask.Source));
                }
                else
                {
                    LogLines.Add(new TransportAssignedLog(tick, Id, item.Resource.Id, item.Amount, task.Source, task.Destination));
                }
            }
            task.HasPickedUp = true;
            _target = task.Destination.Position;
        }
        else
        {
            var failed = new List<ResourceAmount>();

            foreach (var taskItem in task.Cargo)
            {
                var carried = Carrying.FirstOrDefault(c => c.Resource == taskItem.Resource);
                var available = carried?.Amount ?? 0;
                var toDeliver = Math.Min(available, taskItem.Amount);

                if (toDeliver > 0)
                {
                    task.Destination.ReceiveImport(taskItem.Resource, toDeliver, tick, this);
                    LogLines.Add(new DeliveryLog(tick, Id, task.Destination.Position, [new(taskItem.Resource, toDeliver)]));
                    carried!.Amount -= toDeliver;
                }

                if (toDeliver < taskItem.Amount)
                {
                    failed.Add(new ResourceAmount(taskItem.Resource, taskItem.Amount - toDeliver));
                }
            }

            if (failed.Count > 0)
            {
                LogLines.Add(new DeliveryFailedLog(tick, Id, failed, task.Destination));
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