using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemComponents.Data;
using GameInterface.Services.ItemComponents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.PartyComponents.Handlers;
internal class ItemComponentHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ItemComponentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private readonly Type[] itemTypes = new Type[]
    {
        typeof(HorseComponent),
        typeof(ArmorComponent),
        typeof(WeaponComponent),
        typeof(BannerComponent),
        typeof(SaddleComponent),
        typeof(TradeItemComponent),
    };

    public ItemComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ItemComponentCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateItemComponent>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemComponentCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateItemComponent>(Handle);
    }

    private void Handle(MessagePayload<ItemComponentCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        var typeIndex = itemTypes.IndexOf(payload.What.Instance.GetType());
        var data = new ItemComponentData(typeIndex, id);
        network.SendAll(new NetworkCreateItemComponent(data));
    }

    private void Handle(MessagePayload<NetworkCreateItemComponent> payload)
    {
        var data = payload.What.Data;
        var typeIdx = data.TypeIndex;

        var obj = ObjectHelper.SkipConstructor(itemTypes[typeIdx]);

        objectManager.AddExisting(data.Id, obj);
    }
}