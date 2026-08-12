using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Party.Handlers;

internal class SellPrisonersHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleProcessor prisonerSaleProcessor;
    private readonly ISendCoalescer sendCoalescer;
    private readonly IPlayerManager playerManager;
    private readonly object pendingGate = new();
    private readonly Dictionary<NetPeer, PendingSale> pendingSales = new();
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
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        if (ModInformation.IsServer)
            serverInstance = this;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonersSold>(Handle_PrisonersSold);
        messageBroker.Unsubscribe<SellPrisoners>(Handle_SellPrisoners);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        lock (pendingGate)
            pendingSales.Clear();
        if (ReferenceEquals(serverInstance, this))
            serverInstance = null;
    }

    private void Handle_PrisonersSold(MessagePayload<PrisonersSold> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.SellingParty, out var sellingPartyId)) return;

        var packedData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonerRoster);

        var message = new SellPrisoners(sellingPartyId, packedData);
        network.SendAll(message);
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
                !objectManager.TryGetObjectWithLogging<PartyBase>(
                    obj.What.SellingPartyId, out var sellingParty) ||
                sellingParty != playerParty.Party ||
                !playerParty.IsActive || playerParty.MapEvent != null ||
                playerParty.CurrentSettlement?.Town == null)
                return;

            lock (pendingGate)
                pendingSales[peer] = new PendingSale(
                    playerParty,
                    obj.What.LeftPrisonerRosterData,
                    DateTime.UtcNow.AddSeconds(30));
        });
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        if (!ModInformation.IsServer) return;
        lock (pendingGate)
            pendingSales.Remove(obj.What.PlayerId);
    }

    internal static bool TryGetPendingSale(
        NetPeer peer,
        MobileParty expectedParty,
        out TroopRoster requestedPrisoners)
    {
        requestedPrisoners = null;
        SellPrisonersHandler current = serverInstance;
        if (current == null || peer == null || expectedParty == null)
            return false;
        PendingSale pending;
        lock (current.pendingGate)
        {
            if (!current.pendingSales.TryGetValue(peer, out pending) ||
                pending.ExpiresUtc < DateTime.UtcNow ||
                pending.Party != expectedParty)
            {
                current.pendingSales.Remove(peer);
                return false;
            }
        }

        requestedPrisoners = new TroopRoster();
        current.troopRosterInterface.UpdateWithData(
            requestedPrisoners,
            pending.RosterData,
            expectedParty.LeaderHero);
        return true;
    }

    internal static void ApplySale(
        MobileParty expectedParty,
        TroopRoster requestedPrisoners)
    {
        SellPrisonersHandler current = serverInstance ??
            throw new InvalidOperationException(
                "The prisoner sale service is unavailable.");
        current.prisonerSaleProcessor.Sell(
            expectedParty.Party, requestedPrisoners);
    }

    internal static void NotifySaleCommitted(
        NetPeer peer,
        MobileParty expectedParty)
    {
        SellPrisonersHandler current = serverInstance;
        if (current == null || peer == null || expectedParty == null)
            return;
        current.objectManager.TryGetId(
            expectedParty.PrisonRoster, out var rosterId);
        var compactId = Compact(rosterId, typeof(TroopRoster));
        current.sendCoalescer?.FlushInstance(compactId, current.network);
        if (current.objectManager.TryGetId(
                expectedParty.LeaderHero, out var heroId))
            current.network.Send(
                peer, new RefreshGameMenu(heroId, "town_backstreet"));
    }

    internal static void ClearPendingSale(NetPeer peer)
    {
        SellPrisonersHandler current = serverInstance;
        if (current == null || peer == null) return;
        lock (current.pendingGate)
            current.pendingSales.Remove(peer);
    }

    private sealed class PendingSale
    {
        internal readonly MobileParty Party;
        internal readonly TroopRosterData RosterData;
        internal readonly DateTime ExpiresUtc;

        internal PendingSale(
            MobileParty party,
            TroopRosterData rosterData,
            DateTime expiresUtc)
        {
            Party = party;
            RosterData = rosterData;
            ExpiresUtc = expiresUtc;
        }
    }
}
