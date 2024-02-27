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
public class AutoSyncHandlerTemplate<ObjectType, DataType, EventMessage> : IHandler 
    where ObjectType : class
    where EventMessage : IAutoSyncMessage<DataType>
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger logger;
    private readonly Action<ObjectType, DataType> propertySetter;

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

        messageBroker.Subscribe<NetworkChangeDataMessage>(Handle_NetworkMessage);
        messageBroker.Subscribe<EventMessage>(Handle_EventMessage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeDataMessage>(Handle_NetworkMessage);
        messageBroker.Unsubscribe<EventMessage>(Handle_EventMessage);
    }

    private void Handle_EventMessage(MessagePayload<EventMessage> payload)
    {
        var message = new NetworkChangeDataMessage(payload.What.Data.StringId, payload.What.Data);
        network.SendAll(message);
    }

    private void Handle_NetworkMessage(MessagePayload<NetworkChangeDataMessage> payload)
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

    [ProtoContract(SkipConstructor = true)]
    class NetworkChangeDataMessage : IAutoSyncMessage<DataType>, ICommand
    {
        public NetworkChangeDataMessage(string stringId, IAutoSyncData<DataType> data)
        {
            Data = data;
        }

        [ProtoMember(1)]
        public string StringId { get; }

        [ProtoMember(2)]
        public IAutoSyncData<DataType> Data { get; }
    }
}
