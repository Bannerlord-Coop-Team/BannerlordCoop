using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.BesiegerCampss.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using static Common.Serialization.BinaryFormatterSerializer;
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

        var message = CreateNetworkMessage(data);

        network.SendAll(message);
    }

    private void Handle_ChangeProperty(MessagePayload<NetworkBesiegerCampChangeProperty> payload)
    {
        var data = payload.What;
        if (objectManager.TryGetObject<BesiegerCamp>(data.BesiegerCampId, out var instance) == false)
        {
            Logger.Error("Unable to find {type} with id: {id}", typeof(BesiegerCamp), data.BesiegerCampId);
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
        var propInfo = typeof(BesiegerCamp).GetProperty(data.PropertyName);
        if (propInfo == null)
        {
            Logger.Error("Unable to find property with name {propName} on type: {type}", data.PropertyName, typeof(BesiegerCamp));
            return;
        }
        object newValue = ResolvePropertyValue(data, propInfo);
        propInfo.SetValue(instance, newValue);
    }

    private object ResolvePropertyValue(NetworkBesiegerCampChangeProperty data, PropertyInfo propInfo)
    {
        object obj;
        Type propType = propInfo.PropertyType;

        // special case for this pesky type because its not working really well with object manager
        if (propType == typeof(SiegeStrategy))
        {
            obj = SiegeStrategy.All.FirstOrDefault(x => string.Equals(x.StringId, data.ObjectId));
            if (obj == null)
            {
                Logger.Error("Unable to find SiegeStrategy with id: {id}", data.ObjectId);
            }
            return obj;
        }

        if (!propType.IsClass) // Obj is simple struct and was serialized, just deserialize it
        {
            obj = Deserialize(data.SerializedValue);
        }
        else
        {
            if (!objectManager.TryGetObject(data.ObjectId, propType, out obj)) // Obj is a class, use ObjectManager
            {
                Logger.Error("Unable to find {type} with id: {id}", propType.Name, data.ObjectId);
            }
        }

        return obj;
    }

    public NetworkBesiegerCampChangeProperty CreateNetworkMessage(BesiegerCampPropertyChanged internalMessage)
    {
        string besiegeCampId = objectManager.TryGetId(internalMessage.BesiegerCamp, Logger);
        PropertyInfo property = internalMessage.PropertyInfo;
        bool isClass = property.PropertyType.IsClass;

        if (isClass)
        {
            var id = objectManager.TryGetId(internalMessage.Value, Logger);
            return new NetworkBesiegerCampChangeProperty(property.Name, besiegeCampId, id);
        }
        else
        {
            var serializedValue = Serialize(internalMessage.Value);
            return new NetworkBesiegerCampChangeProperty(property.Name, besiegeCampId, serializedValue);
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegerCampPropertyChanged>(Handle_PropertyChanged);
        messageBroker.Unsubscribe<NetworkBesiegerCampChangeProperty>(Handle_ChangeProperty);
    }
}