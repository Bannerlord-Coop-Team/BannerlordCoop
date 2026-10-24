using HarmonyLib;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// Applied ship fates reach clients still in a battle, where ShipHitPointsPatches.HitPointsSetterPostfix's menu refresh can
// find a menu context with no handler. Delete once that postfix skips during a mission.
[HarmonyPatch(typeof(GameMenuManager), nameof(GameMenuManager.RefreshMenuOptions))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class MissionMenuRefreshHoldPatch
{
    // Holds every menu refresh while a mission is current; MapState.OnActivate refreshes the menu on return.
    [HarmonyPrefix]
    internal static bool Prefix() => Mission.Current == null;
}
