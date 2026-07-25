using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Bandits.Patches;
using GameInterface.Services.Barters;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Bandits.Handlers;

internal sealed class BanditBarterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditBarterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly ISessionInteractionsPlayerDataInterface interactions;
    private readonly IBarterClientPresentation barterClientPresentation;
    private readonly ISendCoalescer sendCoalescer;

    public BanditBarterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ConversationPartyTracker conversationPartyTracker,
        ISessionInteractionsPlayerDataInterface interactions,
        IBarterClientPresentation barterClientPresentation,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.conversationPartyTracker = conversationPartyTracker;
        this.interactions = interactions;
        this.barterClientPresentation = barterClientPresentation;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<NetworkRequestBanditBarter>(HandleRequest);
        messageBroker.Subscribe<NetworkBanditBarterResult>(HandleResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBanditBarter>(HandleRequest);
        messageBroker.Unsubscribe<NetworkBanditBarterResult>(HandleResult);
        BanditBarterPatch.ClearPendingRequest();
    }

    private void HandleRequest(MessagePayload<NetworkRequestBanditBarter> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received bandit safe-passage request without an originating peer");
            return;
        }

        var request = payload.What;
        GameThread.RunSafe(
            () => ProcessRequest(peer, request),
            context: nameof(BanditBarterHandler));
    }

    private void HandleResult(MessagePayload<NetworkBanditBarterResult> payload)
    {
        if (ModInformation.IsServer) return;

        var result = payload.What;
        GameThread.RunSafe(
            () => BanditBarterPatch.CompleteRequest(result, barterClientPresentation),
            context: nameof(NetworkBanditBarterResult));
    }

    private void ProcessRequest(NetPeer peer, NetworkRequestBanditBarter request)
    {
        try
        {
            if (!TryResolveParties(peer, request.BanditPartyId, out var player, out var playerHero, out var playerParty, out var banditParty, out var reason) ||
                !HasActiveEngagement(peer, playerParty, banditParty, out reason) ||
                !TryValidateOffer(request, playerHero, playerParty, banditParty, out var offer, out reason))
            {
                Reject(peer, request, playerHero?.Gold ?? 0, reason);
                return;
            }

            ApplyOffer(playerHero, playerParty.Party, banditParty.Party, offer);

            var protectionUntil = CampaignTime.HoursFromNow(32);
            foreach (var protectedParty in offer.EnemyParties)
            {
                DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                    protectedParty,
                    playerParty,
                    protectionUntil);
                protectedParty.SetMoveModeHold();
                protectedParty.Ai.SetInitiative(0f, 0.8f, 8f);
            }

            interactions.AddPlayerKeys(player.HeroId);
            interactions.SetPlayerBanditsInteraction(
                player.HeroId,
                request.BanditPartyId,
                BanditInteractionsCampaignBehavior.PlayerInteraction.PaidOffParty);

            ConversationPartyHold.EndEngagement(conversationPartyTracker, peer);
            FlushHeroGold(playerHero);
            network.Send(peer, new NetworkBanditBarterResult(
                request.BanditPartyId,
                true,
                playerHero.Gold,
                requestId: request.RequestId));
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to apply an authoritative bandit safe-passage barter");
            Reject(peer, request, 0, "The server could not process the bandit barter.");
        }
    }

    private bool TryResolveParties(
        NetPeer peer,
        string banditPartyId,
        out Player player,
        out Hero playerHero,
        out MobileParty playerParty,
        out MobileParty banditParty,
        out string reason)
    {
        player = null;
        playerHero = null;
        playerParty = null;
        banditParty = null;
        reason = null;

        if (!playerManager.TryGetPlayer(peer, out player) ||
            !objectManager.TryGetObject(player.HeroId, out playerHero) ||
            !objectManager.TryGetObject(player.MobilePartyId, out playerParty))
        {
            reason = "The server could not identify your party.";
            return false;
        }

        if (!objectManager.TryGetObject(banditPartyId, out banditParty) ||
            banditParty?.IsActive != true ||
            !banditParty.IsBandit)
        {
            reason = "The bandit party is no longer available.";
            return false;
        }

        if (playerParty?.IsActive != true || playerParty.LeaderHero != playerHero ||
            playerParty.MapEvent != null || banditParty.MapEvent != null)
        {
            reason = "The encounter state changed before the barter completed.";
            return false;
        }

        return true;
    }

    private bool HasActiveEngagement(
        NetPeer peer,
        MobileParty playerParty,
        MobileParty banditParty,
        out string reason)
    {
        reason = null;
        if (!objectManager.TryGetId(playerParty.Party, out var playerPartyId) ||
            !objectManager.TryGetId(banditParty.Party, out var banditPartyId) ||
            !conversationPartyTracker.TryGetEngagement(peer, out var engagement) ||
            engagement.PartyId != banditPartyId ||
            engagement.EngagerPartyId != playerPartyId ||
            !engagement.EngagerIsDefender)
        {
            reason = "The bandit encounter is no longer active.";
            return false;
        }

        return true;
    }

    private static void GetSafePassageParties(
        MobileParty playerParty,
        MobileParty paidBandit,
        out List<MobileParty> playerSide,
        out List<MobileParty> enemySide)
    {
        playerSide = new List<MobileParty>();
        enemySide = new List<MobileParty>();
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        var playerPosition = playerParty.Position.ToVec2();
        var nearbyParties = MobileParty.StartFindingLocatablesAroundPosition(playerPosition, radius);

        for (var party = MobileParty.FindNextLocatable(ref nearbyParties);
             party != null;
             party = MobileParty.FindNextLocatable(ref nearbyParties))
        {
            if (party == playerParty ||
                party.IsActive != true ||
                party.MapEvent != null ||
                party.SiegeEvent != null ||
                party.CurrentSettlement != null ||
                party.AttachedTo != null ||
                party.IsInRaftState ||
                party.IsCurrentlyAtSea != playerParty.IsCurrentlyAtSea)
            {
                continue;
            }

            if (!party.IsLordParty &&
                !party.IsBandit &&
                !party.IsPatrolParty &&
                !party.ShouldJoinPlayerBattles)
            {
                continue;
            }

            var partyFaction = party.MapFaction;
            var playerFaction = playerParty.MapFaction;
            var enemyFaction = paidBandit.MapFaction;
            if (partyFaction == null || playerFaction == null || enemyFaction == null)
                continue;

            if (!partyFaction.IsAtWarWith(playerFaction) &&
                partyFaction.IsAtWarWith(enemyFaction) &&
                enemySide.All(enemy => enemy.MapFaction?.IsAtWarWith(partyFaction) == true))
            {
                playerSide.Add(party);
            }

            if (partyFaction.IsAtWarWith(playerFaction) &&
                !partyFaction.IsAtWarWith(enemyFaction) &&
                playerSide.All(ally => ally.MapFaction?.IsAtWarWith(partyFaction) == true))
            {
                enemySide.Add(party);
            }
        }

        if (enemySide.Any(party => party.ShouldBeIgnored))
            playerSide.Clear();
        if (playerSide.Any(party => party.ShouldBeIgnored))
            enemySide.Clear();

        if (!playerSide.Contains(playerParty))
            playerSide.Add(playerParty);
        if (!enemySide.Contains(paidBandit))
            enemySide.Add(paidBandit);

        foreach (var party in playerSide.ToArray())
            AddPartyAndAttachments(playerSide, party);
        foreach (var party in enemySide.ToArray())
            AddPartyAndAttachments(enemySide, party);
    }

    private static void AddPartyAndAttachments(ICollection<MobileParty> parties, MobileParty party)
    {
        if (party == null) return;
        if (!parties.Contains(party))
            parties.Add(party);

        if (party.AttachedParties == null) return;
        foreach (var attachedParty in party.AttachedParties)
        {
            if (attachedParty?.IsActive == true && !parties.Contains(attachedParty))
                parties.Add(attachedParty);
        }
    }

    private bool TryValidateOffer(
        NetworkRequestBanditBarter request,
        Hero playerHero,
        MobileParty playerParty,
        MobileParty banditParty,
        out ValidatedOffer offer,
        out string reason)
    {
        offer = null;
        reason = null;

        if (string.IsNullOrEmpty(request.RequestId))
        {
            reason = "The bandit barter request is no longer valid.";
            return false;
        }

        if (request.PlayerGold < 0 || request.PlayerGold > playerHero.Gold)
        {
            reason = "The gold in the bandit barter is no longer available.";
            return false;
        }

        if (!TryResolveItems(request.PlayerItems, playerParty.ItemRoster, out var playerItems) ||
            !TryResolvePrisoners(
                request.PlayerPrisoners,
                playerParty.PrisonRoster,
                banditParty,
                out var playerPrisoners))
        {
            reason = "An item or prisoner in the bandit barter is no longer available.";
            return false;
        }

        if (request.PlayerGold == 0 && playerItems.Count == 0 && playerPrisoners.Count == 0)
        {
            reason = "The safe-passage offer contains no payment.";
            return false;
        }

        GetSafePassageParties(playerParty, banditParty, out var playerSide, out var enemySide);
        offer = new ValidatedOffer(
            request.PlayerGold,
            playerItems,
            playerPrisoners,
            enemySide);

        var offeredValue = GetOfferValueForBandits(playerHero, playerParty, banditParty, offer);
        var requiredValue = GetRequiredSafePassageValue(
            playerHero,
            playerParty,
            banditParty,
            playerSide,
            enemySide);
        if (offeredValue < requiredValue)
        {
            offer = null;
            reason = "The bandits will not accept such a small payment.";
            return false;
        }

        return true;
    }

    private static int GetOfferValueForBandits(
        Hero playerHero,
        MobileParty playerParty,
        MobileParty banditParty,
        ValidatedOffer offer)
    {
        long value = offer.PlayerGold;
        foreach (var item in offer.PlayerItems)
        {
            var averageValue = GetAverageNearbySettlementValue(item.EquipmentElement, playerParty);
            var barterable = new ItemBarterable(
                playerHero,
                null,
                playerParty.Party,
                banditParty.Party,
                new ItemRosterElement(item.EquipmentElement, item.Amount),
                averageValue);
            barterable.CurrentAmount = item.Amount;
            value += barterable.GetValueForFaction(banditParty.MapFaction);
        }

        foreach (var prisoner in offer.PlayerPrisoners)
            value += Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(prisoner);

        return (int)MathF.Clamp(value, 0L, int.MaxValue);
    }

    private static int GetAverageNearbySettlementValue(
        EquipmentElement equipmentElement,
        MobileParty playerParty)
    {
        var nearbyTowns = Campaign.Current.AllTowns
            .OrderBy(town => town.Settlement.Position.ToVec2().DistanceSquared(playerParty.Position.ToVec2()))
            .Take(3)
            .ToArray();
        if (nearbyTowns.Length == 0)
            return equipmentElement.GetBaseValue();

        long total = 0;
        foreach (var town in nearbyTowns)
            total += town.GetItemPrice(equipmentElement, playerParty, true);
        return (int)MathF.Clamp(total / nearbyTowns.Length, 0L, int.MaxValue);
    }

    private static int GetRequiredSafePassageValue(
        Hero playerHero,
        MobileParty playerParty,
        MobileParty banditParty,
        IEnumerable<MobileParty> playerSide,
        IEnumerable<MobileParty> enemySide)
    {
        var strengthContext = playerParty.IsCurrentlyAtSea
            ? MapEvent.PowerCalculationContext.SeaBattle
            : MapEvent.PowerCalculationContext.PlainBattle;
        var playerStrength = playerSide.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Defender, strengthContext));
        var enemyStrength = enemySide.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Attacker, strengthContext));
        if (enemyStrength <= 0f)
            enemyStrength = 0.00001f;

        var strengthRatio = MathF.Clamp(playerStrength / enemyStrength, 0f, 1f);
        long totalWealth = playerHero.Gold;
        foreach (var item in playerParty.ItemRoster)
        {
            totalWealth += (long)item.EquipmentElement.Item.Value * item.Amount;
            if (totalWealth >= int.MaxValue)
            {
                totalWealth = int.MaxValue;
                break;
            }
        }
        var wealth = (float)Math.Max(0L, totalWealth);

        var wealthFactor = strengthRatio < 1f
            ? 0.05f + ((1f - strengthRatio) * 0.2f)
            : 0.1f;
        if (playerParty.MapEvent != null || playerParty.SiegeEvent != null)
            wealthFactor *= 1.2f;

        var relationFactor = banditParty.MapFaction?.Leader == null
            ? 1f
            : MathF.Clamp(
                (50f + banditParty.MapFaction.Leader.GetRelation(playerHero)) / 50f,
                0.05f,
                1.1f);
        var price = (int)((wealth * wealthFactor) + 1000f) / 8;
        if (playerHero.GetPerkValue(DefaultPerks.Roguery.SweetTalker) && !playerParty.IsCurrentlyAtSea)
            price += MathF.Round(price * DefaultPerks.Roguery.SweetTalker.PrimaryBonus);
        if (playerHero.GetPerkValue(DefaultPerks.Trade.MarketDealer))
            price += MathF.Round(price * DefaultPerks.Trade.MarketDealer.PrimaryBonus);

        return (int)(price / (relationFactor * relationFactor));
    }

    private bool TryResolveItems(
        ItemRosterElementData[] requestedItems,
        ItemRoster sourceRoster,
        out List<ItemTransfer> transfers)
    {
        transfers = new List<ItemTransfer>();
        var totals = new Dictionary<EquipmentElement, int>();

        foreach (var requestedItem in requestedItems ?? Array.Empty<ItemRosterElementData>())
        {
            if (requestedItem.Amount <= 0 || !TryResolveEquipmentElement(requestedItem.ItemObjectData, out var equipmentElement))
                return false;

            totals.TryGetValue(equipmentElement, out var current);
            var total = (long)current + requestedItem.Amount;
            if (total > int.MaxValue) return false;
            totals[equipmentElement] = (int)total;
        }

        foreach (var item in totals)
        {
            if (item.Key.GetBaseValue() <= 100 || GetItemAmount(sourceRoster, item.Key) < item.Value)
                return false;

            transfers.Add(new ItemTransfer(item.Key, item.Value));
        }

        return true;
    }

    private bool TryResolvePrisoners(
        TroopRosterElementData[] requestedPrisoners,
        TroopRoster sourceRoster,
        MobileParty banditParty,
        out List<CharacterObject> prisoners)
    {
        prisoners = new List<CharacterObject>();
        var seen = new HashSet<CharacterObject>();

        foreach (var requestedPrisoner in requestedPrisoners ?? Array.Empty<TroopRosterElementData>())
        {
            if (requestedPrisoner.Number != 1 ||
                !objectManager.TryGetObject(requestedPrisoner.CharacterId, out CharacterObject prisoner) ||
                 !prisoner.IsHero ||
                 !seen.Add(prisoner) ||
                 sourceRoster.GetElementNumber(prisoner) < 1 ||
                 prisoner.HeroObject.MapFaction == null ||
                 banditParty.MapFaction == null ||
                 !FactionManager.IsAtWarAgainstFaction(prisoner.HeroObject.MapFaction, banditParty.MapFaction))
            {
                return false;
            }

            prisoners.Add(prisoner);
        }

        return true;
    }

    private bool TryResolveEquipmentElement(ItemObjectData itemData, out EquipmentElement equipmentElement)
    {
        equipmentElement = default;
        if (!objectManager.TryGetObject(itemData.ItemObjectId, out ItemObject itemObject))
            return false;

        ItemModifier modifier = null;
        if (!itemData.ItemModifierNull && !objectManager.TryGetObject(itemData.ItemModifierId, out modifier))
            return false;

        equipmentElement = new EquipmentElement(itemObject, modifier);
        return true;
    }

    private static int GetItemAmount(ItemRoster roster, EquipmentElement equipmentElement)
    {
        foreach (var item in roster)
        {
            if (item.EquipmentElement.Equals(equipmentElement))
                return item.Amount;
        }

        return 0;
    }

    private static void ApplyOffer(Hero playerHero, PartyBase playerParty, PartyBase banditParty, ValidatedOffer offer)
    {
        if (offer.PlayerGold > 0)
            GiveGoldAction.ApplyForCharacterToParty(playerHero, banditParty, offer.PlayerGold, false);

        ApplyItems(playerParty, banditParty, offer.PlayerItems);
        ApplyPrisoners(playerParty, banditParty, offer.PlayerPrisoners);
    }

    private static void ApplyItems(PartyBase source, PartyBase destination, IEnumerable<ItemTransfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            source.ItemRoster.AddToCounts(transfer.EquipmentElement, -transfer.Amount);
            destination.ItemRoster.AddToCounts(transfer.EquipmentElement, transfer.Amount);
        }
    }

    private static void ApplyPrisoners(PartyBase source, PartyBase destination, IEnumerable<CharacterObject> prisoners)
    {
        foreach (var prisoner in prisoners)
            TransferPrisonerAction.Apply(prisoner, source, destination);
    }

    private void FlushHeroGold(Hero hero)
    {
        if (sendCoalescer == null || !objectManager.TryGetId(hero, out var heroId)) return;

        sendCoalescer.FlushInstance(heroId, network);
    }

    private void Reject(NetPeer peer, NetworkRequestBanditBarter request, int playerGold, string reason)
    {
        Logger.Warning("Rejected bandit barter for {BanditPartyId}: {Reason}", request.BanditPartyId, reason);
        network.Send(peer, new NetworkBanditBarterResult(
            request.BanditPartyId,
            false,
            playerGold,
            reason,
            request.RequestId));
    }

    private sealed class ValidatedOffer
    {
        public int PlayerGold { get; }
        public List<ItemTransfer> PlayerItems { get; }
        public List<CharacterObject> PlayerPrisoners { get; }
        public List<MobileParty> EnemyParties { get; }

        public ValidatedOffer(
            int playerGold,
            List<ItemTransfer> playerItems,
            List<CharacterObject> playerPrisoners,
            List<MobileParty> enemyParties)
        {
            PlayerGold = playerGold;
            PlayerItems = playerItems;
            PlayerPrisoners = playerPrisoners;
            EnemyParties = enemyParties;
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
}
