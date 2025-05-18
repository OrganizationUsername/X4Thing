using System.Numerics;

namespace Factory.Core;



public class Ship : Entity
{

    public double TotalHull { get; set; } = 100;
    public float SpeedPerTick { get; set; } = 1f;
    public List<ResourceAmount> Carrying { get; } = [];
    public IShipTask? CurrentTask;
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