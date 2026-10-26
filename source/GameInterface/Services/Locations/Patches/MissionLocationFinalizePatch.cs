using HarmonyLib;
using SandBox.Missions.MissionLogics;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Keeps vanilla location cleanup safe when leaving a settlement has already cleared the
/// <see cref="PlayerEncounter.LocationEncounter"/> before mission behaviors are finalized.
/// </summary>
[HarmonyPatch(typeof(MissionLocationLogic), nameof(MissionLocationLogic.OnRemoveBehavior))]
internal static class MissionLocationFinalizePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(MissionLocationLogic __instance)
    {
        LocationComplex locationComplex = null;
        try
        {
            locationComplex = LocationComplex.Current;
        }
        catch (NullReferenceException)
        {
            // Campaign teardown can clear the encounter object while the mission still owns its location.
        }

        if (locationComplex != null)
            return true;

        locationComplex = CampaignMission.Current?.Location?._ownerComplex;
        if (locationComplex != null)
        {
            foreach (Location location in locationComplex.GetListOfLocations())
            {
                if (location == null || !IsTemporaryLocation(location.StringId)) continue;

                location.RemoveAllCharacters(character => character?.Character?.IsHero != true);
            }
        }

        CampaignEventDispatcher.Instance?.RemoveListeners(__instance);
        return false;
    }

    private static bool IsTemporaryLocation(string locationId) =>
        locationId == "center" || locationId == "village_center" || locationId == "lordshall" ||
        locationId == "prison" || locationId == "tavern" || locationId == "alley" || locationId == "port";
}
