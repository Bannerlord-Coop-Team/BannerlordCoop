using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Villages.Handlers;

internal class ServerVillageHostileActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerVillageHostileActionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly IRaidAiInterventionConfigInterface raidAiInterventionConfigInterface;

    public ServerVillageHostileActionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        IVillageHostileActionInterface villageHostileActionInterface,
        IRaidAiInterventionConfigInterface raidAiInterventionConfigInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.raidAiInterventionConfigInterface = raidAiInterventionConfigInterface;

        messageBroker.Subscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Subscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Unsubscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_NetworkRequestVillageHostileAction(MessagePayload<NetworkRequestVillageHostileAction> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestVillageHostileAction));
            return;
        }

        var request = payload.What;
        if (!TryValidateRequester(request.ControllerId, request.MobilePartyId))
        {
            Deny(peer, VillageHostileActionDeniedReason.InvalidRequester);
            return;
        }

        GameThread.RunSafe(() => TryStartHostileAction(peer, request), blocking: true, context: nameof(Handle_NetworkRequestVillageHostileAction));
    }

    private bool TryValidateRequester(string controllerId, string mobilePartyId)
    {
        if (string.IsNullOrWhiteSpace(controllerId))
            return false;

        if (!playerManager.TryGetPlayer(controllerId, out var player))
            return false;

        return player.MobilePartyId == mobilePartyId;
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
                KickOtherPlayersOutOfVillage(request.ControllerId, mobileParty, settlement);

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

    private void KickOtherPlayersOutOfVillage(string requestingControllerId, MobileParty raidingParty, Settlement settlement)
    {
        foreach (var player in playerManager.Players)
        {
            if (player.ControllerId == requestingControllerId)
                continue;

            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                continue;

            if (playerParty == raidingParty || playerParty.CurrentSettlement != settlement)
                continue;

            network.SendAll(new NetworkEndSettlementEncounter(player.MobilePartyId));
            network.SendAll(new NetworkPartyLeaveSettlement(player.MobilePartyId));
            settlementInterface.PartyLeaveSettlement(playerParty);
        }
    }

    private void Deny(NetPeer peer, VillageHostileActionDeniedReason reason)
    {
        network.Send(peer, new NetworkVillageHostileActionDenied(reason));
    }

    private void Handle_VillageHostileActionCooldownsChanged(MessagePayload<VillageHostileActionCooldownsChanged> payload)
    {
        if (ModInformation.IsClient) return;

        network.SendAll(new NetworkVillageHostileActionCooldowns(payload.What.Cooldowns ?? Array.Empty<VillageHostileActionCooldownData>()));
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(
            () => SendJoinSnapshots(payload.What.playerId),
            blocking: true,
            context: nameof(Handle_PlayerCampaignEntered));
    }

    private void SendJoinSnapshots(NetPeer peer)
    {
        SendCooldownSnapshot(peer);
        SendRaidAiInterventionConfigSnapshot(peer);
    }

    private void SendCooldownSnapshot(NetPeer peer)
    {
        var cooldowns = villageHostileActionInterface.GetActiveCooldowns();
        if (cooldowns.Length == 0)
            return;

        network.Send(peer, new NetworkVillageHostileActionCooldowns(cooldowns));
    }

    private void SendRaidAiInterventionConfigSnapshot(NetPeer peer)
    {
        raidAiInterventionConfigInterface.SendSnapshot(peer);
    }
}
