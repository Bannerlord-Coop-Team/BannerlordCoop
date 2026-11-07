using Common.Messaging;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// [Game thread] Published synchronously by the ship damage patches when this client hit a hull another owner simulates;
/// vanilla was skipped and <c>BattleShipDamageRouter</c> sends the hit to the hull's owner.
/// </summary>
public readonly struct BattleShipHit : IEvent
{
    public readonly MissionObject Hull;
    public readonly BattleShipDamageKind Kind;
    public readonly float Damage;
    /// <summary>The sail damage after the attacker's sail fire model (Sails only).</summary>
    public readonly float InflictedDamage;
    /// <summary>The hit position in the hull's local frame; zero when the hit has none.</summary>
    public readonly Vec3 LocalPoint;
    public readonly Agent Attacker;
    /// <summary>The ramming or colliding hull (Collision only).</summary>
    public readonly MissionObject Hitter;
    public readonly bool IsRamDamage;
    /// <summary>The hit sail's index in the hull's sails, or -1.</summary>
    public readonly int SailIndex;

    public BattleShipHit(MissionObject hull, BattleShipDamageKind kind, float damage, float inflictedDamage, Vec3 localPoint,
        Agent attacker, MissionObject hitter, bool isRamDamage, int sailIndex)
    {
        Hull = hull;
        Kind = kind;
        Damage = damage;
        InflictedDamage = inflictedDamage;
        LocalPoint = localPoint;
        Attacker = attacker;
        Hitter = hitter;
        IsRamDamage = isRamDamage;
        SailIndex = sailIndex;
    }
}
