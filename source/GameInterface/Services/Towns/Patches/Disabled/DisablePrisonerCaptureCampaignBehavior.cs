using Common;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(PrisonerCaptureCampaignBehavior))]
internal class DisablePrisonerCaptureCampaignBehavior
{
    [HarmonyPatch(nameof(PrisonerCaptureCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PrisonerCaptureCampaignBehavior))]
internal class PrisonerCaptureCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PrisonerCaptureCampaignBehavior.HandleSettlementHeroes))]
    [HarmonyPrefix]
    public static bool HandleSettlementHeroesPrefix(PrisonerCaptureCampaignBehavior __instance, Settlement settlement)
    {
        for (int i = settlement.HeroesWithoutParty.Count - 1; i >= 0; i--)
        {
            Hero hero = settlement.HeroesWithoutParty[i];
            if (__instance.SettlementHeroCaptureCommonCondition(hero))
            {
                TakePrisonerAction.Apply(hero.CurrentSettlement.Party, hero);
            }
        }
        for (int j = settlement.Parties.Count - 1; j >= 0; j--)
        {
            MobileParty mobileParty = settlement.Parties[j];

            // Replace MobileParty.MainParty usage
            if (mobileParty.IsLordParty
                && (mobileParty.Army == null || (mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty && !mobileParty.Army.IsPlayerArmy())
                && mobileParty.MapEvent == null
                && __instance.SettlementHeroCaptureCommonCondition(mobileParty.LeaderHero)))
            {
                LeaveSettlementAction.ApplyForParty(mobileParty);
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(PrisonerCaptureCampaignBehavior.SettlementHeroCaptureCommonCondition))]
    [HarmonyPrefix]
    public static bool HandleSettlementHeroesPrefix(ref bool __result, Hero hero)
    {
        if (hero.IsPlayerHero())
        {
            __result = false;
            return false;
        }

        return true;
    }
}