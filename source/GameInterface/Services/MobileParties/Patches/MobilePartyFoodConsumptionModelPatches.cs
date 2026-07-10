using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DefaultMobilePartyFoodConsumptionModel))]
internal class MobilePartyFoodConsumptionModelPatches
{
    [HarmonyPatch(nameof(DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood))]
    [HarmonyPrefix]
    public static bool DoesPartyConsumeFoodPrefix(DefaultMobilePartyFoodConsumptionModel __instance, ref bool __result, MobileParty mobileParty)
    {
        // Override PlayerClan check
        if (mobileParty.IsActive && (mobileParty.LeaderHero == null || mobileParty.LeaderHero.Clan.IsPlayerClan()))
        {
            __result = true;
            return false;
        }

        return true;
    }
}
