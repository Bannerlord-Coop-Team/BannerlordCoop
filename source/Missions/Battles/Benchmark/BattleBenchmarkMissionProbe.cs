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
        state.ActiveAgents = mission.Agents.Count(agent => agent.IsActive());
        state.OwnedAgents = controller.MissionComponent.AgentRegistry
            .GetAgents(battle?.Session.OwnControllerId ?? state.ControllerId)
            .Count(info => info.Agent != null && info.Agent.IsActive());
        return state;
    }
}
