using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Chat.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.Session;

/// <summary>Sends the operator's MOTD as System chat to each player whose campaign sync completed.</summary>
internal sealed class ServerMotdHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IServerInfoConfig serverInfo;

    public ServerMotdHandler(IMessageBroker messageBroker, INetwork network, IServerInfoConfig serverInfo)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.serverInfo = serverInfo;

        messageBroker.Subscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    private void Handle_PlayerCampaignSynchronized(MessagePayload<PlayerCampaignSynchronized> payload)
    {
        if (serverInfo.Motd.Count == 0) return;

        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() => SendMotd(peer), context: nameof(ServerMotdHandler));
    }

    private void SendMotd(NetPeer peer)
    {
        foreach (string line in serverInfo.Motd)
        {
            network.SendImmediate(peer, new NetworkChatMessage(
                ChatChannel.System,
                string.Empty,
                "System",
                string.Empty,
                string.Empty,
                line));
        }
    }
}
