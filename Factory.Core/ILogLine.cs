﻿using System.Numerics;

namespace Factory.Core;

public interface ILogLine
{
    int Tick { get; }
    string Format();
}

public interface IFighterLog;
public interface ITransporterLog;
public interface IProductionFacilityLog;

public class TransportReceivedLog(int tick, string resourceId, int amount, Vector2 position, Transporter from) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public Transporter From { get; } = from;
    public string Format() => $"[Tick {Tick:D4}] Received {Amount} of {ResourceId} from {From.Name} at {Position}";
}

public class TransporterDestroyedLog(int tick, int transporterId, Vector2 position) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public Vector2 Position { get; } = position;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} destroyed at {Position}";
}

public class ShipLostCargoLog(int tick, int transporterId, string resourceId, int amount) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} lost cargo: {Amount} of {ResourceId}";
}

public class TransporterDamagedLog(int tick, int transporterId, float damage, Vector2 position, string? name) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public float Damage { get; } = damage;
    public Vector2 Position { get; } = position;
    public string? Name { get; } = name;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} damaged ({Damage}) at {Position} by {Name ?? "Unknown"}";
}

public class FighterTargetAssignedLog(int tick, int fighterId, int targetId, Vector2 targetPosition) : ILogLine, IFighterLog
{
    public int Tick { get; } = tick;
    public int FighterId { get; } = fighterId;
    public int TargetId { get; } = targetId;
    public Vector2 TargetPosition { get; } = targetPosition;
    public string Format() => $"[Tick {Tick:D4}] Fighter {FighterId} assigned to target {TargetId} at {TargetPosition}";
}

public class TransportSentLog(int tick, string resourceId, int amount, Vector2 position, Transporter to) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public Vector2 Position { get; } = position;
    public Transporter To { get; } = to;
    public string Format() => $"[Tick {Tick:D4}] Sent {Amount} of {ResourceId} to {To.Name} at {Position}";
}

public class TransportFailedLog(int tick, ProductionFacility facility, string resourceId, int amount) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public string Format() => $"[Tick {Tick:D4}] Failed to send {Amount} of {ResourceId} at {facility.Position} for {facility.Name}";
}

public class FighterTargetLostLog(int tick, int fighterId, int targetId, Vector2 targetPosition) : ILogLine, IFighterLog
{
    public int Tick { get; } = tick;
    public int FighterId { get; } = fighterId;
    public int TargetId { get; } = targetId;
    public Vector2 TargetPosition { get; } = targetPosition;
    public string Format() => $"[Tick {Tick:D4}] Fighter {FighterId} lost target {TargetId} at {TargetPosition}";
}

public class EntityAttackedLog(int tick, int transporterId, float damage, Vector2 position, string? name) : ILogLine, IFighterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public float Damage { get; } = damage;
    public Vector2 Position { get; } = position;
    public string? Name { get; } = name;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} hit ({Damage}) at {Position} by {Name ?? "Unknown"}";
}

public class ProductionCompletedLog(int tick, ProductionFacility facility, string resourceId, int amount) : ILogLine, IProductionFacilityLog //ToDo: We should replace this with a bulk production log that contains all the resources produced in a tick
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public ProductionFacility Facility { get; } = facility;
    public int Amount { get; } = amount;
    public string Format() => $"[Tick {Tick:D4}] Completed job for {ResourceId}, output added to storage at {Facility.Position} at station {Facility.Name}";
}

public class ProductionStartedLog(int tick, ProductionFacility facility, string resourceId, int duration, Vector2 position, Recipe recipe) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public int Duration { get; } = duration;
    public Vector2 Position { get; } = position;
    public Recipe Recipe { get; } = recipe;
    public string Format() => $"[Tick {Tick:D4}] Started job for {ResourceId} (duration: {Duration}) at {facility.Position} with Recipe = {recipe.Id}";
}

public class WorkshopAddedLog(int tick, ProductionFacility facility, string resourceId, int amount) : ILogLine, IProductionFacilityLog
{
    public int Tick { get; } = tick;
    public string ResourceId { get; } = resourceId;
    public int Amount { get; } = amount;
    public string Format() => $"[Tick {Tick:D4}] Added {Amount} workshop(s) for {ResourceId} at {facility.Position}";
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

public class PickupLog(int tick, int transporterId, List<ResourceAmount> pickedUp, ProductionFacility? pf) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> PickedUp { get; } = pickedUp;
    public ProductionFacility? Facility { get; } = pf;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} picked up: {string.Join(", ", PickedUp)} from {Facility?.Name ?? "Unknown"}";
}

public class DeliveryPartialLog(int tick, int transporterId, List<ResourceAmount> partial, ProductionFacility pf) : ILogLine, ITransporterLog
{
    public int Tick { get; } = tick;
    public int TransporterId { get; } = transporterId;
    public List<ResourceAmount> Partial { get; } = partial;
    public ProductionFacility Facility { get; } = pf;
    public string Format() => $"[Tick {Tick:D4}] Transporter {TransporterId} partially delivered: {string.Join(", ", Partial)} to {Facility.Name}";
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