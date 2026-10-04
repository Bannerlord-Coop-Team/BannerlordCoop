using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipEngine"/>
public class NavalShipEngine : INavalShipEngine
{
    private readonly ICoopShipSnapshotBuilder snapshotBuilder;
    private readonly IObjectManager objectManager;

    public NavalShipEngine(ICoopShipSnapshotBuilder snapshotBuilder, IObjectManager objectManager)
    {
        this.snapshotBuilder = snapshotBuilder;
        this.objectManager = objectManager;
    }

    private static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    public IReadOnlyList<MissionObject> Hulls =>
        ShipsLogic?.AllShips.Cast<MissionObject>().ToList() ?? new List<MissionObject>();

    public BattleShipSpawnData Describe(MissionObject hull, NetworkShipInfo identity)
    {
        var ship = (MissionShip)hull;
        var campaignShip = ship.ShipOrigin as Ship;
        var pieces = campaignShip?._shipPieces.Where(piece => piece.Value != null).ToArray()
            ?? Array.Empty<KeyValuePair<string, ShipUpgradePiece>>();

        return new BattleShipSpawnData(
            identity.ShipId,
            identity.CurrentAuthority,
            identity.MapEventPartyId,
            identity.IsNpcParty,
            ship.Team.Side,
            (int)ship.Formation.FormationIndex,
            NetworkBattleShipSample.FromFrame(ship.GlobalFrame),
            ship.ShipOrigin.Hull.StringId,
            pieces.Select(piece => piece.Key).ToArray(),
            pieces.Select(piece => piece.Value.StringId).ToArray(),
            campaignShip?.Figurehead?.StringId,
            campaignShip?._name?.ToString(),
            ship.ShipOrigin.HitPoints,
            ship.ShipOrigin.SailHitPoints,
            ship.ShipOrigin.RandomValue,
            ship.ShipOrigin.CustomSailPatternId);
    }

    public MissionObject SpawnForeignHull(BattleShipSpawnData data, Team team, out Formation formation)
    {
        formation = null;
        var shipsLogic = ShipsLogic;
        if (shipsLogic == null || !NetworkBattleShipSample.IsValidFrame(data.Frame)) return null;

        // NavalShipsLogic keys hulls by team side and formation class, so a team holds at most one hull per formation.
        formation = shipsLogic.FindFirstFormationWithoutShip(team, (FormationClass)8);
        if (formation == null) return null;

        objectManager.TryGetObject(data.MapEventPartyId, out MapEventParty mapEventParty);
        var origin = snapshotBuilder.Build(data, mapEventParty?.Party);
        if (origin == null) return null;

        var frame = NetworkBattleShipSample.ToFrame(data.Frame);
        var ship = shipsLogic.SpawnShip(origin, in frame, team, formation, spawnAnchored: false, checkForFreeArea: false);

        // The owner simulates this hull: no local controller, no auto controller, a kinematic body.
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
        ship.GameEntity.DisableDynamicBodySimulation();
        NavalForeignHulls.Add(ship);
        formation = ship.Formation;
        return ship;
    }

    public MatrixFrame GetFrame(MissionObject hull) => ((MissionShip)hull).GlobalFrame;

    public void ApplyForeignFrame(MissionObject hull, MatrixFrame frame)
    {
        var entity = ((MissionShip)hull).GameEntity;
        if (!entity.IsValid) return;

        entity.SetGlobalFrame(frame, isTeleportation: false);
        entity.UpdateAttachedNavigationMeshFaces();
    }

    public bool IsBodyActive(MissionObject hull)
    {
        var entity = ((MissionShip)hull).GameEntity;
        return entity.IsValid && entity.HasDynamicRigidBodyAndActiveSimulation();
    }

    public string ControllerName(MissionObject hull) =>
        ((MissionShip)hull).Controller?.ControllerType.ToString() ?? ShipControllerType.None.ToString();
}
