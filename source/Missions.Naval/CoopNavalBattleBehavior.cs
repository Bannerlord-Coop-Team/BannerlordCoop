using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Drives the coop naval services of one battle mission and tears them down with it.</summary>
public class CoopNavalBattleBehavior : MissionLogic
{
    private readonly NavalShipsLogic shipsLogic;

    public CoopNavalBattleBehavior(IBattleShipReplicator shipReplicator, IAgentStationUseReplicator stationUseReplicator,
        INavalNpcFleetSpawner npcFleetSpawner, IBattleShipDamageRouter shipDamageRouter, NavalShipsLogic shipsLogic)
    {
        ShipReplicator = shipReplicator;
        StationUseReplicator = stationUseReplicator;
        NpcFleetSpawner = npcFleetSpawner;
        ShipDamageRouter = shipDamageRouter;
        this.shipsLogic = shipsLogic;
        // Fires in NavalShipsLogic.OnEndMission before it removes every hull, so the ropes are still attached.
        shipsLogic.MissionEndEvent += ShipReplicator.SendFinalRopes;
        CoopNavalMissionScope.IsActive = true;
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public INavalNpcFleetSpawner NpcFleetSpawner { get; }

    public IBattleShipDamageRouter ShipDamageRouter { get; }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        ShipReplicator.Tick(dt);
        StationUseReplicator.Tick(dt);
        NpcFleetSpawner.Tick();
    }

    public override void OnEndMissionInternal()
    {
        CoopNavalMissionScope.IsActive = false;
        shipsLogic.MissionEndEvent -= ShipReplicator.SendFinalRopes;
        ShipDamageRouter.Dispose();
        StationUseReplicator.Dispose();
        ShipReplicator.Dispose();
        NavalRopes.Clear();
#if DEBUG
        NavalHelmOverride.Clear();
#endif
        NavalForeignHulls.Clear();
        NavalShipDamageGate.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
