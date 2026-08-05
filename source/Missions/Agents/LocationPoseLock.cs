using GameInterface.Services.Locations;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

/// <summary>
/// Whether a settlement puppet's pose is OWNED by its animation (seated at a chair, using an
/// animation point): the enforce flags native conversation logic keys on, inside a coop location
/// mission. Every continuous-state write site must respect it — a direction or look write on a
/// stationary agent triggers the native turn-in-place, which an enforced loop can never complete,
/// so repeated writes spin the seated NPC in its chair.
/// </summary>
internal static class LocationPoseLock
{
    public static bool IsPosePinned(Agent agent)
    {
        return LocationNpcGate.IsCoopLocationMissionActive
            && agent != null
            && agent.MountAgent == null
            && agent.GetCurrentAnimationFlag(0).HasAnyFlag(
                AnimFlags.anf_enforce_all
                | AnimFlags.anf_enforce_lowerbody
                | AnimFlags.anf_enforce_root_rotation);
    }
}
