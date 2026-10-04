using Missions.Battles;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Drives the coop naval services of one battle mission and tears them down with it.</summary>
public class CoopNavalBattleBehavior : MissionLogic
{
    public CoopNavalBattleBehavior(IBattleShipReplicator shipReplicator)
    {
        ShipReplicator = shipReplicator;
    }

    public IBattleShipReplicator ShipReplicator { get; }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        ShipReplicator.Tick(dt);
    }

    public override void OnEndMissionInternal()
    {
        ShipReplicator.Dispose();
        NavalForeignHulls.Clear();
        NavalPlayerDeploymentSlot.Reset();
        base.OnEndMissionInternal();
    }
}
