using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Auto;
class AutoRegistryHandler<T> : IHandler where T : class
{
    public AutoRegistry<T> Registry { get; }
    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IObjectManager ObjectManager { get; }
    public Action<T, string> ClientCreatedCallback { get; }
    public Action<T, string> ClientDestroyedCallback { get; }

    public AutoRegistryHandler(
        AutoRegistry<T> registry,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        Action<T, string> clientCreatedCallback = null,
        Action<T, string> clientDestroyedCallback = null)
    {
        Registry = registry;
        MessageBroker = messageBroker;
        Network = network;
        ObjectManager = objectManager;
        ClientCreatedCallback = clientCreatedCallback;
        ClientDestroyedCallback = clientDestroyedCallback;

        MessageBroker.Subscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Subscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
        MessageBroker.Subscribe<InstanceDestroyed<T>>(Handle_InstanceDestroyed);
        MessageBroker.Subscribe<NetworkDestroyInstance<T>>(Handle_NetworkDestroyInstance);
    }

    public void Dispose()
    {
        MessageBroker.Unsubscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Unsubscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
        MessageBroker.Unsubscribe<InstanceDestroyed<T>>(Handle_InstanceDestroyed);
        MessageBroker.Unsubscribe<NetworkDestroyInstance<T>>(Handle_NetworkDestroyInstance);
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

        if (ClientCreatedCallback != null) ClientCreatedCallback(newInstance, payload.What.InstanceId);
    }

    private void Handle_InstanceDestroyed(MessagePayload<InstanceDestroyed<T>> payload)
    {
        if (ObjectManager.TryGetId(payload.What.Instance, out string id) == false) return;

        ObjectManager.Remove(payload.What.Instance);

        Network.SendAll(new NetworkCreateInstance<T>(id));
    }

    private void Handle_NetworkDestroyInstance(MessagePayload<NetworkDestroyInstance<T>> payload)
    {
        if (ObjectManager.TryGetObject(payload.What.InstanceId, out T obj) == false) return;

        ObjectManager.Remove(obj);

        if (ClientDestroyedCallback != null) ClientDestroyedCallback(obj, payload.What.InstanceId);
    }
}