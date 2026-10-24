using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Configuration;
using GameInterface.Services.UI.ServerInfo;
using System;

namespace Coop.Core.Server.Services.Session;

/// <summary>Sends the operator's server info, as one message, to each player whose campaign sync completed.</summary>
internal sealed class ServerInfoServerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly Lazy<NetworkServerInfo> info;
    private bool disposed;

    public ServerInfoServerHandler(IMessageBroker messageBroker, INetwork network, IModConfig modConfig, IServerInfoMessageBuilder builder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        // Built once per session, so the caps and link warnings log once and not on every join.
        info = new Lazy<NetworkServerInfo>(() => builder.Build(modConfig.Data.ServerInfo));

        messageBroker.Subscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    // Prevents a queued send after the session ended.
    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    private void Handle_PlayerCampaignSynchronized(MessagePayload<PlayerCampaignSynchronized> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() =>
        {
            if (disposed || info.Value == null) return;
            network.Send(peer, info.Value);
        }, context: nameof(ServerInfoServerHandler));
    }
}
