using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using LiteNetLib;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class ArtisanProductGenerationHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IPlayerManager players;
    private readonly IArtisanProductGenerationContext context;

    public ArtisanProductGenerationHandler(IMessageBroker broker, IPlayerManager players,
        IArtisanProductGenerationContext context)
    {
        this.broker = broker;
        this.players = players;
        this.context = context;
        broker.Subscribe<RequestArtisanProductGenerationContext>(HandleRequest);
        broker.Subscribe<CampaignReady>(HandleCampaignReady);
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestArtisanProductGenerationContext>(HandleRequest);
        broker.Unsubscribe<CampaignReady>(HandleCampaignReady);
    }

    private void HandleCampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsServer) context.Clear();
    }

    private void HandleRequest(MessagePayload<RequestArtisanProductGenerationContext> payload)
    {
        if (ModInformation.IsClient) return;
        var peer = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (peer != null && players.TryGetPlayer(peer, out var player))
                context.Request(player.ControllerId);
        });
    }
}
