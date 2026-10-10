using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Naval.Storms.Interfaces;
using Coop.Naval.Storms.Messages;
using GameInterface.Services.ObjectManager;
using NavalDLC.Map;

namespace Coop.Naval.Storms.Handlers;

internal class StormSyncHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IStormInterface stormInterface;

    public StormSyncHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IStormInterface stormInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.stormInterface = stormInterface;

        messageBroker.Subscribe<StormStateChanged>(Handle_StormStateChanged);
        messageBroker.Subscribe<NetworkStormStateChanged>(Handle_NetworkStormStateChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StormStateChanged>(Handle_StormStateChanged);
        messageBroker.Unsubscribe<NetworkStormStateChanged>(Handle_NetworkStormStateChanged);
    }

    private void Handle_StormStateChanged(MessagePayload<StormStateChanged> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Storm, out var stormId)) return;

        var stormState = stormInterface.GetState(data.Storm);

        network.SendAll(new NetworkStormStateChanged(stormId, stormState, data.IsNewStorm));
    }

    private void Handle_NetworkStormStateChanged(MessagePayload<NetworkStormStateChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Storm>(data.StormId, out var storm)) return;

            using (new AllowedThread())
            {
                stormInterface.ApplyState(storm, data.State);

                if (data.IsNewStorm)
                {
                    stormInterface.AddSpawnedStorm(storm);
                }
            }
        });
    }
}
