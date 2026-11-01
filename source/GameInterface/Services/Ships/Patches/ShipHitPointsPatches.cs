using Common;
using GameInterface.Services.GameMenus;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Patches;

/// <summary>[Client] Refreshes the open campaign menu when the main party's ship hit points change.</summary>
[HarmonyPatch(typeof(Ship))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class ShipHitPointsPatches
{
    // Port menu options like repair ships read the main party's ship hit points
    [HarmonyPatch(nameof(Ship.HitPoints), MethodType.Setter)]
    [HarmonyPostfix]
    public static void HitPointsSetterPostfix(Ship __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance.Owner == null || __instance.Owner != PartyBase.MainParty) return;
        if (!ContainerProvider.TryResolve<IMenuOptionsRefresher>(out var refresher)) return;

        refresher.RefreshMenuOptions(Campaign.Current?.CurrentMenuContext);
    }
}
