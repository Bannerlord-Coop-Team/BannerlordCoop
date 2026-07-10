using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Sends every server-simulated party's CURRENT behavior to a client entering the
/// campaign. MobileParty.DefaultBehavior is not a saveable property (the transferred
/// save carries no behaviors) and the live broadcast only fires on behavior CHANGES,
/// so without this snapshot any party whose behavior stays stable after the join —
/// hideout bandits above all — shows "Unknown Behavior" and never moves on that
/// client.
/// </summary>
internal class ServerPartyBehaviorSnapshotHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerPartyBehaviorSnapshotHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IMobilePartyInterface mobilePartyInterface;

    public ServerPartyBehaviorSnapshotHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IMobilePartyInterface mobilePartyInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.mobilePartyInterface = mobilePartyInterface;

        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        NetPeer peer = payload.What.playerId;
        if (peer == null) return;

        // The snapshot reads live campaign state; build it on the game thread.
        GameThread.RunSafe(() =>
        {
            var snapshot = mobilePartyInterface.GetBehaviorSnapshot();
            foreach (var data in snapshot)
            {
                network.Send(peer, new NetworkUpdatePartyBehavior(data));
            }
            Logger.Information("Sent {Count} party behavior snapshots to joining peer {Peer}",
                snapshot.Count, peer.Id);
        }, context: nameof(ServerPartyBehaviorSnapshotHandler));
    }
}
