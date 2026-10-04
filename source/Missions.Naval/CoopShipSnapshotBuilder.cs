using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

/// <summary>
/// Builds the campaign <see cref="Ship"/> a coop naval mission spawns instead of the party's own ship.
/// </summary>
public interface ICoopShipSnapshotBuilder
{
    /// <summary>A detached copy of <paramref name="source"/>: same owner, hull, pieces and damage, outside the party's ships.</summary>
    Ship Build(Ship source);
}

/// <summary>
/// Vanilla naval code casts every <c>IShipOrigin</c> to <see cref="Ship"/> (scoreboard owner, figurehead models), so the
/// mission needs a real Ship; a detached copy keeps mission damage off the campaign ship.
/// </summary>
public class CoopShipSnapshotBuilder : ICoopShipSnapshotBuilder
{
    public Ship Build(Ship source)
    {
        var snapshot = new Ship(source.ShipHull);

        // Field, not the Owner setter: the setter would add the copy to the owner's party ships.
        snapshot._owner = source.Owner;
        snapshot._name = source._name;
        foreach (var piece in source._shipPieces)
            snapshot._shipPieces[piece.Key] = piece.Value;
        snapshot.Figurehead = source.Figurehead;
        snapshot.RandomValue = source.RandomValue;
        snapshot.CustomSailPatternId = source.CustomSailPatternId;
        snapshot.IsInvulnerable = source.IsInvulnerable;
        snapshot.HitPoints = source.HitPoints;
        snapshot.SailHitPoints = source.SailHitPoints;

        // S5: Ship.OnShipDamaged on this copy still grants skill XP and runs DestroyShipAction at 0 HP; gate both.
        return snapshot;
    }
}
