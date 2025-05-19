using System.Numerics;

namespace Factory.Core;

public class Entity
{
    public List<ILogLine> LogLines { get; } = [];
    public int Id { get; set; }
    public required string Name { get; set; }// = "Unknown";
    public Vector2 Position { get; set; }
    public int PlayerId { get; set; } = 0;
}

public class Ship : Entity
{
    public double TotalHull { get; set; } = 100;
    public float SpeedPerTick { get; set; } = 1f;
    public List<ResourceAmount> Carrying { get; } = [];
    public IShipTask? CurrentTask;
    public float DistanceTraveled { get; protected set; }
    public bool TakeDamage(float attackDamage, int currentTick, Entity hasName)
    {
        TotalHull -= attackDamage;
        LogLines.Add(new TransporterDamagedLog(currentTick, Id, attackDamage, Position, hasName.Name));
        if (!(TotalHull <= 0)) { return false; }

        LogLines.Add(new TransporterDestroyedLog(currentTick, Id, Position));
        CurrentTask = null;
        foreach (var item in Carrying) { LogLines.Add(new ShipLostCargoLog(currentTick, Id, item.Resource.Id, item.Amount)); }
        Carrying.Clear();
        return true;
    }

    protected bool MoveTowards(Vector2 targetPosition)
    {
        var toTarget = targetPosition - Position;
        var distance = toTarget.Length();

        if (distance == 0) { return true; }
        DistanceTraveled += SpeedPerTick;
        var direction = Vector2.Normalize(toTarget);
        Position += direction * MathF.Min(SpeedPerTick, distance);

        return distance <= SpeedPerTick;
    }
}

public interface IShipTask
{
    public ProductionFacility Source { get; }
    public ProductionFacility Destination { get; }
}

public class TransportTask(ProductionFacility source, ProductionFacility dest, List<ResourceAmount> cargo) : IShipTask
{
    public ProductionFacility Source { get; } = source;
    public ProductionFacility Destination { get; } = dest;
    public List<ResourceAmount> Cargo { get; } = cargo;

    public bool HasPickedUp { get; set; }
}