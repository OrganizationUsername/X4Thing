using System.Numerics;

namespace Factory.Core;

//ToDo: Make it so the logs from the production facilities and transporters are stored in a sortable way so I can sort them by tick and see what happened at a certain time.
//ToDo: Make it so when transporters take or drop off product that they only take as much as they can hold.

public class Transporter : Ship, IUpdatable
{
    public float MaxVolume { get; set; } = 10f;
    private readonly Queue<TransportTask> _taskQueue = new();
    private Vector2? _target;
    public string? GetCurrentDestination => CurrentTask?.Destination.Name;
    public bool HasActiveTask() => CurrentTask != null || _taskQueue.Count > 0;

    public void AssignTask(ProductionFacility from, ProductionFacility to, List<ResourceAmount> cargo, int? currentTick = null)
    {
        var task = new TransportTask(from, to, cargo); //on the `to` side, we should say how many items are on the way
        to.SayWhatsOnTheWay(cargo);
        _taskQueue.Enqueue(task);
        LogLines.Add(new TransportAssignedLog(currentTick ?? 0, Id, cargo.First().Resource.Id, cargo.Sum(x => x.Amount), from, to));
    }

    public void Tick(int tick)
    {
        if (CurrentTask is null)
        {
            if (_taskQueue.Count == 0) { return; }

            CurrentTask = _taskQueue.Dequeue();
            _target = CurrentTask.Source.Position;
        }

        if (_target is null) { return; }

        var task = CurrentTask!;

        if (!MoveTowards(_target.Value)) { return; }

        Position = _target.Value;
        _target = null;

        if (task is not TransportTask t) { return; }
        if (!t.HasPickedUp) { PickUp(tick, t); }
        else { Deliver(tick, t); }
    }

    private void PickUp(int tick, TransportTask task)
    {
        var usedVolume = Carrying.Sum(c => c.Resource.Volume * c.Amount);
        var remainingVolume = MaxVolume - usedVolume;

        foreach (var item in task.Cargo)
        {
            var volumePerUnit = item.Resource.Volume;
            if (volumePerUnit <= 0) { continue; }

            var maxUnits = (int)(remainingVolume / volumePerUnit);
            if (maxUnits == 0) { break; }

            var amountToTake = Math.Min(item.Amount, maxUnits);

            if (amountToTake > 0 && task.Source.TryExport(item.Resource, amountToTake, tick, this))
            {
                if (Carrying.FirstOrDefault(x => x.Resource == item.Resource) is { } existingResourceAmount) { existingResourceAmount.Amount += amountToTake; }
                else { Carrying.Add(new ResourceAmount(item.Resource, amountToTake)); }
                remainingVolume -= amountToTake * volumePerUnit;

                LogLines.Add(new PickupLog(tick, Id, [new ResourceAmount(item.Resource, amountToTake),], CurrentTask?.Source));
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
        var failed = new List<ResourceAmount>(); // Keep track of which items were NOT delivered in full
        var actualDelivered = new List<ResourceAmount>(); // Track items that were successfully delivered
        foreach (var taskItem in task.Cargo) // Loop over all the resources this task is supposed to deliver
        {

            var carried = Carrying.FirstOrDefault(c => c.Resource == taskItem.Resource); // Try to find this resource in the transporter’s current inventory
            var available = carried?.Amount ?? 0; // If not found, assume it has 0 available
            var toDeliver = Math.Min(available, taskItem.Amount); // We'll deliver as much as we can, up to the requested amount
            if (toDeliver > 0) // If we can deliver anything at all
            {
                task.Destination.ReceiveImport(taskItem.Resource, toDeliver, tick, this); // Send it to the destination facility
                actualDelivered.Add(new ResourceAmount(taskItem.Resource, toDeliver)); // Log the successful delivery
                LogLines.Add(new DeliveryLog(tick, Id, task.Destination.Position, [new(taskItem.Resource, toDeliver),]));
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
        CurrentTask = null; // Clear the current task and target so the transporter can move on
        _target = null;
    }
}

public class ResourceAmount(Resource resource, int amount)
{
    public Resource Resource { get; set; } = resource;
    public int Amount { get; set; } = amount;
}