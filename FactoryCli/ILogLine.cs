using System.Numerics;

namespace FactoryCli;

public interface ILogLine
{
    int Tick { get; }
    string Format();
}

public interface ITransporterLog;
public interface IProductionFacilityLog;

public class TransportReceivedLog(int tick, int facilityId, string resourceId, int amount, Vector2 position, Transporter from) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public Transporter From { get; } = from;
    public string Format() => $"[Tick {Tick:D4}] Received {Amount} of {ResourceId} from {From.Name} at {Position}";
}


public class ProductionCompletedLog(int tick, int facilityId, string resourceId, int amount, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick:D4}] Completed job for {ResourceId}, output added to storage at {Position}";
}

public class ProductionStartedLog(int tick, int facilityId, string resourceId, int duration, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Duration { get; } = duration;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick:D4}] Started job for {ResourceId} (duration: {Duration}) at {Position}";
}

public class WorkshopAddedLog(int tick, int facilityId, string resourceId, int amount, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick:D4}] Added {Amount} workshop(s) for {ResourceId} at {Position}";
}


public class TransportAssignedLog(int tick, int transporterId, string resourceId, int amount, ProductionFacility from, ProductionFacility to) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public ProductionFacility From { get; } = from;
    public ProductionFacility To { get; } = to;

    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} assigned to deliver {Amount} x {ResourceId} from {From.Name}({From.Position}) to {To.Name}({To.Position})";
}

public class PickupLog(int tick, int transporterId, List<ResourceAmount> pickedUp, ProductionFacility pf) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> PickedUp { get; } = pickedUp;
    public ProductionFacility Facility { get; } = pf;

    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} picked up: {string.Join(", ", PickedUp)} from {Facility.Name}";
}

public class TransportSplitLog(int tick, int transporterId, List<ResourceAmount> originalCargo, List<ResourceAmount> assignedNow, List<ResourceAmount> remaining) : ILogLine
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> OriginalCargo { get; } = originalCargo;
    public List<ResourceAmount> AssignedNow { get; } = assignedNow;
    public List<ResourceAmount> Remaining { get; } = remaining;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} split delivery: now={string.Join(", ", AssignedNow)}, remaining={string.Join(", ", Remaining)}";
}

public class DeliveryFailedLog(int tick, int transporterId, List<ResourceAmount> failed, ProductionFacility pf) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> Failed { get; } = failed;
    public ProductionFacility Facility { get; } = pf;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} failed to deliver: {string.Join(", ", Failed)} to {Facility.Name}";
}

public class DeliveryLog(int tick, int transporterId, Vector2 destination, List<ResourceAmount> delivered) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public Vector2 Destination { get; } = destination;
    public List<ResourceAmount> Delivered { get; } = delivered;

    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} delivered to {Destination}: {string.Join(", ", Delivered)}";
}