namespace _40K.Core;

public interface ICombatLogSink
{
    void Write(CombatEvent e);
}

public sealed class RecordingCombatLog : ICombatLogSink
{
    public List<CombatEvent> Events { get; } = [];
    public void Write(CombatEvent e) => Events.Add(e);

    public string ToLines() => string.Join(Environment.NewLine, Events.Select(ToLine));
    public void Clear() => Events.Clear();
    public static string ToLine(CombatEvent e) =>
        e.Type switch
        {
            CombatEventType.ShotStart => $"-- {e.Attacker} fires {e.WeaponProfile} at {e.Target} (dist {e.Distance}\")",
            CombatEventType.HitRoll => $"Hit: roll {e.Roll} + {e.Modifier} vs {e.Required} ⇒ {(e.Passed == true ? "HIT" : "MISS")}",
            CombatEventType.WoundRoll => $"Wound: roll {e.Roll} vs {e.Required} ⇒ {(e.Passed == true ? "WOUND" : "NO WOUND")}",
            CombatEventType.SaveRoll => e.Roll is null ? $"Save: impossible (req {e.Required}+), no roll" : $"Save: roll {e.Roll} vs {e.Required} ⇒ {(e.Passed == true ? "SAVED" : "FAILED")}",
            CombatEventType.DamageRoll => $"Damage: {e.Damage}",
            CombatEventType.ShotResult => $"Shot result: {(e.Passed == true ? "UNSAVED" : "SAVED")} dmg={e.Damage}",
            CombatEventType.HealthRemaining => $"{e.Target} health remaining: {e.Damage}",
            CombatEventType.BlastShotCount => $"Blast shot count for {e.Distance} models: {e.Required}",
            CombatEventType.DistanceCountShot => $"Distance {e.Distance}\": base shot count {e.Required}",
            _ => e.Type.ToString(),
        };
}

public enum CombatEventType { ShotStart, HitRoll, WoundRoll, SaveRoll, DamageRoll, ShotResult, HealthRemaining, BlastShotCount, DistanceCountShot, }

public readonly record struct CombatEvent(CombatEventType Type, string? Attacker, string? WeaponProfile, string? Target, int? Distance = null, int? Roll = null, int? Required = null, int? Modifier = null, bool? Passed = null, int? Damage = null);