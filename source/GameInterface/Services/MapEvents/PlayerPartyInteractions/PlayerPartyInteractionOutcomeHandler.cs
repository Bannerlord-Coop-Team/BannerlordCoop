using Common;
using Common.Logging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal readonly struct PlayerPartyInteractionOutcome
{
    public readonly string SessionId;
    public readonly string InitiatorPartyId;
    public readonly string ResponderPartyId;
    public readonly PlayerPartyInteractionOutcomeType OutcomeType;
    public readonly ItemRosterElementData[] InitiatorOfferedItems;
    public readonly ItemRosterElementData[] ResponderOfferedItems;
    public readonly int InitiatorOfferedGold;
    public readonly int ResponderOfferedGold;
    public readonly string[] InitiatorOfferedFiefs;
    public readonly string[] ResponderOfferedFiefs;
    public readonly TroopRosterElementData[] InitiatorOfferedPrisoners;
    public readonly TroopRosterElementData[] ResponderOfferedPrisoners;
    public readonly TroopRosterElementData[] InitiatorOfferedTroops;
    public readonly TroopRosterElementData[] ResponderOfferedTroops;
    public readonly bool InitiatorOfferedPeace;
    public readonly bool ResponderOfferedPeace;

    public PlayerPartyInteractionOutcome(PlayerPartyInteractionSession session, PlayerPartyInteractionOutcomeType outcomeType)
    {
        SessionId = session.SessionId;
        InitiatorPartyId = session.InitiatorPartyId;
        ResponderPartyId = session.ResponderPartyId;
        OutcomeType = outcomeType;
        InitiatorOfferedItems = session.InitiatorOfferedItems ?? new ItemRosterElementData[0];
        ResponderOfferedItems = session.ResponderOfferedItems ?? new ItemRosterElementData[0];
        InitiatorOfferedGold = session.InitiatorOfferedGold;
        ResponderOfferedGold = session.ResponderOfferedGold;
        InitiatorOfferedFiefs = session.InitiatorOfferedFiefs ?? new string[0];
        ResponderOfferedFiefs = session.ResponderOfferedFiefs ?? new string[0];
        InitiatorOfferedPrisoners = session.InitiatorOfferedPrisoners ?? new TroopRosterElementData[0];
        ResponderOfferedPrisoners = session.ResponderOfferedPrisoners ?? new TroopRosterElementData[0];
        InitiatorOfferedTroops = session.InitiatorOfferedTroops ?? new TroopRosterElementData[0];
        ResponderOfferedTroops = session.ResponderOfferedTroops ?? new TroopRosterElementData[0];
        InitiatorOfferedPeace = session.InitiatorOfferedPeace;
        ResponderOfferedPeace = session.ResponderOfferedPeace;
    }
}

internal class PlayerPartyInteractionOutcomeHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyInteractionOutcomeHandler>();

    private readonly IObjectManager objectManager;
    private readonly IKingdomMembershipState kingdomMembershipState;

    public PlayerPartyInteractionOutcomeHandler(
        IObjectManager objectManager,
        IKingdomMembershipState kingdomMembershipState)
    {
        this.objectManager = objectManager;
        this.kingdomMembershipState = kingdomMembershipState;
    }

    public bool Handle(PlayerPartyInteractionOutcome outcome)
    {
        switch (outcome.OutcomeType)
        {
            case PlayerPartyInteractionOutcomeType.TradeAccepted:
                return HandleTradeAccepted(outcome);
            case PlayerPartyInteractionOutcomeType.ClanJoinAccepted:
                return HandleClanJoinAccepted(outcome);
            case PlayerPartyInteractionOutcomeType.VassalAccepted:
                return HandleVassalAccepted(outcome);
            case PlayerPartyInteractionOutcomeType.MercenaryAccepted:
                return HandleMercenaryAccepted(outcome);
            default:
                return true;
        }
    }

    private bool HandleVassalAccepted(PlayerPartyInteractionOutcome outcome)
    {
        try
        {
            bool result = false;
            RunOnGameThread(() => result = ApplyVassalage(outcome), "Apply player-party vassalage");
            return result;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Failed to apply player-party vassalage. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.SessionId,
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }
    }

    private bool ApplyVassalage(PlayerPartyInteractionOutcome outcome)
    {
        if (!objectManager.TryGetObject(outcome.InitiatorPartyId, out PartyBase initiatorParty))
        {
            Logger.Warning("Unable to apply player-party vassalage: initiator party not found. PartyId={PartyId}", outcome.InitiatorPartyId);
            return false;
        }

        if (!objectManager.TryGetObject(outcome.ResponderPartyId, out PartyBase responderParty))
        {
            Logger.Warning("Unable to apply player-party vassalage: responder party not found. PartyId={PartyId}", outcome.ResponderPartyId);
            return false;
        }

        var initiatorClan = initiatorParty.LeaderHero?.Clan ?? initiatorParty.MobileParty?.ActualClan;
        var responderHero = responderParty.LeaderHero;
        var targetKingdom = responderHero?.Clan?.Kingdom;
        var previousKingdom = initiatorClan?.Kingdom;
        if (initiatorClan == null ||
            initiatorClan.Tier < Campaign.Current.Models.ClanTierModel.VassalEligibleTier ||
            responderHero?.IsKingdomLeader != true ||
            targetKingdom?.RulingClan != responderHero.Clan ||
            (initiatorClan.Kingdom != null && !initiatorClan.IsUnderMercenaryService))
        {
            Logger.Warning(
                "Unable to apply player-party vassalage: eligibility changed before acceptance. InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }
        if (initiatorClan.Kingdom == targetKingdom)
        {
            if (!initiatorClan.IsUnderMercenaryService)
            {
                Logger.Warning("Rejected vassal service request because clan {ClanId} already belongs to kingdom {KingdomId}", initiatorClan, targetKingdom);
                return false;
            }

            EndMercenaryServiceAction.EndByBecomingVassal(initiatorClan);
        }
        else
        {
            if (initiatorClan.Kingdom != null)
            {
                if (!initiatorClan.IsUnderMercenaryService)
                {
                    Logger.Warning("Rejected vassal service request because clan {ClanId} already belongs to another kingdom", initiatorClan);
                    return false;
                }

                EndMercenaryServiceAction.EndByLeavingKingdom(initiatorClan);
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(initiatorClan, targetKingdom);
        }

        if (initiatorClan.Kingdom != targetKingdom || initiatorClan.IsUnderMercenaryService)
        {
            Logger.Error("Vassal service did not place clan {ClanId} in kingdom {KingdomId}", initiatorClan, targetKingdom);
            return false;
        }

        kingdomMembershipState.MoveClanToKingdom(
            previousKingdom,
            targetKingdom,
            initiatorClan,
            publishCollectionChanges: true,
            republishExistingCollections: true);
        if (!targetKingdom.Clans.Contains(initiatorClan))
        {
            Logger.Error("Vassal service did not add clan {ClanId} to kingdom {KingdomId} collections", initiatorClan, targetKingdom);
            return false;
        }
        return true;
    }

    private bool HandleClanJoinAccepted(PlayerPartyInteractionOutcome outcome)
    {
        try
        {
            bool result = false;
            RunOnGameThread(() => result = ApplyClanJoin(outcome), "Apply player-party clan join");
            return result;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Failed to apply player-party clan join. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.SessionId,
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }
    }

    private bool HandleTradeAccepted(PlayerPartyInteractionOutcome outcome)
    {
        try
        {
            bool result = false;
            RunOnGameThread(() => result = ApplyAcceptedTrade(outcome), "Apply player-party trade");
            return result;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Failed to apply player-party trade. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.SessionId,
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }
    }

    private bool ApplyClanJoin(PlayerPartyInteractionOutcome outcome)
    {
        if (!objectManager.TryGetObject(outcome.InitiatorPartyId, out PartyBase initiatorParty))
        {
            Logger.Warning("Unable to apply player-party clan join: initiator party not found. PartyId={PartyId}", outcome.InitiatorPartyId);
            return false;
        }

        if (!objectManager.TryGetObject(outcome.ResponderPartyId, out PartyBase responderParty))
        {
            Logger.Warning("Unable to apply player-party clan join: responder party not found. PartyId={PartyId}", outcome.ResponderPartyId);
            return false;
        }

        var initiatorHero = initiatorParty.LeaderHero;
        var responderClan = responderParty.LeaderHero?.Clan;
        if (initiatorHero == null || responderClan == null)
        {
            Logger.Warning(
                "Unable to apply player-party clan join: missing initiator hero or responder clan. InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }

        initiatorHero.Clan = responderClan;
        if (initiatorParty.MobileParty != null)
            initiatorParty.MobileParty.ActualClan = responderClan;
        return true;
    }

    private bool HandleMercenaryAccepted(PlayerPartyInteractionOutcome outcome)
    {
        try
        {
            bool result = false;
            RunOnGameThread(() => result = ApplyMercenaryJoin(outcome), "Apply player-party Mercenary join");
            return result;
        }
        catch (Exception e)
        {
            Logger.Error(e,
                "Failed to apply player-party mercenary join. SessionId={SessionId}, InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
                outcome.SessionId,
                outcome.InitiatorPartyId,
                outcome.ResponderPartyId);
            return false;
        }
    }

    private bool ApplyMercenaryJoin(PlayerPartyInteractionOutcome outcome)
    {
        if (!objectManager.TryGetObject(outcome.InitiatorPartyId, out PartyBase initiatorParty))
        {
            Logger.Warning("Unable to apply player-party mercenary: initiator party not found. PartyId={PartyId}", outcome.InitiatorPartyId);
            return false;
        }

        if (!objectManager.TryGetObject(outcome.ResponderPartyId, out PartyBase responderParty))
        {
            Logger.Warning("Unable to apply player-party mercenary: responder party not found. PartyId={PartyId}", outcome.ResponderPartyId);
            return false;
        }

        var initiatorClan = initiatorParty.LeaderHero?.Clan ?? initiatorParty.MobileParty?.ActualClan;
        var responderHero = responderParty.LeaderHero;
        var targetKingdom = responderHero?.Clan?.Kingdom;
        // Initiator clan must not be at war with anyone the target kingdom is at peace with.
        var initiatorWars = initiatorClan.MapFaction?.FactionsAtWarWith;
        bool sameWars = true;
        if (initiatorWars != null)
        {
            foreach (var enemyFaction in initiatorWars)
            {
                if (enemyFaction == null) continue;
                if (!FactionManager.IsAtWarAgainstFaction(targetKingdom, enemyFaction))
                {
                    sameWars = false;
                    break;
                }
            }
        }

        if (initiatorClan == null
            || initiatorParty.LeaderHero != initiatorClan.Leader
            || targetKingdom == null
            || initiatorClan.Tier < Campaign.Current.Models.ClanTierModel.MercenaryEligibleTier
            || (initiatorClan.IsUnderMercenaryService && initiatorClan.Kingdom == targetKingdom)
            || !initiatorClan.Settlements.IsEmpty<Settlement>()
            || initiatorParty.LeaderHero.GetRelation(responderHero) < (float)Campaign.Current.Models.DiplomacyModel.MinimumRelationWithConversationCharacterToJoinKingdom
            || (initiatorClan.MapFaction.IsKingdomFaction && !initiatorClan.IsUnderMercenaryService)
            || !sameWars)
        {
            Logger.Warning(
                "Unable to apply player-party mercenary: eligibility changed before acceptance. InitiatorPartyId={InitiatorPartyId}, ResponderPartyId={ResponderPartyId}",
            outcome.InitiatorPartyId,
            outcome.ResponderPartyId);
            return false;
        }
        int awardMultiplier = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(initiatorClan, targetKingdom, true);
        if (initiatorClan.MapFaction.IsKingdomFaction && initiatorClan.IsUnderMercenaryService)
        {
            awardMultiplier = awardMultiplier * 3 / 2;
            ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(initiatorClan, true);
        }
        ChangeKingdomAction.ApplyByJoinFactionAsMercenary(initiatorClan, targetKingdom, default, awardMultiplier, true);
        GainKingdomInfluenceAction.ApplyForJoiningFaction(initiatorClan.Leader, 5f);
        return true;
    }
    private bool ApplyAcceptedTrade(PlayerPartyInteractionOutcome outcome)
    {
        if (!objectManager.TryGetObject(outcome.InitiatorPartyId, out PartyBase initiatorParty))
        {
            Logger.Warning("Unable to apply player-party trade: initiator party not found. PartyId={PartyId}", outcome.InitiatorPartyId);
            return false;
        }

        if (!objectManager.TryGetObject(outcome.ResponderPartyId, out PartyBase responderParty))
        {
            Logger.Warning("Unable to apply player-party trade: responder party not found. PartyId={PartyId}", outcome.ResponderPartyId);
            return false;
        }

        var initiatorOffer = BuildAcceptedOffer(
            initiatorParty,
            outcome.InitiatorOfferedItems,
            outcome.InitiatorOfferedTroops,
            outcome.InitiatorOfferedGold,
            outcome.InitiatorOfferedFiefs,
            outcome.InitiatorOfferedPrisoners);
        var responderOffer = BuildAcceptedOffer(
            responderParty,
            outcome.ResponderOfferedItems,
            outcome.ResponderOfferedTroops,
            outcome.ResponderOfferedGold,
            outcome.ResponderOfferedFiefs,
            outcome.ResponderOfferedPrisoners);

        ApplyOffer(initiatorParty, responderParty, initiatorOffer);
        ApplyOffer(responderParty, initiatorParty, responderOffer);

        if (outcome.InitiatorOfferedPeace && outcome.ResponderOfferedPeace)
        {
            ApplyPeace(initiatorParty, responderParty);
        }
        return true;
    }

    private static void ApplyPeace(PartyBase initiatorParty, PartyBase responderParty)
    {
        if (!PlayerPartyPeaceBarterable.CanOfferPeace(initiatorParty, responderParty)) return;

        var initiatorFaction = PlayerPartyPeaceBarterable.GetMapFaction(initiatorParty);
        var responderFaction = PlayerPartyPeaceBarterable.GetMapFaction(responderParty);
        if (initiatorFaction == null || responderFaction == null || initiatorFaction == responderFaction) return;

        MakePeaceAction.Apply(initiatorFaction, responderFaction);
    }

    private AcceptedTradeOffer BuildAcceptedOffer(
        PartyBase sourceParty,
        ItemRosterElementData[] offeredItems,
        TroopRosterElementData[] offeredTroops,
        int offeredGold,
        string[] offeredFiefs,
        TroopRosterElementData[] offeredPrisoners)
    {
        var offer = new AcceptedTradeOffer
        {
            Gold = Math.Max(0, Math.Min(offeredGold, sourceParty.LeaderHero?.Gold ?? 0))
        };

        AddItemTransfers(offer, sourceParty, offeredItems);
        AddTroopTransfers(offer, sourceParty, offeredTroops);
        AddFiefTransfers(offer, sourceParty, offeredFiefs);
        AddPrisonerTransfers(offer, sourceParty, offeredPrisoners);

        return offer;
    }

    private void AddItemTransfers(AcceptedTradeOffer offer, PartyBase sourceParty, ItemRosterElementData[] offeredItems)
    {
        var requestedItems = BuildItemRequests(offeredItems);
        foreach (var request in requestedItems.Values)
        {
            if (!TryResolveEquipmentElement(request.ItemObjectData, out var equipmentElement)) continue;

            var amount = Math.Min(request.Amount, GetItemAmount(sourceParty.ItemRoster, equipmentElement));
            if (amount <= 0) continue;

            offer.Items.Add(new ItemTransfer(equipmentElement, amount));
        }
    }

    private void AddTroopTransfers(AcceptedTradeOffer offer, PartyBase sourceParty, TroopRosterElementData[] offeredTroops)
    {
        var requestedTroops = BuildTroopRequests(offeredTroops);
        foreach (var request in requestedTroops.Values)
        {
            if (!objectManager.TryGetObjectWithLogging(request.Data.CharacterId, out CharacterObject character))
                continue;
            if (character == sourceParty.LeaderHero?.CharacterObject)
                continue;

            var index = sourceParty.MemberRoster.FindIndexOfTroop(character);
            if (index < 0) continue;

            var rosterElement = sourceParty.MemberRoster.GetElementCopyAtIndex(index);
            var amount = Math.Min(request.Number, rosterElement.Number);
            if (amount <= 0) continue;

            var wounded = Math.Min(amount, Math.Min(request.WoundedNumber, rosterElement.WoundedNumber));
            var xp = Math.Min(request.Xp, rosterElement.Xp);

            offer.Troops.Add(new TroopTransfer(character, amount, wounded, xp));
        }
    }

    private void AddFiefTransfers(AcceptedTradeOffer offer, PartyBase sourceParty, string[] offeredFiefs)
    {
        if (sourceParty.LeaderHero == null) return;
        if (offeredFiefs == null) return;

        var seenFiefs = new HashSet<string>();
        foreach (var fiefId in offeredFiefs)
        {
            if (string.IsNullOrEmpty(fiefId)) continue;
            if (!seenFiefs.Add(fiefId)) continue;
            if (!objectManager.TryGetObject(fiefId, out Settlement settlement)) continue;
            if (settlement.OwnerClan?.Leader != sourceParty.LeaderHero) continue;

            offer.Fiefs.Add(settlement);
        }
    }

    private void AddPrisonerTransfers(AcceptedTradeOffer offer, PartyBase sourceParty, TroopRosterElementData[] offeredPrisoners)
    {
        var requestedPrisoners = BuildTroopRequests(offeredPrisoners);
        foreach (var request in requestedPrisoners.Values)
        {
            if (!objectManager.TryGetObjectWithLogging(request.Data.CharacterId, out CharacterObject character))
                continue;

            var amount = Math.Min(request.Number, sourceParty.PrisonRoster.GetElementNumber(character));
            if (amount <= 0) continue;

            offer.Prisoners.Add(new PrisonerTransfer(character, amount));
        }
    }

    private void ApplyOffer(PartyBase sourceParty, PartyBase destinationParty, AcceptedTradeOffer offer)
    {
        ApplyGold(sourceParty, destinationParty, offer.Gold);
        ApplyItems(sourceParty, destinationParty, offer.Items);
        ApplyTroops(sourceParty, destinationParty, offer.Troops);
        ApplyPrisoners(sourceParty, destinationParty, offer.Prisoners);
        ApplyFiefs(destinationParty, offer.Fiefs);
    }

    private static void ApplyGold(PartyBase sourceParty, PartyBase destinationParty, int amount)
    {
        if (amount <= 0) return;
        if (sourceParty.LeaderHero == null || destinationParty.LeaderHero == null) return;

        GiveGoldAction.ApplyBetweenCharacters(sourceParty.LeaderHero, destinationParty.LeaderHero, amount, false);
    }

    private static void ApplyItems(PartyBase sourceParty, PartyBase destinationParty, List<ItemTransfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            sourceParty.ItemRoster.AddToCounts(transfer.EquipmentElement, -transfer.Amount);
            destinationParty.ItemRoster.AddToCounts(transfer.EquipmentElement, transfer.Amount);
        }
    }

    private static void ApplyTroops(PartyBase sourceParty, PartyBase destinationParty, List<TroopTransfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            var character = transfer.Character;
            if (character.IsHero && character.HeroObject.IsWanderer)
            {
                var companion = character.HeroObject;

                // Skip clan remove/re-add to support same two players in the same clan trading companions
                if (sourceParty.MobileParty.ActualClan != destinationParty.MobileParty.ActualClan)
                {
                    // Use ApplyAfterQuest to avoid doing extra logic for making the companion fugitive and resetting equipment
                    RemoveCompanionAction.ApplyAfterQuest(sourceParty.MobileParty.ActualClan, companion);

                    companion.CompanionOf = destinationParty.MobileParty.ActualClan;
                    CampaignEventDispatcher.Instance.OnNewCompanionAdded(companion);
                }

                AddHeroToPartyAction.Apply(companion, destinationParty.MobileParty);
                continue;
            }

            sourceParty.MemberRoster.AddToCounts(
                transfer.Character,
                -transfer.Amount,
                false,
                -transfer.WoundedNumber,
                -transfer.Xp,
                true,
                -1);
            destinationParty.MemberRoster.AddToCounts(
                transfer.Character,
                transfer.Amount,
                false,
                transfer.WoundedNumber,
                transfer.Xp,
                true,
                -1);
        }
    }

    private static void ApplyPrisoners(PartyBase sourceParty, PartyBase destinationParty, List<PrisonerTransfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            for (var i = 0; i < transfer.Amount; i++)
                TransferPrisonerAction.Apply(transfer.Character, sourceParty, destinationParty);
        }
    }

    private static void ApplyFiefs(PartyBase destinationParty, List<Settlement> fiefs)
    {
        if (destinationParty.LeaderHero == null) return;

        foreach (var fief in fiefs)
            ChangeOwnerOfSettlementAction.ApplyByBarter(destinationParty.LeaderHero, fief);
    }

    private Dictionary<string, ItemRequest> BuildItemRequests(ItemRosterElementData[] offeredItems)
    {
        var result = new Dictionary<string, ItemRequest>();
        if (offeredItems == null) return result;

        foreach (var offeredItem in offeredItems)
        {
            if (offeredItem.Amount <= 0) continue;

            var key = GetItemKey(offeredItem.ItemObjectData);
            if (result.TryGetValue(key, out var request))
            {
                result[key] = new ItemRequest(offeredItem.ItemObjectData, request.Amount + offeredItem.Amount);
            }
            else
            {
                result[key] = new ItemRequest(offeredItem.ItemObjectData, offeredItem.Amount);
            }
        }

        return result;
    }

    private Dictionary<string, TroopRequest> BuildTroopRequests(TroopRosterElementData[] offeredTroops)
    {
        var result = new Dictionary<string, TroopRequest>();
        if (offeredTroops == null) return result;

        foreach (var offeredTroop in offeredTroops)
        {
            if (offeredTroop.Number <= 0) continue;

            if (result.TryGetValue(offeredTroop.CharacterId, out var request))
            {
                result[offeredTroop.CharacterId] = new TroopRequest(
                    offeredTroop,
                    request.Number + offeredTroop.Number,
                    request.WoundedNumber + offeredTroop.WoundedNumber,
                    request.Xp + offeredTroop.Xp);
            }
            else
            {
                result[offeredTroop.CharacterId] = new TroopRequest(
                    offeredTroop,
                    offeredTroop.Number,
                    offeredTroop.WoundedNumber,
                    offeredTroop.Xp);
            }
        }

        return result;
    }

    private bool TryResolveEquipmentElement(ItemObjectData itemObjectData, out EquipmentElement equipmentElement)
    {
        equipmentElement = default;

        if (!objectManager.TryGetObject(itemObjectData.ItemObjectId, out ItemObject itemObject))
            return false;

        ItemModifier itemModifier = null;
        if (!itemObjectData.ItemModifierNull &&
            !objectManager.TryGetObject(itemObjectData.ItemModifierId, out itemModifier))
            return false;

        equipmentElement = new EquipmentElement(itemObject, itemModifier);
        return true;
    }

    private static int GetItemAmount(ItemRoster itemRoster, EquipmentElement equipmentElement)
    {
        if (itemRoster == null) return 0;

        foreach (var item in itemRoster)
        {
            if (item.EquipmentElement.Equals(equipmentElement))
                return item.Amount;
        }

        return 0;
    }

    private static string GetItemKey(ItemObjectData itemObjectData)
        => $"{itemObjectData.ItemObjectId}|{itemObjectData.ItemModifierId}|{itemObjectData.ItemModifierNull}";

    private static void RunOnGameThread(Action action, string context)
    {
        if (!GameThread.Instance.IsInitialized || GameThread.Instance.IsGameThread)
        {
            action();
            return;
        }

        GameThread.RunSafe(action, blocking: true, context: context);
    }

    private sealed class AcceptedTradeOffer
    {
        public int Gold { get; set; }
        public List<ItemTransfer> Items { get; } = new List<ItemTransfer>();
        public List<TroopTransfer> Troops { get; } = new List<TroopTransfer>();
        public List<Settlement> Fiefs { get; } = new List<Settlement>();
        public List<PrisonerTransfer> Prisoners { get; } = new List<PrisonerTransfer>();
    }

    private readonly struct ItemRequest
    {
        public readonly ItemObjectData ItemObjectData;
        public readonly int Amount;

        public ItemRequest(ItemObjectData itemObjectData, int amount)
        {
            ItemObjectData = itemObjectData;
            Amount = amount;
        }
    }

    private readonly struct TroopRequest
    {
        public readonly TroopRosterElementData Data;
        public readonly int Number;
        public readonly int WoundedNumber;
        public readonly int Xp;

        public TroopRequest(TroopRosterElementData data, int number, int woundedNumber, int xp)
        {
            Data = data;
            Number = number;
            WoundedNumber = woundedNumber;
            Xp = xp;
        }
    }

    private readonly struct ItemTransfer
    {
        public readonly EquipmentElement EquipmentElement;
        public readonly int Amount;

        public ItemTransfer(EquipmentElement equipmentElement, int amount)
        {
            EquipmentElement = equipmentElement;
            Amount = amount;
        }
    }

    private readonly struct TroopTransfer
    {
        public readonly CharacterObject Character;
        public readonly int Amount;
        public readonly int WoundedNumber;
        public readonly int Xp;

        public TroopTransfer(CharacterObject character, int amount, int woundedNumber, int xp)
        {
            Character = character;
            Amount = amount;
            WoundedNumber = woundedNumber;
            Xp = xp;
        }
    }

    private readonly struct PrisonerTransfer
    {
        public readonly CharacterObject Character;
        public readonly int Amount;

        public PrisonerTransfer(CharacterObject character, int amount)
        {
            Character = character;
            Amount = amount;
        }
    }
}
