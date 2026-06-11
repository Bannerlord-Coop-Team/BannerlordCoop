using Common;
using GameInterface.Policies;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.Settlements.Locations;

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

        return ModInformation.IsClient == false;
    }
}
