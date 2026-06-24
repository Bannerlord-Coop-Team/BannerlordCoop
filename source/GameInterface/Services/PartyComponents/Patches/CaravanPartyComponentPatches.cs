using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(CaravanPartyComponent))]
internal class CaravanPartyComponentPatches
{
    [HarmonyPatch(nameof(CaravanPartyComponent.GetDefaultComponentBanner))]
    [HarmonyPrefix]
    public static bool GetDefaultComponentBannerPrefix(ref CaravanPartyComponent __instance, ref Banner __result)
    {
        if (__instance.Leader != null)
        {
            __result = __instance.Leader.ClanBanner;
        }
        else if (__instance.Owner.IsPlayerHero()) // Replace Hero.MainHero with IsPlayerHero()
        {
            __result = __instance.Owner.MapFaction.Banner;
        }
        else
        {
            __result = __instance.Owner.HomeSettlement.OwnerClan.MapFaction.Banner;
        }

        return false;
    }
}