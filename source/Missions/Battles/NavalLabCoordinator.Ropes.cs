#if DEBUG
using System;
using Common;

namespace Missions.Battles;

public interface INavalRopeCoordinator
{
    object ExecuteRope(Guid operationId, string kind, int sourceShip, int sourceStation, int targetStation);
    object RopeStatus();
}

public sealed partial class NavalLabCoordinator : INavalRopeCoordinator
{
    public object ExecuteRope(Guid operationId, string kind, int sourceShip, int sourceStation, int targetStation)
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Rope actions require the game thread.");
        if (kind != "throw" && kind != "miss" && kind != "cut") throw new ArgumentException("Use throw, miss or cut.");
        return ExecuteCore(operationId, "rope-" + kind, sourceShip, sourceStation, false, targetStation);
    }

    public object RopeStatus()
    {
        if (!GameThread.Instance.IsGameThread) throw new InvalidOperationException("Rope status requires the game thread.");
        return (adapter as INavalRopeAdapter)?.InspectRopes() ?? new { unavailable = "no_native_rope_adapter" };
    }
}
#endif
