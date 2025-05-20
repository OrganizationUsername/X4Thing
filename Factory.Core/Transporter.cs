using System.Numerics;

namespace Factory.Core;

//ToDo: The next thing is to make Energy cells create from nothing.
//ToDo: Another thing is to make asteroid mining.
//ToDo: Making it so ships can be produced would be great
//ToDo: A proof of concept for creating a ship and having a protectingShip/escort would be awesome.
//ToDo: Draw transports/fighters differently in the WPF.
//ToDo: Animate the fighter shooting a laser at the target if it's attacking.
//ToDo: Set up more factions, including neutral that sells something important. Have us on left, neutral in the middle, and enemies on the right.
//ToDo: Have contracts presented that the user can accept. If they do, sell to get money.
//ToDo: Actually, I don't use money in a meaningful way right now. I guess that makes sense since I'm only doing trade with my own faction. Later on, I want to make sure that I can trade with other factions if it benefits me.
//ToDo: Probably want to rework the recipes/values since they're not really balanced.

/*
 How could I go about making it so a workshop could make two recipes, but we decide the best one available. Maybe we could either wait for solar to happen or we could spend two chemicals together to get one quick, but with a much lower benefit. Help walk me through this route. maybe also if we were desperate for something done quickly, we could do more iron bars at once at much higher expense, and leave those details up to a strategy to be employed by the individual station.
 */


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
            else { LogLines.Add(new TransportAssignedLog(tick, Id, item.Resource.Id, item.Amount, task.Source, task.Destination)); }
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

        switch (actualDelivered.Count) // Decide which failure log to write, if any
        {
            case 0 when failed.Count > 0: LogLines.Add(new DeliveryFailedLog(tick, Id, failed, task.Destination)); break; // Entire delivery failed — none of the cargo was delivered
            case > 0 when failed.Count > 0: LogLines.Add(new DeliveryPartialLog(tick, Id, failed, task.Destination)); break; // Partial delivery — some of the items were delivered, some were not
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