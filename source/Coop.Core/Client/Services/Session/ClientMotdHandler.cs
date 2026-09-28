using Common;
using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.UI.Motd;

namespace Coop.Core.Client.Services.Session;

/// <summary>Creates the message of the day popup with the campaign and hands it the server's text.</summary>
internal sealed class ClientMotdHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IMotdService service;
    private bool disposed;

    public ClientMotdHandler(IMessageBroker broker, IMotdService service)
    {
        this.broker = broker;
        this.service = service;
        broker.Subscribe<ClientCampaignReady>(Ready);
        broker.Subscribe<NetworkMotd>(Receive);
    }

    // The overlay needs the campaign map, so it is created only once the campaign is ready.
    private void Ready(MessagePayload<ClientCampaignReady> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Initialize();
    }, context: nameof(ClientMotdHandler));

    // Keeps Gauntlet changes on the game thread; the service holds the text until the map is free.
    private void Receive(MessagePayload<NetworkMotd> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Show(payload.What.Paragraphs);
    }, context: nameof(ClientMotdHandler));

    // Prevents queued messages from reopening a disposed session's UI.
    public void Dispose()
    {
        disposed = true;
        broker.Unsubscribe<ClientCampaignReady>(Ready);
        broker.Unsubscribe<NetworkMotd>(Receive);
    }
}
