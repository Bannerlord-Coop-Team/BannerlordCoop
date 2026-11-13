using HarmonyLib;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        var teamShips = new MBList<MissionShip>();
        shipsLogic.FillTeamShips(__instance.Team.TeamSide, teamShips);
        __result = PickConnectedShip(teamShips, shipUniqueBitwiseID, NavalForeignHullRegistry.IsCopyHere)?.Formation;
    }

    /// <summary>
    /// NavalShipsLogic.GetConnectedTeamShip without copies: the hull itself if it is one of <paramref name="teamShips"/>, else the
    /// last of them in its island, else none.
    /// </summary>
    internal static MissionShip PickConnectedShip(IEnumerable<MissionShip> teamShips, ulong shipUniqueBitwiseID,
        Func<MissionShip, bool> isCopy)
    {
        MissionShip connected = null;
        foreach (var ship in teamShips)
        {
            bool inIsland = (ship.ShipIslandCombinedID & shipUniqueBitwiseID) != 0;
            if (!inIsland || isCopy(ship)) continue;
            if (ship.ShipUniqueBitwiseID == shipUniqueBitwiseID) return ship;

            connected = ship;
        }

        return connected;
    }
}

// Vanilla's team crewing spreads the team's troops over every hull of its team side (NavalTeamAgents keeps one NavalShipAgents
// per hull), copies included; a copy's crew is its owner's, so the crew share and the troop assignment see only own hulls.
[HarmonyPatch]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class CopySkippingCrewSharePatch
{
    // The share of the team's crew per hull and the assignment of its troops to the hulls missing some.
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(NavalTeamAgents), nameof(NavalTeamAgents.AutoComputeDesiredTroopCountsPerShip)),
        AccessTools.Method(typeof(NavalTeamAgents), nameof(NavalTeamAgents.AssignTroops)),
    };

    // Takes the copies out of the team's hulls for the call, with where each stood, last first.
    [HarmonyPrefix]
    private static void Prefix(NavalTeamAgents __instance, out List<(int Index, NavalShipAgents Hull)> __state)
    {
        __state = new List<(int Index, NavalShipAgents Hull)>();
        var hulls = __instance._allShipAgents;
        for (int index = hulls.Count - 1; index >= 0; index--)
        {
            if (!NavalForeignHullRegistry.IsCopyHere(hulls[index].Ship)) continue;

            __state.Add((index, hulls[index]));
            hulls.RemoveAt(index);
        }
    }

    // Puts the copies back where they stood, also when vanilla threw.
    [HarmonyFinalizer]
    private static void Finalizer(NavalTeamAgents __instance, List<(int Index, NavalShipAgents Hull)> __state)
    {
        for (int taken = __state.Count - 1; taken >= 0; taken--)
            __instance._allShipAgents.Insert(__state[taken].Index, __state[taken].Hull);
    }
}
