using Common;
using GameInterface.Services.MobileParties.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class DisablePartiesSellPrisonerCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior))]
internal class PartiesSellPrisonerCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(PartiesSellPrisonerCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        if (!ContainerProvider.TryResolve<IPartiesSellPrisonerCampaignBehaviorInterface>(out var partiesSellPrisonerCampaignBehaviorInterface)) return false;

        // Replace checks for player characters and heroes
        partiesSellPrisonerCampaignBehaviorInterface.OnSettlementEntered(__instance, mobileParty, settlement);

        return false;
    }

    [HarmonyPatch(nameof(PartiesSellPrisonerCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix(PartiesSellPrisonerCampaignBehavior __instance, Settlement settlement)
    {
        if (!ContainerProvider.TryResolve<IPartiesSellPrisonerCampaignBehaviorInterface>(out var partiesSellPrisonerCampaignBehaviorInterface)) return false;

        // Replace MainHero checks
        partiesSellPrisonerCampaignBehaviorInterface.DailyTickSettlement(__instance, settlement);

        return false;
    }
}