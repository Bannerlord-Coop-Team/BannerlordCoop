#if DEBUG
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Reads morale by hull on the client that owns each crew.</summary>
public interface INavalRamInspect
{
    /// <summary>Reports non-fleeing, active human crew; foreign morale stays null because it is not replicated.</summary>
    object Inspect(Mission mission, CoopBattleController controller);
}

/// <inheritdoc cref="INavalRamInspect"/>
public sealed class NavalRamInspect : INavalRamInspect
{
    /// <inheritdoc/>
    public object Inspect(Mission mission, CoopBattleController controller) => new
    {
        crewMorale = controller.MissionComponent.ShipRegistry.Ships.Select(ship =>
        {
            var hull = ship.Hull as MissionShip;
            var crew = mission.Agents.Where(agent => agent.IsHuman && agent.IsActive() && !agent.IsRunningAway
                && hull?.Formation != null && agent.Formation == hull.Formation).ToArray();
            var owned = crew.Where(agent => controller.MissionComponent.AgentRegistry.TryGetAgentInfo(agent, out var info)
                && info.CurrentAuthority == controller.Session.OwnControllerId && agent.CommonAIComponent != null)
                .Select(agent => agent.GetMorale()).ToArray();
            return new
            {
                shipId = ship.ShipId,
                authority = ship.CurrentAuthority,
                nonFleeingCrew = crew.Length,
                sampledCrew = owned.Length,
                mean = owned.Length == 0 ? (float?)null : owned.Average(),
                min = owned.Length == 0 ? (float?)null : owned.Min(),
            };
        }).ToArray(),
    };
}
#endif
