using System.Numerics;

namespace Factory.Core;

//ToDo: Make it so the logs from the production facilities and transporters are stored in a sortable way so I can sort them by tick and see what happened at a certain time.
//ToDo: Make it so when transporters take or drop off product that they only take as much as they can hold.

public interface IUpdatable
{
    void Tick(int currentTick);
}
public interface IHasName;

public class Resource
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public float BaseValue { get; init; } = 1.0f;
    public float Volume { get; init; } = 1.0f;

    public override string ToString() => DisplayName;
}
public class Recipe
{
    public string Id { get; init; } = "";
    public Resource Output { get; init; } = null!;
    public int OutputAmount { get; init; }
    public Dictionary<Resource, int> Inputs { get; init; } = new();
    public int Duration { get; init; }
    public float Benefit { get; init; } //kind of a heuristic to help show that creating these things in-house is worth it because it gets us closer to some economic/endProduct goal
}

public class Transporter : IUpdatable, IHasName
{
    public Vector2 Position { get; set; }
    public float SpeedPerTick { get; set; } = 1f;
    public int PlayerId { get; set; } = 0;
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "Transporter";
    public float MaxVolume { get; set; } = 10f;

    public List<ILogLine> LogLines { get; } = [];

    public List<ResourceAmount> Carrying { get; } = [];

    private readonly Queue<TransportTask> _taskQueue = new();
    private TransportTask? _currentTask;
    private Vector2? _target;

    public bool HasActiveTask() => _currentTask != null || _taskQueue.Count > 0;

    public void AssignTask(ProductionFacility from, ProductionFacility to, List<ResourceAmount> cargo, int? currentTick = null)
    {
        var task = new TransportTask(from, to, cargo);
        //on the `to` side, we should say how many items are on the way
        to.SayWhatsOnTheWay(cargo);
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

    private float _distanceTraveled;
    public float DistanceTraveled => _distanceTraveled;

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

        _distanceTraveled += distance;
        Position = _target.Value;
        _target = null;

        if (!task.HasPickedUp) { PickUp(tick, task); }
        else { Deliver(tick, task); }
    }

    private void PickUp(int tick, TransportTask task)
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

                LogLines.Add(new PickupLog(tick, Id, [new ResourceAmount(item.Resource, amountToTake),], _currentTask?.Source));
            }
            else
            {
                LogLines.Add(new TransportAssignedLog(tick, Id, item.Resource.Id, item.Amount, task.Source, task.Destination));
            }
        }
        task.HasPickedUp = true;
        _target = task.Destination.Position;
    }

    private void Deliver(int tick, TransportTask task)
    {
        // Keep track of which items were NOT delivered in full
        var failed = new List<ResourceAmount>();

        // Track items that were successfully delivered
        var actualDelivered = new List<ResourceAmount>();

        // Loop over all the resources this task is supposed to deliver
        foreach (var taskItem in task.Cargo)
        {
            // Try to find this resource in the transporter’s current inventory
            var carried = Carrying.FirstOrDefault(c => c.Resource == taskItem.Resource);

            // If not found, assume it has 0 available
            var available = carried?.Amount ?? 0;

            // We'll deliver as much as we can, up to the requested amount
            var toDeliver = Math.Min(available, taskItem.Amount);

            // If we can deliver anything at all
            if (toDeliver > 0)
            {
                // Send it to the destination facility
                task.Destination.ReceiveImport(taskItem.Resource, toDeliver, tick, this);

                // Log the successful delivery
                actualDelivered.Add(new ResourceAmount(taskItem.Resource, toDeliver));
                LogLines.Add(new DeliveryLog(tick, Id, task.Destination.Position, [new(taskItem.Resource, toDeliver)]));

                carried!.Amount -= toDeliver; // Subtract the delivered amount from what the transporter is carrying
            }
            if (toDeliver >= taskItem.Amount) { continue; } // If we couldn't deliver the full requested amount, record what was missing

            var shortfall = taskItem.Amount - toDeliver;
            failed.Add(new ResourceAmount(taskItem.Resource, shortfall));
        }

        if (actualDelivered.Count == 0 && failed.Count > 0) // Decide which failure log to write, if any
        {
            LogLines.Add(new DeliveryFailedLog(tick, Id, failed, task.Destination)); // Entire delivery failed — none of the cargo was delivered
        }
        else if (actualDelivered.Count > 0 && failed.Count > 0)
        {
            LogLines.Add(new DeliveryPartialLog(tick, Id, failed, task.Destination)); // Partial delivery — some of the items were delivered, some were not
        }

        Carrying.RemoveAll(item => item.Amount == 0); // Clean up inventory — remove any resource entries with 0 quantity left
        _currentTask = null; // Clear the current task and target so the transporter can move on
        _target = null;
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

    public bool HasPickedUp { get; set; }
}