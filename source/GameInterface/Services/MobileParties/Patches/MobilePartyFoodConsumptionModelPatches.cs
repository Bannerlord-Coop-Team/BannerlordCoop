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
        var leaderHero = mobileParty.LeaderHero;

        // Match vanilla eligibility while recognizing every co-op player clan.
        __result = mobileParty.IsActive &&
                   (leaderHero == null ||
                    leaderHero.IsLord ||
                    leaderHero.Clan?.IsPlayerClan() == true ||
                    leaderHero.IsMinorFactionHero) &&
                   !mobileParty.IsGarrison &&
                   !mobileParty.IsCaravan &&
                   !mobileParty.IsBandit &&
                   !mobileParty.IsMilitia &&
                   !mobileParty.IsPatrolParty &&
                   !mobileParty.IsVillager;

        return false;
    }
}
