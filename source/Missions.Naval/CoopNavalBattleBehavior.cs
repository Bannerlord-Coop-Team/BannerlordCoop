using Missions.Battles;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Drives the coop naval services of one battle mission and tears them down with it.</summary>
public class CoopNavalBattleBehavior : MissionLogic
{
    public CoopNavalBattleBehavior(IBattleShipReplicator shipReplicator, IAgentStationUseReplicator stationUseReplicator,
        INavalNpcFleetSpawner npcFleetSpawner)
    {
        ShipReplicator = shipReplicator;
        StationUseReplicator = stationUseReplicator;
        NpcFleetSpawner = npcFleetSpawner;
        CoopNavalMissionScope.IsActive = true;
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public INavalNpcFleetSpawner NpcFleetSpawner { get; }

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
        StationUseReplicator.Dispose();
        ShipReplicator.Dispose();
        NavalRopes.Clear();
#if DEBUG
        NavalHelmOverride.Clear();
#endif
        NavalForeignHulls.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
