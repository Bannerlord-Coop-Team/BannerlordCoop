using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(MapNavigationItemVM))]
internal class MapNavigationItemPatch
{
    [HarmonyPatch(nameof(MapNavigationItemVM.RefreshStates))]
    static bool Prefix() => true;
}

