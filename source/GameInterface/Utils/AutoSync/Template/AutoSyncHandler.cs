using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using System;
using System.Reflection;

namespace GameInterface.Utils.AutoSync.Template;

/// <summary>
/// DO NOT USE DIRECTLY! Template for creating an AutoSyncHandler
/// </summary>
public interface IAutoSyncHandlerTemplate : IHandler
{
}

/// <inheritdoc cref="IAutoSyncHandlerTemplate"/>
/// <typeparam name="ObjectType">Type of object to be synced</typeparam>
/// <typeparam name="DataType">Property data type</typeparam>
/// <typeparam name="NetworkMesage">Type of network message</typeparam>
/// <typeparam name="EventMessage">Type of event message</typeparam>
public class AutoSyncHandler<ObjectType, DataType, NetworkMesage, EventMessage> : IAutoSyncHandlerTemplate
    where ObjectType : class
    where NetworkMesage : IAutoSyncMessage<DataType>
    where EventMessage : IAutoSyncMessage<DataType>
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger logger;
    private readonly Action<ObjectType, DataType> propertySetter;

    public AutoSyncHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILogger logger,
        PropertyInfo syncedProperty)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.logger = logger;

        propertySetter = syncedProperty.BuildUntypedSetter<ObjectType, DataType>();

        messageBroker.Subscribe<NetworkMesage>(Handle_NetworkMessage);
        messageBroker.Subscribe<EventMessage>(Handle_EventMessage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMesage>(Handle_NetworkMessage);
        messageBroker.Unsubscribe<EventMessage>(Handle_EventMessage);
    }

    private void Handle_EventMessage(MessagePayload<EventMessage> payload)
    {
        var message = (NetworkMesage)Activator.CreateInstance(typeof(NetworkMesage), payload.What.Data);
        network.SendAll(message);
    }

    private void Handle_NetworkMessage(MessagePayload<NetworkMesage> payload)
    {
        if (objectManager.TryGetObject(payload.What.Data.StringId, out ObjectType obj) == false)
        {
            logger.Error("Could not find {objType} with string id {stringId}", typeof(ObjectType), payload.What.Data.StringId);
            return;
        }

        var value = payload.What.Data.Value;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                propertySetter(obj, value);
            }
        });
    }
}
