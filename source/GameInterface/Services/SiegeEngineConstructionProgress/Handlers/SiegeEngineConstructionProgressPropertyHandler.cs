using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BesiegerCamps.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngineConstructionProgresss.Messages;
using GameInterface.Services.SiegeEngineConstructionProgressss.Messages;
using Serilog;
using System;
using System.Reflection;
using static Common.Serialization.BinaryFormatterSerializer;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Handlers;

//internal class SiegeEngineConstructionProgressPropertyHandler : IHandler
//{
//    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineConstructionProgressLifetimeHandler>();

//    private readonly IMessageBroker messageBroker;
//    private readonly INetwork network;
//    private readonly IObjectManager objectManager;

//    public SiegeEngineConstructionProgressPropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
//    {
//        this.messageBroker = messageBroker;
//        this.network = network;
//        this.objectManager = objectManager;
//        messageBroker.Subscribe<SiegeEngineConstructionProgressPropertyChanged>(Handle_PropertyChanged);
//        messageBroker.Subscribe<NetworkSiegeEngineConstructionProgressChangeProperty>(Handle_ChangeProperty);
//    }

//    private void Handle_PropertyChanged(MessagePayload<SiegeEngineConstructionProgressPropertyChanged> payload)
//    {
//        var data = payload.What;

//        var message = data.CreateNetworkMessage(Logger);

//        network.SendAll(message);
//    }

//    private void Handle_ChangeProperty(MessagePayload<NetworkSiegeEngineConstructionProgressChangeProperty> payload)
//    {
//        var data = payload.What;
//        if (objectManager.TryGetObject<SiegeEngineConstructionProgress>(data.siegeEngineConstructionProgressId, out var instance) == false)
//        {
//            Logger.Error("Unable to find {type} with id: {id}", typeof(SiegeEngineConstructionProgress), data.siegeEngineConstructionProgressId);
//            return;
//        }

//        GameLoopRunner.RunOnMainThread(() =>
//        {
//            using (new AllowedThread())
//            {
//                HandleDataChanged(instance, data);
//            }
//        });
//    }

//    private object ResolvePropertyValue(NetworkSiegeEngineConstructionProgressChangeProperty data, PropertyInfo propInfo)
//    {
//        object obj;
//        Type propType = propInfo.PropertyType;

//        if (!propType.IsClass) // Obj is simple struct and was serialized, just deserialize it
//        {
//            obj = Deserialize(data.serializedValue);
//        }
//        else
//        {
//            if (!objectManager.TryGetObject(data.objectId, propType, out obj)) // Obj is a class, use ObjectManager
//            {
//                Logger.Error("Unable to find {type} with id: {id}", propType.Name, data.objectId);
//            }
//        }

//        return obj;
//    }

//    private void HandleDataChanged(SiegeEngineConstructionProgress instance, NetworkSiegeEngineConstructionProgressChangeProperty data)
//    {
//        var propInfo = typeof(SiegeEngineConstructionProgress).GetProperty(data.propertyName);
//        if (propInfo == null)
//        {
//            Logger.Error("Unable to find property with name {propName} on type: {type}", data.propertyName, typeof(SiegeEngineConstructionProgress));
//            return;
//        }
//        object newValue = ResolvePropertyValue(data, propInfo);
//        propInfo.SetValue(instance, newValue);
//    }

//    public void Dispose()
//    {
//        messageBroker.Unsubscribe<SiegeEngineConstructionProgressPropertyChanged>(Handle_PropertyChanged);
//        messageBroker.Unsubscribe<NetworkSiegeEngineConstructionProgressChangeProperty>(Handle_ChangeProperty);
//    }
//}