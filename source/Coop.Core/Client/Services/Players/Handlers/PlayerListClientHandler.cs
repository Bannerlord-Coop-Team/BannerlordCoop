using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.UI.PlayerList;

namespace Coop.Core.Client.Services.Players.Handlers;

/// <summary>Initializes the player list and applies server snapshots on the game thread.</summary>
internal sealed class PlayerListClientHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IPlayerListService service;
    private readonly IPlatformDisplayNameProvider platform;
    private bool disposed;

    // Connects campaign readiness and roster reception to the UI contract.
    public PlayerListClientHandler(IMessageBroker broker, INetwork network,
        IPlayerListService service, IPlatformDisplayNameProvider platform)
    {
        this.broker = broker;
        this.network = network;
        this.service = service;
        this.platform = platform;
        broker.Subscribe<ClientCampaignReady>(Ready);
        broker.Subscribe<NetworkPlayerList>(Receive);
    }

    // Reports the name after the sending peer has a registered campaign player.
    private void Ready(MessagePayload<ClientCampaignReady> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Initialize();
        network.SendAll(new NetworkPlayerPlatformName(platform.GetName()));
    });

    // Keeps Gauntlet mutations ordered on the game thread.
    private void Receive(MessagePayload<NetworkPlayerList> payload) => GameThread.RunSafe(() =>
    {
        if (disposed) return;
        service.Update(payload.What.Entries);
    });

    // Prevents queued messages from reopening a disposed session's UI.
    public void Dispose()
    {
        disposed = true;
        broker.Unsubscribe<ClientCampaignReady>(Ready);
        broker.Unsubscribe<NetworkPlayerList>(Receive);
    }
}
