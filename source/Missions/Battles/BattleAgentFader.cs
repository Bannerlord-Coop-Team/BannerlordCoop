using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Fades an agent out of a battle without a casualty. Vanilla Agent.OnRemove skips the station release once the mission has
/// ended, leaving a hull machine ticking a removed user, so the station is released first.
/// </summary>
public interface IBattleAgentFader
{
    /// <summary>
    /// [Game thread] Release the agent's station, then fade it out; an inactive agent is left alone. A caller that deregisters
    /// a puppet first keeps the naval release patch from reporting the release as a desync.
    /// </summary>
    void FadeOut(Agent agent, bool hideInstantly, bool hideMount);
}

/// <inheritdoc cref="IBattleAgentFader"/>
public class BattleAgentFader : IBattleAgentFader
{
    // Takes the agent's station lock like vanilla Agent.OnRemove, since the game-thread queue drains while agent ticks run.
    public void FadeOut(Agent agent, bool hideInstantly, bool hideMount)
    {
        if (agent == null || !agent.IsActive()) return;

        if (agent.CurrentlyUsedGameObject != null)
            agent.StopUsingGameObjectMT(isSuccessful: false, Agent.StopUsingGameObjectFlags.None);
        agent.FadeOut(hideInstantly, hideMount);
    }
}
