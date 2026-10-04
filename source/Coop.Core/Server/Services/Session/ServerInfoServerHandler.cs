using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.UI.ServerInfo;
using LiteNetLib;
using System.Linq;

namespace Coop.Core.Server.Services.Session;

/// <summary>Sends the operator's server info, as one message, to each player whose campaign sync completed.</summary>
internal sealed class ServerInfoServerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IServerInfoConfig serverInfo;

    public ServerInfoServerHandler(IMessageBroker messageBroker, INetwork network, IServerInfoConfig serverInfo)
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
        if (serverInfo.Motd.Count == 0 && serverInfo.Rules.Count == 0 &&
            serverInfo.Links.Count == 0 && serverInfo.News.Count == 0) return;

        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() => SendServerInfo(peer), context: nameof(ServerInfoServerHandler));
    }

    private void SendServerInfo(NetPeer peer)
    {
        network.Send(peer, new NetworkServerInfo(
            serverInfo.Motd.ToArray(),
            serverInfo.Rules.ToArray(),
            serverInfo.Links.ToArray(),
            serverInfo.News.ToArray()));
    }
}
