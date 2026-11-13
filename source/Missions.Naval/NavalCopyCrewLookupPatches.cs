using HarmonyLib;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using NavalPhysicsScript = NavalDLC.Missions.NavalPhysics.NavalPhysics;

namespace Missions.Naval;

// Off-ship AI crew joins the nearest afloat hull of its team (AgentNavalComponent.CheckAgentOffShip). A copy's crew is its owner's,
// so on a team that holds both own hulls and copies, as the battle host's ally team does with an allied AI fleet, copies are skipped.
[HarmonyPatch(typeof(TeamAINavalComponent), nameof(TeamAINavalComponent.GetNearestAllyShipFormation))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopySkippingNearestShipPatch
{
    // Searches again past the copies when vanilla picked one.
    [HarmonyPostfix]
    private static void Postfix(TeamAINavalComponent __instance, Agent agent, ref Formation __result)
    {
        var shipsLogic = __instance._navalShipsLogic;
        if (!NavalForeignHullRegistry.IsCopyFormation(shipsLogic, __result)) return;

        var position = agent.Frame.origin;
        __result = shipsLogic.GetNearestTeamShip(__instance.Team.TeamSide, in position, float.MaxValue, IsAfloatOwnShip)?.Formation;
    }

    // Vanilla's afloat filter (not sinking, not abandoned), on a hull this client simulates.
    private static bool IsAfloatOwnShip(MissionShip ship) =>
        ship.Physics.NavalSinkingState == NavalPhysicsScript.SinkingState.Floating && !ship.BeingAbandoned
            && !NavalForeignHullRegistry.IsCopyHere(ship);
}

// An agent stepping onto a team hull outside its own hull's island joins that hull's crew; never a copy's.
[HarmonyPatch(typeof(TeamAINavalComponent), nameof(TeamAINavalComponent.GetConnectedAllyFormation))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopySkippingConnectedShipPatch
{
    // Takes a connected own hull of the team instead of a copy, or none, which leaves the agent off any hull as vanilla does.
    [HarmonyPostfix]
    private static void Postfix(TeamAINavalComponent __instance, ulong shipUniqueBitwiseID, ref Formation __result)
    {
        var shipsLogic = __instance._navalShipsLogic;
        if (!NavalForeignHullRegistry.IsCopyFormation(shipsLogic, __result)) return;

        __result = ConnectedOwnShip(shipsLogic, __instance.Team.TeamSide, shipUniqueBitwiseID)?.Formation;
    }

    // NavalShipsLogic.GetConnectedTeamShip without copies: the hull itself if it is on the team, else the last one in its island.
    private static MissionShip ConnectedOwnShip(NavalShipsLogic shipsLogic, TeamSideEnum teamSide, ulong shipUniqueBitwiseID)
    {
        var teamShips = new MBList<MissionShip>();
        shipsLogic.FillTeamShips(teamSide, teamShips);

        MissionShip connected = null;
        foreach (var ship in teamShips)
        {
            if ((ship.ShipIslandCombinedID & shipUniqueBitwiseID) == 0 || NavalForeignHullRegistry.IsCopyHere(ship)) continue;
            if (ship.ShipUniqueBitwiseID == shipUniqueBitwiseID) return ship;

            connected = ship;
        }

        return connected;
    }
}
