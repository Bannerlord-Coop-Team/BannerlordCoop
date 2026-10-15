using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MapTracking.Messages;
using GameInterface.Services.Players;

namespace GameInterface.Services.MapTracking.Handlers;

internal class PlayerMapTrackingHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IPlayerMapTracking mapTracking;

    public PlayerMapTrackingHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider,
        IPlayerMapTracking mapTracking)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.mapTracking = mapTracking;

        messageBroker.Subscribe<NetworkPlayerMapTrackingChanged>(Handle_NetworkPlayerMapTrackingChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerMapTrackingChanged>(Handle_NetworkPlayerMapTrackingChanged);
    }

    private void Handle_NetworkPlayerMapTrackingChanged(MessagePayload<NetworkPlayerMapTrackingChanged> obj)
    {
        if (ModInformation.IsServer) return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            mapTracking.Apply(data.ControllerId, data.OtherControllerId, data.IsTracking);

            var localControllerId = controllerIdProvider.ControllerId;
            string otherControllerId = null;
            if (data.ControllerId == localControllerId) otherControllerId = data.OtherControllerId;
            else if (data.OtherControllerId == localControllerId) otherControllerId = data.ControllerId;

            if (otherControllerId == null || !playerManager.TryGetPlayer(otherControllerId, out var otherPlayer)) return;
            
            mapTracking.RefreshTracker(otherPlayer);
        });
    }
}
