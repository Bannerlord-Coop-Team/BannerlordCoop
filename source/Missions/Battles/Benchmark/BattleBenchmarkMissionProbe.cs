using Common.BattleBenchmark;
using GameInterface.Services.Entity;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles.Benchmark;

/// <summary>Reports the current co-op mission's size and this process's role in it to the battle benchmark.</summary>
public class BattleBenchmarkMissionProbe : IBattleBenchmarkMissionProbe
{
    private readonly IControllerIdProvider controllerIdProvider;

    public BattleBenchmarkMissionProbe(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public BattleBenchmarkMissionState Capture()
    {
        var state = new BattleBenchmarkMissionState { ControllerId = controllerIdProvider.ControllerId };
        Mission mission = Mission.Current;
        CoopMissionController controller = mission?.GetMissionBehavior<CoopMissionController>();
        if (controller == null) return state;

        var battle = controller as CoopBattleController;
        state.InMission = true;
        state.IsBattleHost = battle?.Session.IsLocalHost == true;
        state.Scene = mission.SceneName;
        state.MissionEnded = mission.MissionEnded;
        // An ended mission is tearing its agents down, and Agent.State reads native memory, so don't touch them.
        if (state.MissionEnded) return state;

        // Count from the mission's own agent list; a registry entry can outlive its native agent.
        string ownControllerId = battle?.Session.OwnControllerId ?? state.ControllerId;
        INetworkAgentRegistry registry = controller.MissionComponent.AgentRegistry;
        foreach (Agent agent in mission.Agents.Where(agent => agent.IsActive()))
        {
            state.ActiveAgents++;
            if (registry.TryGetAgentInfo(agent, out CoopAgentInfo info) && info.CurrentAuthority == ownControllerId)
                state.OwnedAgents++;
        }
        return state;
    }
}
