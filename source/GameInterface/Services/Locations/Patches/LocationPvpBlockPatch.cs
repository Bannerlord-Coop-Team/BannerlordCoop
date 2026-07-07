using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Disables PVP in coop location missions (tavern/indoor, town centre, castle courtyard, village): a blow whose
/// victim or attacker is a remote player's puppet is suppressed before the engine applies it. Locations sync
/// presence and movement but have NO damage routing (unlike coop field battles, where
/// <see cref="MapEvents.Patches.BattleBlowInterceptPatch"/> routes a puppet blow to its owner), so an applied
/// blow would be local-only: the victim's owner never hears about it and the "dead" player keeps walking on
/// every other client. Remote players spawn as puppets with <see cref="AgentControllerType.None"/>
/// (<c>CoopLocationsController.SpawnAgent</c>); native NPCs are AI/Player-controlled and take blows as usual,
/// so brawling with the locals still works — just not with other players. The attacker side is guarded too:
/// a puppet's replicated attack animation still produces real melee hits on this client, which would otherwise
/// damage the local player.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.RegisterBlow))]
internal class LocationPvpBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance, Blow blow)
    {
        var mission = Mission.Current;
        if (!LocationMissionTracker.IsLocationMission(mission)) return true;

        if (IsRemotePlayerPuppet(__instance)) return false;
        if (IsRemotePlayerPuppet(mission.FindAgentWithIndex(blow.OwnerId))) return false;

        return true;
    }

    // Matches how CoopLocationsController spawns remote players: human + AgentControllerType.None. A puppet's
    // mount counts as the puppet (village puppets ride in), so the horse can't be killed from under them either.
    private static bool IsRemotePlayerPuppet(Agent agent)
    {
        if (agent == null) return false;
        if (agent.IsMount) agent = agent.RiderAgent;

        return agent != null && agent.IsHuman && agent.Controller == AgentControllerType.None;
    }
}
