using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class RecruitmentCampaignBehaviorPatch
{
    private const string TownBackstreetMenu = "town_backstreet";

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
    /// it so the server applies it authoritatively.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch("BuyMercenaries")]
    private static bool BuyMercenariesPrefix(RecruitmentCampaignBehavior __instance)
    {
        // The server applies the hire authoritatively from the client's request; let it run vanilla.
        if (ModInformation.IsServer)
            return true;

        if (!TryGetCurrentMercenaryTown(out var town))
            return true;

        var mercenaryData = __instance.GetMercenaryData(town);
        if (mercenaryData.TroopType == null || mercenaryData.Number <= 0)
            return false;

        var mercenaryTroop = CharacterObject.OneToOneConversationCharacter;
        int unitPrice = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
        if (unitPrice <= 0)
            return false;

        int selectedMercenaryCount = __instance._selectedMercenaryCount;
        int count = GetMercenaryHireCount(selectedMercenaryCount, mercenaryData.Number, Hero.MainHero.Gold, unitPrice);
        if (count <= 0)
            return false;

        int goldAmount = count * unitPrice;

        PublishMercenariesHired(__instance, town, mercenaryTroop, count, goldAmount);

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch("BuyMercenaries")]
    private static void BuyMercenariesPostfix(RecruitmentCampaignBehavior __instance)
    {
        if (!ModInformation.IsServer) return;

        if (!TryGetCurrentMercenaryTown(out var town)) return;

        PublishMercenaryStock(__instance, town);
    }

    [HarmonyPrefix]
    [HarmonyPatch("buy_mercenaries_on_consequence")]
    private static bool BuyMercenariesOnConsequencePrefix(RecruitmentCampaignBehavior __instance)
    {
        if (ModInformation.IsServer)
            return true;

        if (!TryGetCurrentMercenaryTown(out var town))
            return false;

        var mercenaryData = __instance.GetMercenaryData(town);
        if (mercenaryData.TroopType == null || mercenaryData.Number <= 0)
            return false;

        int unitPrice = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
        if (unitPrice <= 0)
            return false;

        int count = GetMercenaryHireCount(0, mercenaryData.Number, Hero.MainHero.Gold, unitPrice);
        if (count <= 0)
            return false;

        int goldAmount = count * unitPrice;

        PublishMercenariesHired(__instance, town, mercenaryData.TroopType, count, goldAmount);
        GameMenu.SwitchToMenu(TownBackstreetMenu);

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch("buy_mercenaries_on_consequence")]
    private static void BuyMercenariesOnConsequencePostfix(RecruitmentCampaignBehavior __instance)
    {
        if (!ModInformation.IsServer) return;

        if (!TryGetCurrentMercenaryTown(out var town)) return;

        PublishMercenaryStock(__instance, town);
    }

    [HarmonyPostfix]
    [HarmonyPatch("DailyTickTown")]
    private static void DailyTickTownPostfix(RecruitmentCampaignBehavior __instance, Town town)
    {
        if (!ModInformation.IsServer) return;

        PublishMercenaryStock(__instance, town);
    }

    internal static void PublishMercenaryStock(RecruitmentCampaignBehavior behavior, Town town)
    {
        if (town?.Settlement == null || !town.Settlement.IsTown) return;

        var mercenaryData = behavior.GetMercenaryData(town);
        var message = new MercenaryStockChanged(town, mercenaryData.TroopType, mercenaryData.Number);
        MessageBroker.Instance.Publish(behavior, message);
    }

    private static void PublishMercenariesHired(RecruitmentCampaignBehavior behavior, Town town, CharacterObject mercenaryTroop, int count, int goldAmount)
    {
        var message = new MercenariesHired(
            Hero.MainHero,
            MobileParty.MainParty,
            town,
            mercenaryTroop,
            count,
            goldAmount);
        MessageBroker.Instance.Publish(behavior, message);
    }

    internal static int GetMercenaryHireCount(int selectedMercenaryCount, int availableMercenaries, int heroGold, int unitPrice)
    {
        if (availableMercenaries <= 0 || unitPrice <= 0 || heroGold < unitPrice)
            return 0;

        int affordableCount = heroGold / unitPrice;
        int requestedCount = selectedMercenaryCount <= 0 ? affordableCount : selectedMercenaryCount;

        return TaleWorlds.Library.MathF.Min(availableMercenaries, TaleWorlds.Library.MathF.Min(requestedCount, affordableCount));
    }

    internal static bool TryGetCurrentMercenaryTown(out Town town)
    {
        town = PlayerEncounter.EncounterSettlement?.Town;
        if (town?.Settlement != null && town.Settlement.IsTown)
            return true;

        town = MobileParty.MainParty?.CurrentSettlement?.Town;
        return town?.Settlement != null && town.Settlement.IsTown;
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
