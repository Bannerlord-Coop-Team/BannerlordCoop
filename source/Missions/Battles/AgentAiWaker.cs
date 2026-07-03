using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The one copy of the AI wake sequence every "make this agent fight" path needs: alarm it, un-pause its AI,
/// refresh its enemy caches and re-sync its behavior params. An agent that misses part of the sequence can be
/// AI-controlled but NOT alarmed with stale enemy caches, so it ignores its formation's Charge order and
/// stands idle — the family of "NPCs don't move" bugs (host-migration adoption, reinforcements, NPC release,
/// leaderless own troops) all traced back to a drifting partial copy of this sequence.
/// </summary>
public static class AgentAiWaker
{
    /// <summary>
    /// Wake <paramref name="agent"/>'s AI so it engages. When <paramref name="onlyResetCachesIfCanWieldWeapon"/>
    /// is true the enemy-cache reset is skipped for non-combatants (e.g. mounts caught by a formation sweep);
    /// the adopt and reinforcement paths pass false because they only ever see combat troops (the registry
    /// holds riders, never mounts).
    /// </summary>
    public static void Wake(Agent agent, bool onlyResetCachesIfCanWieldWeapon = false)
    {
        agent.SetAlarmState(Agent.AIStateFlag.Alarmed);
        agent.SetIsAIPaused(false);
        if (!onlyResetCachesIfCanWieldWeapon || agent.GetAgentFlags().HasFlag(AgentFlag.CanWieldWeapon))
            agent.ResetEnemyCaches();
        agent.HumanAIComponent?.SyncBehaviorParamsIfNecessary();
    }
}
