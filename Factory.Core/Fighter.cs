using System.Numerics;

namespace Factory.Core;

public class Fighter : Ship, IUpdatable, IHasName
{
    public float AttackRange { get; set; } = 10f;
    public float AttackDamage { get; set; } = 10f;
    public float MinimumValue { get; set; } = 20f;
    public int PlayerId { get; set; } = 0;
    public Ship? Target { get; private set; }

    public void Tick(int tick)
    {
        if (!IsValidTarget(Target))
        {
            if (Target is not null)
            {
                LogLines.Add(new FighterTargetLostLog(tick, Id, Target.Id, Target.Position));
            }
            Target = null;
        }

        // Acquire target if none
        if (Target == null || !IsValidTarget(Target))
        {
            //Target = FindTarget(); //it should be assigned a target by gameData
        }

        //Each fighter will also have a transporter following it. Its job is to pick up what's dropped and take it to the other faction, maybe.

        if (Target == null) { return; }

        // Move toward target
        var toTarget = Target.Position - Position;
        var distance = toTarget.Length();

        if (distance <= AttackRange)
        {
            Attack(Target, tick);
        }
        else
        {
            var direction = Vector2.Normalize(toTarget);
            Position += direction * MathF.Min(SpeedPerTick, distance);
        }
    }

    private bool IsValidTarget(Ship? t) => t is not null && GetTransportValue(t) >= MinimumValue && t.TotalHull > 0;


    public void SetTarget(Transporter target, int currentTick)
    {
        Target = target;
        LogLines.Add(new FighterTargetAssignedLog(currentTick, Id, target.Id, target.Position));
    }

    public float GetTransportValue(Ship t) => t.Carrying.Sum(r => r.Amount * r.Resource.BaseValue);

    private void Attack(Ship transporter, int tick)
    {
        var theyDied = transporter.TakeDamage(AttackDamage, tick, this);
        LogLines.Add(new EntityAttackedLog(tick, Id, AttackDamage, transporter.Position, Name));
        if (theyDied)
        {
            //TransporterDestroyedLog
            LogLines.Add(new TransporterDestroyedLog(tick, transporter.Id, transporter.Position));
        }
    }
}