using Missions.Battles;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Drives the coop naval services of one battle mission and tears them down with it.</summary>
public class CoopNavalBattleBehavior : MissionLogic
{
    public CoopNavalBattleBehavior(IBattleShipReplicator shipReplicator, IAgentStationUseReplicator stationUseReplicator)
    {
        ShipReplicator = shipReplicator;
        StationUseReplicator = stationUseReplicator;
        CoopNavalMissionScope.IsActive = true;
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public IAgentStationUseReplicator StationUseReplicator { get; }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        ShipReplicator.Tick(dt);
        StationUseReplicator.Tick(dt);
    }

    public override void OnEndMissionInternal()
    {
        CoopNavalMissionScope.IsActive = false;
        StationUseReplicator.Dispose();
        ShipReplicator.Dispose();
        NavalRopes.Clear();
        NavalForeignHulls.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
