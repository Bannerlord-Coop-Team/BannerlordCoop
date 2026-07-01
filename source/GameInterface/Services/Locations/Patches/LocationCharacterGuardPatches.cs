using Common;
using GameInterface.Policies;
using HarmonyLib;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Client-side guards around the code paths that move characters between locations outside the
/// patched <see cref="Location"/> mutators.
/// </summary>
[HarmonyPatch]
internal class LocationCharacterGuardPatches
{
    // On clients, hero moves come from server broadcasts. Blocking the whole move (not just the
    // inner add/remove) prevents the mission notification from spawning ghost agents that have no
    // roster entry, e.g. from the in-mission passage-usage AI tick.
    [HarmonyPatch(typeof(LocationComplex), nameof(LocationComplex.ChangeLocation))]
    [HarmonyPrefix]
    static bool ChangeLocationPrefix(LocationCharacter locationCharacter)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        return locationCharacter?.Character?.IsHero != true;
    }

    // Companions accompanying a player are placed by the server for every visiting party. The
    // local spawn would duplicate them, because the mission spawn check compares agent origins by
    // reference and cannot recognize the server-broadcast entry as the same character.
    [HarmonyPatch(typeof(MissionLocationLogic), nameof(MissionLocationLogic.SpawnCharactersAccompanyingPlayer))]
    [HarmonyPrefix]
    static bool SpawnCharactersAccompanyingPlayerPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer;
    }

    // On clients an ambient NPC's origin isn't always in the location's character list, so vanilla's
    // GetLocationCharacter returns null and the door-picking AI NREs on locationCharacter.FixedLocation.
    // Report the passage as unavailable so the agent just skips the door this tick.
    [HarmonyPatch(typeof(ChangeLocationBehavior), nameof(ChangeLocationBehavior.GetAvailability))]
    [HarmonyPrefix]
    static bool GetAvailabilityPrefix(ChangeLocationBehavior __instance, ref float __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (CampaignMission.Current?.Location?.GetLocationCharacter(__instance.OwnerAgent.Origin) != null) return true;

        __result = 0f;
        return false;
    }

    // Same null location-character on the client reaches the passage AI, which hands it to
    // LocationComplex.CanIfMaleOrHero and NREs on locationCharacter.Character. Treat the passage as
    // disabled for that agent so it doesn't path through the door.
    [HarmonyPatch(typeof(PassageUsePoint), nameof(PassageUsePoint.IsDisabledForAgent))]
    [HarmonyPrefix]
    static bool IsDisabledForAgentPrefix(PassageUsePoint __instance, Agent agent, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (!agent.IsAIControlled || __instance.IsMissionExit || __instance.ToLocation == null) return true;
        if (CampaignMission.Current?.Location?.GetLocationCharacter(agent.Origin) != null) return true;

        __result = true;
        return false;
    }
}
