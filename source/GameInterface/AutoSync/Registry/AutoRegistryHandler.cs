using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.AutoSync.Registry;
class AutoRegistryHandler<T> : IHandler where T : class
{
    public AutoRegistry<T> Registry { get; }
    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IObjectManager ObjectManager { get; }
    public Action<string, T> ClientCreatedCallback { get; }

    public AutoRegistryHandler(
        AutoRegistry<T> registry,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        Action<string, T> clientCreatedCallback = null)
    {
        Registry = registry;
        MessageBroker = messageBroker;
        Network = network;
        ObjectManager = objectManager;
        ClientCreatedCallback = clientCreatedCallback;

        MessageBroker.Subscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Subscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
    }

    public void Dispose()
    {
        MessageBroker.Subscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Subscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
    }

    private void Handle_InstanceCreated(MessagePayload<InstanceCreated<T>> payload)
    {
        ObjectManager.AddNewObject(payload.What.Instance, out var id);

        if (payload.What.Instance is MBObjectBase mBObject)
        {
            mBObject.StringId = id;
        }

        Network.SendAll(new NetworkCreateInstance<T>(id));
    }

    private void Handle_NetworkCreateInstance(MessagePayload<NetworkCreateInstance<T>> payload)
    {
        var newInstance = ObjectHelper.SkipConstructor<T>();

        ObjectManager.AddExisting(payload.What.InstanceId, newInstance);

        if (ClientCreatedCallback != null) ClientCreatedCallback(payload.What.InstanceId, newInstance);
    }
}