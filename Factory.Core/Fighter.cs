using System.Numerics;

namespace Factory.Core;

public class Fighter : Ship, IUpdatable, IHasName
{
    public float AttackRange { get; set; } = 10f;
    public float AttackDamage { get; set; } = 10f;
    public float MinimumValue { get; set; } = 20f;
    public Ship? Target { get; private set; }
    private float _randomAngle = Random.Shared.Next(0, 360);

    public void Tick(int tick)
    {
        if (!IsValidTarget(Target))
        {
            if (Target is not null) { LogLines.Add(new FighterTargetLostLog(tick, Id, Target.Id, Target.Position)); }
            Target = null;
        }

        if (Target is null) { MoveRandomly(); return; }

        var distance = Vector2.Distance(Position, Target.Position);
        if (distance <= AttackRange) { Attack(Target, tick); }
        else { MoveTowards(Target.Position); }
    }

    private void MoveRandomly()
    {
        _randomAngle += (float)(Random.Shared.Next(0, 5) / 180f * Math.PI);
        var direction = new Vector2((float)Math.Cos(_randomAngle), (float)Math.Sin(_randomAngle));
        MoveTowards(Position + direction * SpeedPerTick);
    }

    private bool IsValidTarget(Ship? t) => t is not null && GetTransportValue(t) >= MinimumValue && t.TotalHull > 0;
    public void SetTarget(Transporter target, int currentTick) { Target = target; LogLines.Add(new FighterTargetAssignedLog(currentTick, Id, target.Id, target.Position)); }
    public static float GetTransportValue(Ship t) => t.Carrying.Sum(r => r.Amount * r.Resource.BaseValue);

    private void Attack(Ship transporter, int tick)
    {
        var theyDied = transporter.TakeDamage(AttackDamage, tick, this);
        LogLines.Add(new EntityAttackedLog(tick, Id, AttackDamage, transporter.Position, Name)); if (theyDied) { LogLines.Add(new TransporterDestroyedLog(tick, transporter.Id, transporter.Position)); }
    }
}