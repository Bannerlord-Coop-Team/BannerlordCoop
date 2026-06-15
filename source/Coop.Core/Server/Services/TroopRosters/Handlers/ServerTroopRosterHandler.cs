using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.TroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System;

namespace Coop.Core.Server.Services.TroopRosters.Handlers;
internal class ServerTroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTroopRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ITroopRosterInterface troopRosterInterface;

    public ServerTroopRosterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;

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

        // Recruitment runs vanilla roster/gold code and the result must replicate to clients;
        // both must run on the main thread, not the network thread that delivered the request.
        // The server replies with the gold change only after it has applied the recruitment.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                // The TroopRoster AddToCounts patch publishes its sync messages while inside an
                // AllowedThread, so the recruited troops only replicate to clients when the apply
                // runs inside this scope.
                using (new AllowedThread())
                {
                    troopRosterInterface.HandleOnRecruitmentDone(data.MobilePartyId, data.TroopsInCart, out var changedGold);

                    network.Send(peer, new NotifyGoldChange(changedGold));
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(ClientRequestRecruitment));
            }
        });
    }
}