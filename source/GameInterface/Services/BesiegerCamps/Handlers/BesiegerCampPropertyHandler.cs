using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using static Common.Serialization.BinaryFormatterSerializer;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using GameInterface.Services.BesiegerCampss.Messages;
using GameInterface.Services.BesiegerCamps.Patches;
using static GameInterface.Services.BesiegerCamps.Extensions.BesiegerCampExtensions;

namespace GameInterface.Services.BesiegerCamps.Handlers;
internal class BesiegerCampPropertyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public BesiegerCampPropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<BesiegerCampPropertyChanged>(Handle_PropertyChanged);
        messageBroker.Subscribe<NetworkBesiegerCampChangeProperty>(Handle_ChangeProperty);
    }


    private void Handle_PropertyChanged(MessagePayload<BesiegerCampPropertyChanged> payload)
    {
        var data = payload.What;

        var message = data.CreateNetworkMessage(Logger);

        network.SendAll(message);
    }

    private void Handle_ChangeProperty(MessagePayload<NetworkBesiegerCampChangeProperty> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<BesiegerCamp>(data.besiegerCampId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), data.besiegerCampId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                HandleDataChanged(instance, data);
            }
        });
    }

    private void HandleDataChanged(BesiegerCamp instance, NetworkBesiegerCampChangeProperty data)
    {
        var propInfo = typeof(BesiegerCamp).GetProperty(data.propertyName);
        if (propInfo == null)
        {
            Logger.Error("Unable to find property with name {propName} on type: {type}", data.propertyName, typeof(BesiegerCamp));
            return;
        }
        object obj;

        if (!propInfo.PropertyType.IsClass) // Obj is simple struct and was serialized, just deserialize it
        {
            obj = Deserialize(data.serializedValue);
        }
        else
        {
            if (!objectManager.TryGetObject(data.objectId, propInfo.PropertyType, out obj)) // Obj is a class, use ObjectManager
            {
                Logger.Error("Unable to find {type} with id: {id}", propInfo.PropertyType.Name, data.objectId);
                return;
            }
        }

        propInfo.SetValue(instance, obj);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegerCampPropertyChanged>(Handle_PropertyChanged);
        messageBroker.Unsubscribe<NetworkBesiegerCampChangeProperty>(Handle_ChangeProperty);
    }


    //private void Handle(MessagePayload<BesiegerCampSiegeEventChanged> payload)
    //{
    //    if (objectManager.TryGetId(payload.What.BesiegerCamp, out var besiegerCampId) == false) return;
    //    if (objectManager.TryGetId(payload.What.SiegeEvent, out var siegeEventId) == false) return;

    //    network.SendAll(new NetworkChangeBesiegerCampSiegeEvent(besiegerCampId, siegeEventId));
    //}


    //private void Handle(MessagePayload<NetworkChangeBesiegerCampSiegeEvent> payload)
    //{
    //    if (objectManager.TryGetObject<BesiegerCamp>(payload.What.BesiegerCampId, out var besiegerCamp) == false) return;
    //    if (objectManager.TryGetObject<SiegeEvent>(payload.What.SiegeEventId, out var siegeEvent) == false) return;

    //    GameLoopRunner.RunOnMainThread(() =>
    //    {
    //        using (new AllowedThread())
    //        {
    //            besiegerCamp.SiegeEvent = siegeEvent;
    //        }
    //    });

    //}
}
