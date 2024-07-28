using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Handlers;
internal class KingdomLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public KingdomLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<KingdomCreated>(Handle_KingdomCreated);
        messageBroker.Subscribe<NetworkCreateKingdom>(Handle_CreateKingdom);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<KingdomCreated>(Handle_KingdomCreated);
        messageBroker.Unsubscribe<NetworkCreateKingdom>(Handle_CreateKingdom);
    }


    private void Handle_KingdomCreated(MessagePayload<KingdomCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Kingdom, out var newId);

        var message = new NetworkCreateKingdom(newId);
        network.SendAll(message);
    }

    private void Handle_CreateKingdom(MessagePayload<NetworkCreateKingdom> payload)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var newKingdom = new Kingdom();
                objectManager.AddExisting(payload.What.KindgomId, newKingdom);
            }
        });

    }
}
