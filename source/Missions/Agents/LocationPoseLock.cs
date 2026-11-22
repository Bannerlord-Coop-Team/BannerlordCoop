using GameInterface.Services.Locations;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>
/// Whether a settlement puppet is OWNED by a scene point it is using (chair, animation point,
/// usable machine) inside a coop location mission. Point use is replicated semantically (the
/// receiver's puppet uses the SAME local point), so the point drives alignment, animation and
/// facing natively on every client — and every continuous-state write path must stand down:
/// driving movement or directions onto a point-owned agent re-fights the point (the old
/// floating/spinning/sliding sitter bugs, each a compensation for replicating the point's outputs
/// instead of the use itself).
/// </summary>
internal static class LocationPoseLock
{
    /// <summary>True if the agent is using a scene point inside an active coop location mission.</summary>
    public static bool IsPointOwned(ILocationNpcAuthorityState authorityState, Agent agent)
    {
        return authorityState.IsCoopLocationMissionActive
            && agent != null
            && agent.CurrentlyUsedGameObject != null;
    }
}
