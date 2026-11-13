using HarmonyLib;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using NavalPhysicsScript = NavalDLC.Missions.NavalPhysics.NavalPhysics;

namespace Missions.Naval;

// Off-ship AI crew joins the nearest afloat hull of its team (AgentNavalComponent.CheckAgentOffShip). A copy's crew is its owner's,
// so on a team that holds both own hulls and copies, as the battle host's ally team does with an allied AI fleet, copies are skipped.
[HarmonyPatch(typeof(TeamAINavalComponent), nameof(TeamAINavalComponent.GetNearestAllyShipFormation))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopySkippingNearestShipPatch
{
    // Searches again past the copies when vanilla picked one, with vanilla's afloat filter.
    [HarmonyPostfix]
    private static void Postfix(TeamAINavalComponent __instance, Agent agent, ref Formation __result)
    {
        var shipsLogic = __instance._navalShipsLogic;
        if (!IsCopyFormation(shipsLogic, __result)) return;

        var position = agent.Frame.origin;
        __result = shipsLogic.GetNearestTeamShip(__instance.Team.TeamSide, in position, float.MaxValue,
            ship => ship.Physics.NavalSinkingState == NavalPhysicsScript.SinkingState.Floating && !ship.BeingAbandoned
                && !NavalForeignHullRegistry.IsCopyHere(ship))?.Formation;
    }

    /// <summary>Whether <paramref name="formation"/> is a copy's, which no local agent may join.</summary>
    internal static bool IsCopyFormation(NavalShipsLogic shipsLogic, Formation formation) =>
        formation != null && shipsLogic.GetShip(formation, out var ship) && NavalForeignHullRegistry.IsCopyHere(ship);
}

// An agent stepping onto a team hull outside its own hull's island joins that hull's crew; never a copy's.
[HarmonyPatch(typeof(TeamAINavalComponent), nameof(TeamAINavalComponent.GetConnectedAllyFormation))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopySkippingConnectedShipPatch
{
    // NavalShipsLogic keeps eleven ship assignment slots per team side.
    private const int AssignmentSlots = 11;

    // Takes a connected own hull of the team instead of a copy, or none, which leaves the agent off any hull as vanilla does.
    [HarmonyPostfix]
    private static void Postfix(TeamAINavalComponent __instance, ulong shipUniqueBitwiseID, ref Formation __result)
    {
        var shipsLogic = __instance._navalShipsLogic;
        if (!CopySkippingNearestShipPatch.IsCopyFormation(shipsLogic, __result)) return;

        __result = ConnectedOwnShip(shipsLogic, __instance.Team.TeamSide, shipUniqueBitwiseID)?.Formation;
    }

    // The first team hull in the island of shipUniqueBitwiseID that is not a copy, over vanilla's assignment slots.
    private static MissionShip ConnectedOwnShip(NavalShipsLogic shipsLogic, TeamSideEnum teamSide, ulong shipUniqueBitwiseID)
    {
        for (int index = 0; index < AssignmentSlots; index++)
        {
            var assignment = shipsLogic.GetShipAssignment(teamSide, (FormationClass)index);
            if (!assignment.HasMissionShip) continue;

            var ship = assignment.MissionShip;
            if ((ship.ShipIslandCombinedID & shipUniqueBitwiseID) != 0 && !NavalForeignHullRegistry.IsCopyHere(ship)) return ship;
        }

        return null;
    }
}
