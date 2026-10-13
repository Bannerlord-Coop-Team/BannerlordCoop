using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] In a naval battle, an agent only joins a formation that has a hull. NavalDLC's team AI builds a
/// formation's behaviours from its ship when the first unit joins (BehaviorNavalEngageCorrespondingEnemy reads the
/// ship's entity), so a shipless formation throws there; such an agent stays without a formation until allied fleets
/// are fielded.
/// </summary>
public class NavalAgentFormationAssigner : AgentFormationAssigner
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalAgentFormationAssigner>();

    private readonly HashSet<Formation> refusedFormations = new HashSet<Formation>();

    internal static bool CanJoinFormation(bool isNavalBattle, bool formationHasHull) => !isNavalBattle || formationHasHull;

    protected override bool CanJoin(Formation formation)
    {
        var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
        bool isNavalBattle = shipsLogic != null;
        if (CanJoinFormation(isNavalBattle, isNavalBattle && shipsLogic.IsAShipAssignedToFormation(formation))) return true;

        if (refusedFormations.Add(formation))
            Logger.Warning("[NavalBattle] {Team} formation {Formation} has no hull, so agents assigned to it stay without a formation",
                formation.Team?.TeamSide, formation.FormationIndex);
        return false;
    }
}
