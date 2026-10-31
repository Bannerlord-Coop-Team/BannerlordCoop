namespace Missions.Messages;

/// <summary>Which vanilla MissionShip damage method a routed hull hit replays on the owner.</summary>
public enum BattleShipDamageKind
{
    /// <summary>DealDamage: hull HP from ship siege missiles.</summary>
    Hull,

    /// <summary>DealCollisionDamage: ramming and hull contact, with the hitter hull.</summary>
    Collision,

    /// <summary>DealDamageToSails: fire arrows and missiles through the sails.</summary>
    Sails,

    /// <summary>DealFireDamage: fire on the hull, which sets it burning at 0 fire HP.</summary>
    Fire,
}
