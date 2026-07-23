using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
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
    private static bool CheckRecruitingPrefix(RecruitmentCampaignBehavior __instance, ref MobileParty mobileParty, ref Settlement settlement, out MercenaryStockState __state)
    {
        if (ModInformation.IsClient)
        {
            __state = default;
            return false;
        }

        __state = GetMercenaryStockState(__instance, settlement?.Town);
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch("CheckRecruiting")]
    private static void CheckRecruitingPostfix(RecruitmentCampaignBehavior __instance, ref Settlement settlement, MercenaryStockState __state, bool __runOriginal)
    {
        if (ModInformation.IsClient || !__runOriginal) return;

        PublishMercenaryStockIfChanged(__instance, settlement?.Town, __state);
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
        // Clients forward hires to the server; if this ever runs on the server, leave vanilla behavior alone.
        if (ModInformation.IsServer)
            return true;

        TryPublishMercenariesHired(__instance, __instance._selectedMercenaryCount, useConversationCharacter: true);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("buy_mercenaries_on_consequence")]
    private static bool BuyMercenariesOnConsequencePrefix(RecruitmentCampaignBehavior __instance)
    {
        if (ModInformation.IsServer)
            return true;

        if (TryPublishMercenariesHired(__instance, 0, useConversationCharacter: false))
        {
            GameMenu.SwitchToMenu(TownBackstreetMenu);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("DailyTickTown")]
    private static void DailyTickTownPrefix(RecruitmentCampaignBehavior __instance, Town town, out MercenaryStockState __state)
    {
        __state = ModInformation.IsServer
            ? GetMercenaryStockState(__instance, town)
            : default;
    }

    [HarmonyPostfix]
    [HarmonyPatch("DailyTickTown")]
    private static void DailyTickTownPostfix(RecruitmentCampaignBehavior __instance, Town town, MercenaryStockState __state)
    {
        if (ModInformation.IsClient) return;

        PublishMercenaryStockIfChanged(__instance, town, __state);
    }

    internal static void PublishMercenaryStock(RecruitmentCampaignBehavior behavior, Town town)
    {
        if (!MercenaryStockHandler.IsMercenaryTown(town)) return;

        var mercenaryData = behavior.GetMercenaryData(town);
        var message = new MercenaryStockChanged(town, mercenaryData.TroopType, mercenaryData.Number);
        MessageBroker.Instance.Publish(behavior, message);
    }

    private static void PublishMercenaryStockIfChanged(RecruitmentCampaignBehavior behavior, Town town, MercenaryStockState previousStock)
    {
        var currentStock = GetMercenaryStockState(behavior, town);
        if (!currentStock.IsMercenaryTown ||
            !IsMercenaryStockChanged(previousStock.TroopType, previousStock.Number, currentStock.TroopType, currentStock.Number)) return;

        PublishMercenaryStock(behavior, town);
    }

    private static MercenaryStockState GetMercenaryStockState(RecruitmentCampaignBehavior behavior, Town town)
    {
        if (!MercenaryStockHandler.IsMercenaryTown(town)) return default;

        var mercenaryData = behavior.GetMercenaryData(town);
        return new MercenaryStockState(true, mercenaryData.TroopType, mercenaryData.Number);
    }

    private static bool TryPublishMercenariesHired(RecruitmentCampaignBehavior behavior, int selectedMercenaryCount, bool useConversationCharacter)
    {
        if (!TryGetCurrentMercenaryTown(out var town))
            return false;

        var mercenaryData = behavior.GetMercenaryData(town);
        if (mercenaryData.TroopType == null || mercenaryData.Number <= 0)
            return false;

        CharacterObject mercenaryTroop = useConversationCharacter
            ? CharacterObject.OneToOneConversationCharacter
            : mercenaryData.TroopType;
        if (mercenaryTroop == null)
            return false;

        int unitPrice = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero).RoundedResultNumber;
        if (unitPrice <= 0)
            return false;

        int count = GetMercenaryHireCount(selectedMercenaryCount, mercenaryData.Number, Hero.MainHero.Gold, unitPrice);
        if (count <= 0)
            return false;

        int goldAmount = count * unitPrice;
        PublishMercenariesHired(behavior, town, mercenaryTroop, count, goldAmount);

        return true;
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

    internal static bool IsMercenaryStockChanged(CharacterObject previousTroopType, int previousNumber, CharacterObject currentTroopType, int currentNumber)
    {
        return previousTroopType != currentTroopType || previousNumber != currentNumber;
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
        if (MercenaryStockHandler.IsMercenaryTown(town))
            return true;

        town = MobileParty.MainParty?.CurrentSettlement?.Town;
        return MercenaryStockHandler.IsMercenaryTown(town);
    }

    private readonly struct MercenaryStockState
    {
        public readonly bool IsMercenaryTown;
        public readonly CharacterObject TroopType;
        public readonly int Number;

        public MercenaryStockState(bool isMercenaryTown, CharacterObject troopType, int number)
        {
            IsMercenaryTown = isMercenaryTown;
            TroopType = troopType;
            Number = number;
        }
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement))]
    [HarmonyPostfix]
    public static void UpdateVolunteersOfNotablesInSettlementPostfix(Settlement settlement)
    {
        // Volunteer generation is server-authoritative; only the server may publish the snapshot.
        // Without this, a client's locally-generated volunteers would be sent back to the server.
        if (ModInformation.IsClient) return;

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
        // Server-authoritative; clients forward recruitment via RecruitmentVMPatches. Mirrors the update postfix.
        if (ModInformation.IsClient) return;

        if (detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual && detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
        {
            return;
        }

        // Need to manually sync the available volunteer types when removed on the server (e.g. by AI parties recruiting volunteers)
        var message = new VolunteerRemoved(individual, bitCode);
        MessageBroker.Instance.Publish(null, message);
    }
}
