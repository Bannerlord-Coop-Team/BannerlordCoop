using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class RecruitmentCampaignBehaviorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("CheckRecruiting")]
    private static bool CheckRecruitingPrefix(ref MobileParty mobileParty, ref Settlement settlement)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("HourlyTickParty")]
    private static bool HourlyTickPartyPrefix(ref MobileParty mobileParty)
    {
        // TODO disable for player parties
        return false;
    }

    /// <summary>
    /// Mercenary tavern hire runs on the conversing client and adds the troops + deducts the gold
    /// locally, so neither reaches the server or other clients. Suppress the local apply and forward
    /// it so the server applies it authoritatively; the local mercenary stock is still decremented
    /// here so the hiring client cannot re-offer the same mercenaries before the server responds.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("BuyMercenaries")]
    private static bool BuyMercenariesPrefix(RecruitmentCampaignBehavior __instance)
    {
        // The server applies the hire authoritatively from the client's request; let it run vanilla.
        if (ModInformation.IsServer)
            return true;

        var town = PlayerEncounter.EncounterSettlement?.Town;
        if (town == null)
            return true;

        int count = __instance._selectedMercenaryCount;
        if (count <= 0)
            return false;

        // The troop added is the conversation mercenary, but the price comes from the town's stocked
        // mercenary type — matching vanilla BuyMercenaries and the gold the dialogue quoted.
        var mercenaryData = __instance.GetMercenaryData(town);
        var mercenaryTroop = CharacterObject.OneToOneConversationCharacter;
        int unitPrice = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
        int goldAmount = count * unitPrice;

        mercenaryData.ChangeMercenaryCount(-count);

        var message = new MercenariesHired(
            Hero.MainHero,
            MobileParty.MainParty,
            town,
            mercenaryTroop,
            count,
            goldAmount);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement))]
    [HarmonyPostfix]
    public static void UpdateVolunteersOfNotablesInSettlementPostfix(Settlement settlement)
    {
        if ((settlement.IsTown && !settlement.Town.InRebelliousState) || (settlement.IsVillage && !settlement.Village.Bound.Town.InRebelliousState))
        {
            Dictionary<Hero, CharacterObject[]> updatedVolunteerTypes = new();
            foreach (Hero hero in settlement.Notables)
            {
                updatedVolunteerTypes[hero] = hero.VolunteerTypes;
            }

            // Need to manually sync the updated volunteer types
            var message = new VolunteersUpdated(updatedVolunteerTypes);
            MessageBroker.Instance.Publish(null, message);
        }
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Hero individual, int number, int bitCode, RecruitmentCampaignBehavior.RecruitingDetail detail)
    {
        if (detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual && detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
        {
            return;
        }

        // Need to manually sync the available volunteer types when removed on the server (e.g. by AI parties recruiting volunteers)
        var message = new VolunteerRemoved(individual, bitCode);
        MessageBroker.Instance.Publish(null, message);
    }
}