using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Handlers;
internal class HideoutLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public HideoutLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<HideoutCreated>(Handle_HideoutCreated);
        messageBroker.Subscribe<NetworkCreateHideout>(Handle_NetworkCreateHideout);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HideoutCreated>(Handle_HideoutCreated);
        messageBroker.Unsubscribe<NetworkCreateHideout>(Handle_NetworkCreateHideout);
    }

    private void Handle_HideoutCreated(MessagePayload<HideoutCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Hideout, out var newId);

        var message = new NetworkCreateHideout(newId);
        network.SendAll(message);
    }

    private void Handle_NetworkCreateHideout(MessagePayload<NetworkCreateHideout> payload)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var newHideout = ObjectHelper.SkipConstructor<Hideout>();
                objectManager.AddExisting(payload.What.HideoutId, newHideout);
            }
        });
    }
}
