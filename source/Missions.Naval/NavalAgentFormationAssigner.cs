using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] In a coop naval battle an agent joins only a formation with a hull: NavalDLC's team AI builds a formation's
/// behaviours from its ship when the first unit joins, so a shipless one throws, and the agent stays without a formation.
/// </summary>
public class NavalAgentFormationAssigner : AgentFormationAssigner
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalAgentFormationAssigner>();

    private readonly HashSet<Formation> refusedFormations = new HashSet<Formation>();

    // Refuses a shipless formation of a coop naval mission, logging it once; any other mission keeps the base rule.
    protected override bool CanJoin(Formation formation)
    {
        if (!CoopNavalMissionScope.IsActive) return true;
        if (Mission.Current?.GetMissionBehavior<NavalShipsLogic>()?.IsAShipAssignedToFormation(formation) != false) return true;

        if (refusedFormations.Add(formation))
            Logger.Warning("[NavalBattle] {Team} formation {Formation} has no hull, so agents assigned to it stay without a formation",
                formation.Team?.TeamSide, formation.FormationIndex);
        return false;
    }
}
