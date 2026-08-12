using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Party.Handlers;

internal class SellPrisonersHandler : IHandler
{
    private static readonly ILogger logger =
        LogManager.GetLogger<SellPrisonersHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleProcessor prisonerSaleProcessor;
    private readonly ISendCoalescer sendCoalescer;
    private readonly IPlayerManager playerManager;
    private static SellPrisonersHandler serverInstance;

    public SellPrisonersHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleProcessor prisonerSaleProcessor,
        IPlayerManager playerManager,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.prisonerSaleProcessor = prisonerSaleProcessor;
        this.playerManager = playerManager;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<PrisonersSold>(Handle_PrisonersSold);
        messageBroker.Subscribe<SellPrisoners>(Handle_SellPrisoners);
        if (ModInformation.IsServer)
            serverInstance = this;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonersSold>(Handle_PrisonersSold);
        messageBroker.Unsubscribe<SellPrisoners>(Handle_SellPrisoners);
        if (ReferenceEquals(serverInstance, this))
            serverInstance = null;
    }

    private void Handle_PrisonersSold(MessagePayload<PrisonersSold> obj)
    {
        if (!objectManager.TryGetIdWithLogging(
                obj.What.SellingParty, out var sellingPartyId)) return;
        network.SendAll(new SellPrisoners(
            sellingPartyId,
            troopRosterInterface.PackTroopRosterData(
                obj.What.LeftPrisonerRoster)));
    }

    private void Handle_SellPrisoners(MessagePayload<SellPrisoners> obj)
    {
        if (!ModInformation.IsServer || obj.Who is not NetPeer peer)
            return;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                player == null ||
                !objectManager.TryGetObject(
                    player.MobilePartyId, out MobileParty playerParty) ||
                !objectManager.TryGetObject(
                    obj.What.SellingPartyId, out PartyBase sellingParty) ||
                sellingParty != playerParty.Party ||
                !playerParty.IsActive || playerParty.MapEvent != null ||
                playerParty.CurrentSettlement?.Town == null)
            {
                Reject(peer,
                    "Prisoners can only be sold from your active party in a town.");
                return;
            }

            if (!TryBuildExactSale(
                    obj.What.LeftPrisonerRosterData,
                    playerParty.PrisonRoster,
                    out var requestedPrisoners,
                    out var reason))
            {
                Reject(peer, reason);
                return;
            }

            int goldBefore = playerParty.LeaderHero?.Gold ?? 0;
            TroopRosterElement[] prisonersBefore =
                playerParty.PrisonRoster.GetTroopRoster().ToArray();
            PrisonerSalePlan plan;
            try
            {
                plan = prisonerSaleProcessor.Prepare(
                    playerParty.Party, requestedPrisoners);
                prisonerSaleProcessor.ApplyCore(playerParty.Party, plan);
                if (!RegularPrisonersWereRemoved(
                        playerParty.PrisonRoster,
                        requestedPrisoners,
                        prisonersBefore))
                {
                    throw new InvalidOperationException(
                        "The authoritative prisoner sale did not commit.");
                }
            }
            catch (Exception exception)
            {
                RestoreRoster(playerParty.PrisonRoster, prisonersBefore);
                if (playerParty.LeaderHero != null)
                    playerParty.LeaderHero.Gold = goldBefore;
                logger.Error(exception,
                    "Rolled back prisoner sale for {PartyId}",
                    player.MobilePartyId);
                Reject(peer,
                    "The prisoner sale could not be committed safely. Please try again.");
                return;
            }

            try
            {
                prisonerSaleProcessor.PublishPostCommit(plan);
            }
            catch (Exception exception)
            {
                // The roster/gold commit is final. A later captivity notification
                // must never turn the same sale into a retryable transaction.
                logger.Error(exception,
                    "Post-commit prisoner release failed for {PartyId}",
                    player.MobilePartyId);
            }
            NotifySaleCommitted(peer, playerParty);
        });
    }

    internal static PrisonerSalePlan ApplySale(
        MobileParty expectedParty,
        TroopRoster requestedPrisoners)
    {
        SellPrisonersHandler current = serverInstance ??
            throw new InvalidOperationException(
                "The prisoner sale service is unavailable.");
        PrisonerSalePlan plan = current.prisonerSaleProcessor.Prepare(
            expectedParty.Party, requestedPrisoners);
        current.prisonerSaleProcessor.ApplyCore(expectedParty.Party, plan);
        return plan;
    }

    internal static void PublishSalePostCommit(PrisonerSalePlan plan) =>
        serverInstance?.prisonerSaleProcessor.PublishPostCommit(plan);

    internal static void NotifySaleCommitted(
        NetPeer peer,
        MobileParty expectedParty)
    {
        SellPrisonersHandler current = serverInstance;
        if (current == null || peer == null || expectedParty == null)
            return;
        current.objectManager.TryGetId(
            expectedParty.PrisonRoster, out var rosterId);
        current.sendCoalescer?.FlushInstance(
            Compact(rosterId, typeof(TroopRoster)), current.network);
        if (current.objectManager.TryGetId(
                expectedParty.LeaderHero, out var heroId))
            current.network.Send(
                peer, new RefreshGameMenu(heroId, "town_backstreet"));
    }

    private bool TryBuildExactSale(
        TroopRosterData data,
        TroopRoster available,
        out TroopRoster requested,
        out string reason)
    {
        requested = new TroopRoster();
        reason =
            "The selected prisoners no longer match the server roster.";
        var rows = data.Data ?? Array.Empty<TroopRosterElementData>();
        if (rows.Length == 0 || rows.Any(row =>
                string.IsNullOrEmpty(row.CharacterId) || row.Number <= 0 ||
                row.WoundedNumber < 0 ||
                row.WoundedNumber > row.Number || row.Xp < 0) ||
            rows.Select(row => row.CharacterId)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length)
            return false;

        foreach (var row in rows)
        {
            if (!objectManager.TryGetObject(
                    row.CharacterId, out CharacterObject character) ||
                character == null)
                return false;
            int index = available.FindIndexOfTroop(character);
            if (index < 0) return false;
            TroopRosterElement current =
                available.GetElementCopyAtIndex(index);
            int requestedHealthy = row.Number - row.WoundedNumber;
            int availableHealthy = current.Number - current.WoundedNumber;
            if (row.Number > current.Number ||
                row.WoundedNumber > current.WoundedNumber ||
                requestedHealthy > availableHealthy)
                return false;
            requested.AddToCounts(
                character, row.Number, false, row.WoundedNumber, 0, true);
        }
        return true;
    }

    private bool RegularPrisonersWereRemoved(
        TroopRoster current,
        TroopRoster requested,
        IEnumerable<TroopRosterElement> before)
    {
        var beforeByCharacter = before.ToDictionary(
            element => element.Character, element => element);
        foreach (var sold in requested.GetTroopRoster())
        {
            if (sold.Character?.HeroObject != null)
                continue;
            if (!beforeByCharacter.TryGetValue(
                    sold.Character, out var old)) return false;
            int index = current.FindIndexOfTroop(sold.Character);
            TroopRosterElement now = index < 0
                ? default
                : current.GetElementCopyAtIndex(index);
            if (now.Number != old.Number - sold.Number ||
                now.WoundedNumber !=
                    old.WoundedNumber - sold.WoundedNumber)
                return false;
        }
        return true;
    }

    private static void RestoreRoster(
        TroopRoster roster,
        IEnumerable<TroopRosterElement> snapshot)
    {
        roster.Clear();
        foreach (var element in snapshot)
            roster.AddToCounts(
                element.Character, element.Number, false,
                element.WoundedNumber, element.Xp, true);
    }

    private void Reject(NetPeer peer, string reason)
    {
        if (peer != null)
            network.Send(peer, new SendInformationMessage(reason));
    }

}
