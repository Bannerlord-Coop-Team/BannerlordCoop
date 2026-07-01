using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Villages.Handlers;

internal class ServerVillageHostileActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerVillageHostileActionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IConnectionCollection connections;
    private readonly IPlayerManager playerManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly ConcurrentDictionary<NetPeer, string> playerIdsByPeer = new ConcurrentDictionary<NetPeer, string>(NetPeerReferenceComparer.Instance);

    public ServerVillageHostileActionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IConnectionCollection connections,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        IVillageHostileActionInterface villageHostileActionInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.connections = connections;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.villageHostileActionInterface = villageHostileActionInterface;

        messageBroker.Subscribe<NetworkClientValidate>(Handle_NetworkClientValidate);
        messageBroker.Subscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Subscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientValidate>(Handle_NetworkClientValidate);
        messageBroker.Unsubscribe<NetworkTransferNewHero>(Handle_NetworkTransferNewHero);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Unsubscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_NetworkClientValidate(MessagePayload<NetworkClientValidate> payload)
    {
        TrackPeerPlayer(payload.Who as NetPeer, payload.What.PlayerId);
    }

    private void Handle_NetworkTransferNewHero(MessagePayload<NetworkTransferNewHero> payload)
    {
        TrackPeerPlayer(payload.Who as NetPeer, payload.What.PlayerId);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        playerIdsByPeer.TryRemove(payload.What.PlayerId, out _);
    }

    private void TrackPeerPlayer(NetPeer peer, string playerId)
    {
        if (peer == null || string.IsNullOrWhiteSpace(playerId))
            return;

        playerIdsByPeer[peer] = playerId;
    }

    private void Handle_NetworkRequestVillageHostileAction(MessagePayload<NetworkRequestVillageHostileAction> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestVillageHostileAction));
            return;
        }

        var request = payload.What;
        if (!TryValidateRequester(peer, request.MobilePartyId))
        {
            Deny(peer, VillageHostileActionDeniedReason.InvalidRequester);
            return;
        }

        GameThread.RunSafe(() => TryStartHostileAction(peer, request), blocking: true, context: nameof(Handle_NetworkRequestVillageHostileAction));
    }

    private bool TryValidateRequester(NetPeer peer, string mobilePartyId)
    {
        if (!TryGetPeerPlayer(peer, out var player))
            return false;

        return player.MobilePartyId == mobilePartyId;
    }

    private bool TryGetPeerPlayer(NetPeer peer, out Player player)
    {
        player = null;

        if (peer == null)
            return false;

        if (!playerIdsByPeer.TryGetValue(peer, out var playerId))
            return false;

        return playerManager.TryGetPlayer(playerId, out player);
    }

    private void TryStartHostileAction(NetPeer peer, NetworkRequestVillageHostileAction request)
    {
        try
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(request.MobilePartyId, out var mobileParty))
            {
                Deny(peer, VillageHostileActionDeniedReason.InvalidRequester);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement))
            {
                Deny(peer, VillageHostileActionDeniedReason.NonVillageSettlement);
                return;
            }

            if (!villageHostileActionInterface.CanStartHostileAction(mobileParty, settlement, request.Action, out var reason))
            {
                Deny(peer, reason);
                return;
            }

            if (request.Action == VillageHostileAction.Raid)
                KickOtherPlayersOutOfVillage(peer, mobileParty, settlement);

            villageHostileActionInterface.ApplyHostileAction(mobileParty, settlement, request.Action);
            villageHostileActionInterface.ApproveMapEventStart(mobileParty.Party, settlement, request.Action);

            network.Send(peer, new NetworkVillageHostileActionStarted(request.Action, request.MobilePartyId, request.SettlementId));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to start village hostile action");
            Deny(peer, VillageHostileActionDeniedReason.Invalid);
        }
    }

    private void KickOtherPlayersOutOfVillage(NetPeer requestingPeer, MobileParty raidingParty, Settlement settlement)
    {
        foreach (var connection in connections)
        {
            var peer = connection.Peer;
            if (peer == requestingPeer)
                continue;

            if (!TryGetPeerPlayer(peer, out var player))
                continue;

            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                continue;

            if (playerParty == raidingParty || playerParty.CurrentSettlement != settlement)
                continue;

            network.Send(peer, new NetworkEndSettlementEncounter());
            network.SendAllBut(peer, new NetworkPartyLeaveSettlement(player.MobilePartyId));
            settlementInterface.PartyLeaveSettlement(playerParty);
        }
    }

    private void Deny(NetPeer peer, VillageHostileActionDeniedReason reason)
    {
        network.Send(peer, new NetworkVillageHostileActionDenied(reason));
    }

    private void Handle_VillageHostileActionCooldownsChanged(MessagePayload<VillageHostileActionCooldownsChanged> payload)
    {
        if (!ModInformation.IsServer) return;

        network.SendAll(new NetworkVillageHostileActionCooldowns(payload.What.Cooldowns ?? Array.Empty<VillageHostileActionCooldownData>()));
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (!ModInformation.IsServer) return;

        GameThread.RunSafe(
            () => SendCooldownSnapshot(payload.What.playerId),
            blocking: true,
            context: nameof(Handle_PlayerCampaignEntered));
    }

    private void SendCooldownSnapshot(NetPeer peer)
    {
        var cooldowns = villageHostileActionInterface.GetActiveCooldowns();
        if (cooldowns.Length == 0)
            return;

        network.Send(peer, new NetworkVillageHostileActionCooldowns(cooldowns));
    }

    private sealed class NetPeerReferenceComparer : IEqualityComparer<NetPeer>
    {
        public static readonly NetPeerReferenceComparer Instance = new NetPeerReferenceComparer();

        public bool Equals(NetPeer x, NetPeer y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(NetPeer obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
