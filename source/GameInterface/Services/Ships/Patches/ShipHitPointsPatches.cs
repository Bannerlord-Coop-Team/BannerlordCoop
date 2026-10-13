using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Patches;

[HarmonyPatch(typeof(Ship))]
internal class ShipHitPointsPatches
{
    // Port menu options like repair ships read the main party's ship hit points
    [HarmonyPatch(nameof(Ship.HitPoints), MethodType.Setter)]
    [HarmonyPostfix]
    public static void HitPointsSetterPostfix(Ship __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance.Owner == null || __instance.Owner != PartyBase.MainParty) return;

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext == null) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
    }
}
