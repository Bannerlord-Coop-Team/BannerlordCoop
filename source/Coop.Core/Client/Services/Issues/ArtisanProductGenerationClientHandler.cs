using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.Issues.Messages;

namespace Coop.Core.Client.Services.Issues;

internal sealed class ArtisanProductGenerationClientHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;

    public ArtisanProductGenerationClientHandler(IMessageBroker broker, INetwork network)
    {
        this.broker = broker;
        this.network = network;
        broker.Subscribe<ClientCampaignReady>(HandleReady);
    }

    public void Dispose() => broker.Unsubscribe<ClientCampaignReady>(HandleReady);

    private void HandleReady(MessagePayload<ClientCampaignReady> payload)
        => network.SendAll(new RequestArtisanProductGenerationContext());
}
