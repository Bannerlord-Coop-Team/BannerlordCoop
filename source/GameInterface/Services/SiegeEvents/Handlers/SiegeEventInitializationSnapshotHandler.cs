using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.SiegeEvents.Messages;

namespace GameInterface.Services.SiegeEvents.Handlers;

internal class SiegeEventInitializationSnapshotHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ISiegeEventGraphSynchronizer graphSynchronizer;

    public SiegeEventInitializationSnapshotHandler(
        IMessageBroker messageBroker,
        ISiegeEventGraphSynchronizer graphSynchronizer)
    {
        this.messageBroker = messageBroker;
        this.graphSynchronizer = graphSynchronizer;
        messageBroker.Subscribe<NetworkInitializeSiegeEvent>(HandleInitialize);
    }

    private void HandleInitialize(MessagePayload<NetworkInitializeSiegeEvent> payload)
    {
        if (ModInformation.IsServer) return;
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            graphSynchronizer.TryApply(message.ToSnapshot());
        }, context: nameof(NetworkInitializeSiegeEvent));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkInitializeSiegeEvent>(HandleInitialize);
    }
}
