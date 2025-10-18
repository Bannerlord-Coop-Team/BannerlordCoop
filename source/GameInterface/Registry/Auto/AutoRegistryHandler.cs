using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Auto;
class AutoRegistryHandler<T> : IHandler where T : class
{
    public AutoRegistry<T> Registry { get; }
    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IObjectManager ObjectManager { get; }
    AutoRegistryCallbacks<T> Callbacks { get; }
    ILogger Logger { get; } = LogManager.GetLogger<AutoRegistryHandler<T>>();

    public AutoRegistryHandler(
        AutoRegistry<T> registry,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        AutoRegistryCallbacks<T> callbacks)
    {
        Registry = registry;
        MessageBroker = messageBroker;
        Network = network;
        ObjectManager = objectManager;
        Callbacks = callbacks;

        MessageBroker.Subscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Subscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
        MessageBroker.Subscribe<NotifyInstanceCreated<T>>(Handle_NotifyInstanceCreated);

        MessageBroker.Subscribe<InstanceDestroyed<T>>(Handle_InstanceDestroyed);
        MessageBroker.Subscribe<NetworkDestroyInstance<T>>(Handle_NetworkDestroyInstance);
    }

    public void Dispose()
    {
        MessageBroker.Unsubscribe<InstanceCreated<T>>(Handle_InstanceCreated);
        MessageBroker.Unsubscribe<NetworkCreateInstance<T>>(Handle_NetworkCreateInstance);
        MessageBroker.Unsubscribe<NotifyInstanceCreated<T>>(Handle_NotifyInstanceCreated);

        MessageBroker.Unsubscribe<InstanceDestroyed<T>>(Handle_InstanceDestroyed);
        MessageBroker.Unsubscribe<NetworkDestroyInstance<T>>(Handle_NetworkDestroyInstance);
    }


    private void Handle_InstanceCreated(MessagePayload<InstanceCreated<T>> payload)
    {
        var instance = payload.What.Instance;
        if (ObjectManager.AddNewObject(instance, out var id) == false)
        {
            Logger.Error("Unable to create new id for {type}", typeof(T).Name);
            return;
        }

        if (instance is MBObjectBase mBObject)
        {
            using(new AllowedThread())
            {
                mBObject.StringId = id;
            }
        }

        // Callback before sent on network
        Callbacks.ServerCreatedCallback?.Invoke(payload.What.Instance, id);

        Network.SendAll(new NetworkCreateInstance<T>(id));

        foreach (var notification in NotificationFactory.CreateNotifications(typeof(T), id, instance))
        {
            MessageBroker.Publish(this, notification);
        }
    }


    private void Handle_NetworkCreateInstance(MessagePayload<NetworkCreateInstance<T>> payload)
    {
        var newInstance = ObjectHelper.SkipConstructor<T>();
        var instanceId = payload.What.InstanceId;

        if (newInstance is MBObjectBase mBObject)
        {
            using (new AllowedThread())
            {
                mBObject.StringId = payload.What.InstanceId;
            }
        }

        if (ObjectManager.AddExisting(instanceId, newInstance) == false)
        {
            Logger.Error("Unable to create new id for {type} with id {id}", typeof(T).Name, instanceId);
            return;
        }

        foreach (var notification in NotificationFactory.CreateNotifications(typeof(T), instanceId, newInstance))
        {
            MessageBroker.Publish(this, notification);
        }

        Callbacks.ClientCreatedCallback?.Invoke(newInstance, instanceId);
    }

    private void Handle_NotifyInstanceCreated(MessagePayload<NotifyInstanceCreated<T>> payload)
    {
        var instance = payload.What.Instance;
        var instanceId = payload.What.InstanceId;

        if (ObjectManager.AddExisting(instanceId, instance) == false)
        {
            Logger.Error("Unable to create new id for {type} with id {id}", typeof(T).Name, instanceId);
            return;
        }

        Callbacks.ClientCreatedCallback?.Invoke(instance, instanceId);
    }

    private void Handle_InstanceDestroyed(MessagePayload<InstanceDestroyed<T>> payload)
    {
        if (ObjectManager.TryGetId(payload.What.Instance, out string id) == false) return;

        // Callback before object is removed from registry
        Callbacks.ServerDestroyedCallback?.Invoke(payload.What.Instance, id);

        ObjectManager.Remove(payload.What.Instance);

        Network.SendAll(new NetworkCreateInstance<T>(id));
    }

    private void Handle_NetworkDestroyInstance(MessagePayload<NetworkDestroyInstance<T>> payload)
    {
        if (ObjectManager.TryGetObject(payload.What.InstanceId, out T obj) == false) return;

        // Callback before object is removed from registry
        Callbacks.ClientDestroyedCallback?.Invoke(obj, payload.What.InstanceId);

        ObjectManager.Remove(obj);
    }
}