using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(InfluenceGainCampaignBehavior))]
internal class DisableInfluenceGainCampaignBehavior
{
    [HarmonyPatch(nameof(InfluenceGainCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(InfluenceGainCampaignBehavior))]
internal class InfluenceGainCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(InfluenceGainCampaignBehavior.OnPrisonerDonatedToSettlement))]
    [HarmonyPrefix]
    public static bool OnPrisonerDonatedToSettlementPrefix(MobileParty donatingParty, FlattenedTroopRoster donatedPrisoners, Settlement donatedSettlement)
    {
        // Donating party is part of the same clan as the settlement, don't give influence
        if (donatedSettlement.OwnerClan == donatingParty.ActualClan) return false;

        float gainedInfluence = 0f;
        foreach (FlattenedTroopRosterElement flattenedTroopRosterElement in donatedPrisoners)
        {
            gainedInfluence += Campaign.Current.Models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(donatingParty.Party, flattenedTroopRosterElement.Troop, donatedSettlement);
        }
        GainKingdomInfluenceAction.ApplyForDonatePrisoners(donatingParty, gainedInfluence);

        return false;
    }
}