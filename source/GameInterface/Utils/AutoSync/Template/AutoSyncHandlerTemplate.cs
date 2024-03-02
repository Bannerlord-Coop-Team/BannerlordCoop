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

public interface IAutoSyncHandlerTemplate : IHandler
{
    Type NetworkMessageType { get; }
}

public class AutoSyncHandlerTemplate<ObjectType, DataType, NetworkMesage, EventMessage> : IAutoSyncHandlerTemplate
    where ObjectType : class
    where NetworkMesage : IAutoSyncMessage<DataType>
    where EventMessage : IAutoSyncMessage<DataType>
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger logger;
    private readonly Action<ObjectType, DataType> propertySetter;

    public Type NetworkMessageType => typeof(NetworkMesage);

    public AutoSyncHandlerTemplate(
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
