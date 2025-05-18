using System.Numerics;

namespace Factory.Core;

public class Ship
{
    public int Id { get; set; }
    public string Name { get; set; } = "Ship";
    public double TotalHull { get; set; } = 100;
    public Vector2 Position { get; set; }
    public List<ILogLine> LogLines { get; } = [];
    public float SpeedPerTick { get; set; } = 1f;
    public List<ResourceAmount> Carrying { get; } = [];
    public TransportTask? CurrentTask;
    public bool TakeDamage(float attackDamage, int currentTick, IHasName hasName)
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
}