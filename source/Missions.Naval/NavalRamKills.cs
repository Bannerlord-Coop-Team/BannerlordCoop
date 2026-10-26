using Missions.Agents.Extensions;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] The kill of vanilla's ram tick on this client: only an agent it controls or doesn't network, so every ram death
/// happens once, on its owner, and is reported like any death. Static for the Harmony transpiler.
/// </summary>
internal static class NavalRamKills
{
    /// <summary>Vanilla's ram kill blow, except on another owner's puppet, which that owner kills from its own view of the impact.</summary>
    internal static void DieUnlessPuppet(Agent agent, Blow blow, Agent.KillInfo killInfo)
    {
        if (CoopNavalMissionScope.IsActive && agent.IsPuppet())
        {
#if DEBUG
            NavalContactProbe.CountSparedRamPuppet(agent);
#endif
            return;
        }

        agent.Die(blow, killInfo);
    }
}
