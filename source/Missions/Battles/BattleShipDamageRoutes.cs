namespace Missions.Battles;

/// <summary>
/// What a vanilla hull damage method does with a hit, by who simulates the hull and where the hit came from. Static and in
/// <c>Missions</c> because the naval Harmony gate reads it and CI runs only these tests.
/// </summary>
internal static class BattleShipDamageRoutes
{
    /// <summary>Runs vanilla on an own hull and in a routed apply, sends a local hit on a copy, and drops the rest.</summary>
    internal static BattleShipDamageRoute Route(bool isOwnHull, bool isRoutedApply, BattleShipHitOrigin origin)
    {
        if (isRoutedApply) return BattleShipDamageRoute.Vanilla;
        if (origin == BattleShipHitOrigin.ReplayedHit) return BattleShipDamageRoute.Drop;
        if (isOwnHull) return BattleShipDamageRoute.Vanilla;
        return origin == BattleShipHitOrigin.LocalHit ? BattleShipDamageRoute.Send : BattleShipDamageRoute.Drop;
    }

    /// <summary>A puppet's missile or swing is a replay its owner decides; a replayed ship weapon shot always has a puppet shooter.</summary>
    internal static BattleShipHitOrigin AttackOrigin(bool hasAttacker, bool attackerIsPuppet) =>
        hasAttacker && attackerIsPuppet ? BattleShipHitOrigin.ReplayedHit : BattleShipHitOrigin.LocalHit;

    /// <summary>A collision belongs to the rammer's owner: queued contacts exist only for own rammers, a ram tip counts on its own hull.</summary>
    internal static BattleShipHitOrigin CollisionOrigin(bool isQueuedContact, bool hitterIsOwn) =>
        isQueuedContact || hitterIsOwn ? BattleShipHitOrigin.LocalHit : BattleShipHitOrigin.ReplayedHit;
}

/// <summary>Where a hull hit came from, as the ship damage patches see it.</summary>
public enum BattleShipHitOrigin
{
    /// <summary>The hull's own tick (capsizing, burning): only its owner's counts.</summary>
    SelfTick,

    /// <summary>A hit this client decided: its own agent's attack, its own rammer, or a debug command.</summary>
    LocalHit,

    /// <summary>A puppet's replayed attack or another owner's rammer: that owner decides it.</summary>
    ReplayedHit,
}

/// <summary>What a hull damage method does with a hit.</summary>
public enum BattleShipDamageRoute
{
    /// <summary>Run vanilla: an own hull, or a routed hit or condition being applied.</summary>
    Vanilla,

    /// <summary>Skip vanilla and send the hit to the hull's authority.</summary>
    Send,

    /// <summary>Skip vanilla; another client decides this hit.</summary>
    Drop,
}
