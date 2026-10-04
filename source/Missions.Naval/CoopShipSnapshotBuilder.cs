using Missions.Messages;
using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Missions.Naval;

/// <summary>
/// Builds the campaign <see cref="Ship"/> a coop naval mission spawns instead of the party's own ship.
/// </summary>
public interface ICoopShipSnapshotBuilder
{
    /// <summary>A detached copy of <paramref name="source"/>: same owner, hull, pieces and damage, outside the party's ships.</summary>
    Ship Build(Ship source);

    /// <summary>A detached ship rebuilt from another owner's hull record; null when its hull id is unknown here.</summary>
    Ship Build(BattleShipSpawnData data, PartyBase owner);
}

/// <summary>
/// Vanilla naval code casts every <c>IShipOrigin</c> to <see cref="Ship"/> (scoreboard owner, figurehead models), so the
/// mission needs a real Ship; a detached copy keeps mission damage off the campaign ship.
/// </summary>
public class CoopShipSnapshotBuilder : ICoopShipSnapshotBuilder
{
    public Ship Build(Ship source)
    {
        var snapshot = Create(source.ShipHull, source.Owner);
        snapshot._name = source._name;
        foreach (var piece in source._shipPieces)
            snapshot._shipPieces[piece.Key] = piece.Value;
        snapshot.Figurehead = source.Figurehead;
        snapshot.RandomValue = source.RandomValue;
        snapshot.CustomSailPatternId = source.CustomSailPatternId;
        snapshot.IsInvulnerable = source.IsInvulnerable;
        snapshot.HitPoints = source.HitPoints;
        snapshot.SailHitPoints = source.SailHitPoints;
        return snapshot;
    }

    public Ship Build(BattleShipSpawnData data, PartyBase owner)
    {
        var hull = MBObjectManager.Instance.GetObject<ShipHull>(data.HullId);
        if (hull == null) return null;

        var snapshot = Create(hull, owner);
        if (!string.IsNullOrEmpty(data.Name)) snapshot._name = new TextObject(data.Name);
        var slots = data.PieceSlots ?? Array.Empty<string>();
        var pieces = data.PieceIds ?? Array.Empty<string>();
        for (int i = 0; i < slots.Length && i < pieces.Length; i++)
        {
            if (snapshot._shipPieces.ContainsKey(slots[i]))
                snapshot._shipPieces[slots[i]] = MBObjectManager.Instance.GetObject<ShipUpgradePiece>(pieces[i]);
        }
        if (!string.IsNullOrEmpty(data.FigureheadId))
            snapshot.Figurehead = MBObjectManager.Instance.GetObject<Figurehead>(data.FigureheadId);
        snapshot.RandomValue = data.RandomValue;
        snapshot.CustomSailPatternId = data.CustomSailPatternId ?? "";
        snapshot.HitPoints = data.HitPoints;
        snapshot.SailHitPoints = data.SailHitPoints;
        return snapshot;
    }

    private static Ship Create(ShipHull hull, PartyBase owner)
    {
        var snapshot = new Ship(hull);

        // Field, not the Owner setter: the setter would add the copy to the owner's party ships.
        snapshot._owner = owner;

        // S5: Ship.OnShipDamaged on this copy still grants skill XP and runs DestroyShipAction at 0 HP; gate both.
        return snapshot;
    }
}
