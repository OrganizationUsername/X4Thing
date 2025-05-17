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
    public string Format() => $"[Tick {Tick}] Received {Amount} of {ResourceId} from {From.Name} at {Position}";
}


public class ProductionCompletedLog(int tick, int facilityId, string resourceId, int amount, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick}] Completed job for {ResourceId}, output added to storage at {Position}";
}

public class ProductionStartedLog(int tick, int facilityId, string resourceId, int duration, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Duration { get; } = duration;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick}] Started job for {ResourceId} (duration: {Duration}) at {Position}";
}

public class WorkshopAddedLog(int tick, int facilityId, string resourceId, int amount, Vector2 position) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public int FacilityId { get; } = facilityId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick}] Added {Amount} workshop(s) for {ResourceId} at {Position}";
}


public class TransportAssignedLog(int tick, int transporterId, string resourceId, int amount, ProductionFacility from, ProductionFacility to) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public ProductionFacility From { get; } = from;
    public ProductionFacility To { get; } = to;

    public string Format() => $"[Tick {Tick}] Transporter {TransporterId} assigned to deliver {Amount} x {ResourceId} from {From.Name}({From.Position}) to {To.Name}({To.Position})";
}

public class PickupLog(int tick, int transporterId, List<ResourceAmount> pickedUp, ProductionFacility pf) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> PickedUp { get; } = pickedUp;
    public ProductionFacility Facility { get; } = pf;

    public string Format() => $"[Tick {Tick}] Transporter {TransporterId} picked up: {string.Join(", ", PickedUp)} from {Facility.Name}";
}

public class DeliveryLog(int tick, int transporterId, Vector2 destination, List<ResourceAmount> delivered) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public Vector2 Destination { get; } = destination;
    public List<ResourceAmount> Delivered { get; } = delivered;

    public string Format() => $"[Tick {Tick}] Transporter {TransporterId} delivered to {Destination}: {string.Join(", ", Delivered)}";
}