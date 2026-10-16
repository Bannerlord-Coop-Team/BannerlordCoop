#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.Objects;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] DEBUG: keeps every copy in the mode its copy_body rule asks for and steers the steered ones once per
/// physics step. Copies the engine did not spawn itself (a hull handed to another owner) are picked up from the registry.
/// </summary>
public class NavalCopyBodyLogic : MissionLogic
{
    // Without a fixed step for this long the correction runs once per frame instead.
    private const float FixedTickTimeoutSeconds = 0.5f;

    private readonly INetworkShipRegistry registry;
    private float sinceFixedTick = float.MaxValue;

    public NavalCopyBodyLogic(INetworkShipRegistry registry)
    {
        this.registry = registry;
    }

    public long FixedTicks { get; private set; }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        TrackRegisteredCopies();
        NavalCopyBodies.Table.ReconcileAll();

        sinceFixedTick += dt;
        if (sinceFixedTick > FixedTickTimeoutSeconds) NavalCopyBodies.Steer(dt);
    }

    public override void OnFixedMissionTick(float fixedDt)
    {
        base.OnFixedMissionTick(fixedDt);
        FixedTicks++;
        sinceFixedTick = 0f;
        NavalCopyBodies.Steer(fixedDt);
    }

    private void TrackRegisteredCopies()
    {
        foreach (var ship in registry.Ships)
        {
            if (!(ship.Hull is MissionShip hull) || !NavalForeignHulls.Contains(hull) || NavalCopyBodies.Table.IsTracked(hull)) continue;

            NavalCopyBodies.Track(hull, ship.ShipId);
        }
    }
}
#endif
