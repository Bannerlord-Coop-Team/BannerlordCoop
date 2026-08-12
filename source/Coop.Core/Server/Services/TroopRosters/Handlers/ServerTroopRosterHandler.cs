using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.Transactions;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTroopRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;

    public ServerTroopRosterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientRequestRecruitment>(HandleOnRecruitmentDone);
    }

    private void HandleOnRecruitmentDone(MessagePayload<ClientRequestRecruitment> payload)
    {
        var data = payload.What;
        var peer = payload.Who as NetPeer;

        GameThread.Run(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.Recruit, () =>
        {
            try
            {
                string authenticationReason =
                    "The server could not authenticate this player.";
                if (!playerManager.TryGetPlayer(peer, out var player) ||
                    !ServerTransactionOutcome.TryResolvePlayer(
                        peer,
                        playerManager,
                        objectManager,
                        player?.HeroId,
                        data.MobilePartyId,
                        out _,
                        out _,
                        out _,
                        out authenticationReason))
                {
                    ServerTransactionOutcome.Reject(
                        peer,
                        ServerTransactionOutcome.Recruit,
                        authenticationReason);
                    return;
                }
                if (troopRosterInterface.TryHandleOnRecruitmentDone(
                        data.MobilePartyId,
                        data.TroopsInCart,
                        out string reason))
                    ServerTransactionOutcome.Accept(
                        peer, ServerTransactionOutcome.Recruit);
                else
                {
                    ServerTransactionOutcome.Reject(
                        peer, ServerTransactionOutcome.Recruit, reason);
                    TryPublishCurrentVolunteerSnapshot(data.MobilePartyId);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(ClientRequestRecruitment));
                ServerTransactionOutcome.Reject(
                    peer,
                    ServerTransactionOutcome.Recruit,
                    "Recruitment could not be completed.");
            }
        }));
    }

    private void TryPublishCurrentVolunteerSnapshot(string mobilePartyId)
    {
        try
        {
            if (!objectManager.TryGetObject<MobileParty>(
                    mobilePartyId, out var mobileParty) ||
                mobileParty?.CurrentSettlement?.Notables == null)
                return;

            var snapshots = new Dictionary<Hero, CharacterObject[]>();
            foreach (Hero notable in mobileParty.CurrentSettlement.Notables)
            {
                if (notable?.VolunteerTypes != null)
                    snapshots[notable] = notable.VolunteerTypes.ToArray();
            }

            if (snapshots.Count > 0)
                messageBroker.Publish(this, new VolunteersUpdated(snapshots));
        }
        catch (Exception exception)
        {
            Logger.Warning(
                exception,
                "Recruitment was rejected but the volunteer snapshot could not be refreshed");
        }
    }
}
