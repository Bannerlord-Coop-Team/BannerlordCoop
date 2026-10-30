using Common;
using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.UI.ServerInfo;

namespace Coop.Core.Client.Services.Session;

/// <summary>Creates the server info panel with the campaign and hands it the server's info.</summary>
internal sealed class ServerInfoClientHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IServerInfoService service;
    private bool disposed;

    public ServerInfoClientHandler(IMessageBroker broker, IServerInfoService service)
    {
        this.broker = broker;
        this.service = service;
        broker.Subscribe<ClientCampaignReady>(Ready);
        broker.Subscribe<NetworkServerInfo>(Receive);
    }

    // The overlay needs the campaign map, so it is created only once the campaign is ready.
    private void Ready(MessagePayload<ClientCampaignReady> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Initialize();
    }, context: nameof(ServerInfoClientHandler));

    // Keeps Gauntlet changes on the game thread; the service holds the info until the map is free.
    private void Receive(MessagePayload<NetworkServerInfo> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Show(payload.What);
    }, context: nameof(ServerInfoClientHandler));

    // Prevents queued messages from reopening a disposed session's UI.
    public void Dispose()
    {
        disposed = true;
        broker.Unsubscribe<ClientCampaignReady>(Ready);
        broker.Unsubscribe<NetworkServerInfo>(Receive);
    }
}
