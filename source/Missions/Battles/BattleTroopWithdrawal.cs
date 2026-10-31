using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] Takes an agent off a battle's books as a withdrawal, not a casualty: its health is recorded, it leaves the
/// agent registry and its casualty attribution is forgotten. The caller fades it out afterwards.
/// </summary>
public interface IBattleTroopWithdrawal
{
    /// <summary>
    /// Withdraws a registered agent while it is still active. False when it is not registered, as after an earlier
    /// withdrawal, so a later one records and counts nothing again.
    /// </summary>
    bool Deregister(Agent agent);
}

/// <inheritdoc cref="IBattleTroopWithdrawal"/>
public class BattleTroopWithdrawal : IBattleTroopWithdrawal
{
    private readonly ICoopMissionComponent missionComponent;
    private readonly IBattleInstanceLifecycle lifecycle;
    private readonly ICasualtyAttributionMap casualties;

    public BattleTroopWithdrawal(ICoopMissionComponent missionComponent, IBattleInstanceLifecycle lifecycle,
        ICasualtyAttributionMap casualties)
    {
        this.missionComponent = missionComponent;
        this.lifecycle = lifecycle;
        this.casualties = casualties;
    }

    public bool Deregister(Agent agent)
    {
        var registry = missionComponent.AgentRegistry;
        if (agent == null || !registry.TryGetAgentInfo(agent, out var info)) return false;

        lifecycle.RecordWithdrawnHealth(agent);
        registry.RemoveAgent(info.AgentId);
        casualties.Forget(info.AgentId);
        return true;
    }
}
