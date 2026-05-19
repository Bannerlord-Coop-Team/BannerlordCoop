using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Auto;
class AutoRegistryHandler<T> : IHandler where T : class
{
    public AutoRegistryBase<T> Registry { get; }
    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IObjectManager ObjectManager { get; }
    ILogger Logger { get; } = LogManager.GetLogger<AutoRegistryHandler<T>>();

    public AutoRegistryHandler(
        AutoRegistryBase<T> registry,
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        Registry = registry;
        MessageBroker = messageBroker;
        Network = network;
        ObjectManager = objectManager;

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
        
        var id = $"Created_{ObjectManager.GetUniqueTypeId(payload.What.Instance)}";

        if (payload.What.Instance is MBObjectBase mBObject)
        {
            using(new AllowedThread())
            {
                mBObject.StringId = id;
            }
        }

        if (!ObjectManager.AddExisting($"{typeof(T).Name}_{id}", payload.What.Instance))
        {
            Logger.Error("Unable to create new id for {type}", typeof(T).Name);
            return;
        }


        if (Registry.Debug)
        {
            Logger.Debug("[Server][{CallingMethod}] Created new instance of {type} with id {id}. {Callstack}",
                $"{nameof(AutoRegistryHandler<T>)}.{nameof(Handle_InstanceCreated)}",
                typeof(T).Name,
                id,
                Environment.StackTrace);
        }

        // Callback before sent on network
        Registry.OnServerCreated(payload.What.Instance, $"{typeof(T).Name}_{id}");

        Network.SendAll(new NetworkCreateInstance<T>(id));
    }

    private void Handle_NetworkCreateInstance(MessagePayload<NetworkCreateInstance<T>> payload)
    {
        var newInstance = ObjectHelper.SkipConstructor<T>();

        var id = payload.What.InstanceId;

        if (newInstance is MBObjectBase mBObject)
        {
            using (new AllowedThread())
            {
                mBObject.StringId = payload.What.InstanceId;
            }
        }

        if (!ObjectManager.AddExisting($"{typeof(T).Name}_{id}", newInstance))
        {
            Logger.Error("Failed to create new id for {type} with id {id}", typeof(T).Name, payload.What.InstanceId);
            return;
        }

        if (Registry.Debug)
        {
            Logger.Debug("[Client][{CallingMethod}] Created new instance of {type} with id {id}.", 
                $"{nameof(AutoRegistryHandler<T>)}.{nameof(Handle_NetworkCreateInstance)}",
                typeof(T).Name, 
                payload.What.InstanceId);
        }

        // Callback after created on client
        Registry.OnClientCreated(newInstance, payload.What.InstanceId);
    }

    private void Handle_InstanceDestroyed(MessagePayload<InstanceDestroyed<T>> payload)
    {
        if (!ObjectManager.TryGetIdWithLogging(payload.What.Instance, out string id)) return;

        // Callback before object is removed from registry
        Registry.OnServerDestroyed(payload.What.Instance, id);

        if (Registry.Debug)
        {
            Logger.Debug("[Server][{CallingMethod}] Destroyed instance of {type} with id {id}. {Callstack}",
                $"{nameof(AutoRegistryHandler<T>)}.{nameof(Handle_InstanceDestroyed)}",
                typeof(T).Name,
                id,
                Environment.StackTrace);
        }

        ObjectManager.Remove(payload.What.Instance);

        Network.SendAll(new NetworkDestroyInstance<T>(id));
    }

    private void Handle_NetworkDestroyInstance(MessagePayload<NetworkDestroyInstance<T>> payload)
    {
        if (!ObjectManager.TryGetObjectWithLogging(payload.What.InstanceId, out T obj)) return;

        if (Registry.Debug)
        {
            Logger.Debug("[Client][{CallingMethod}] Destroyed instance of {type} with id {id}",
                $"{nameof(AutoRegistryHandler<T>)}.{nameof(Handle_NetworkDestroyInstance)}",
                typeof(T).Name,
                payload.What.InstanceId);
        }

        // Callback before object is removed from registry
        Registry.OnClientDestroyed(obj, payload.What.InstanceId);

        ObjectManager.Remove(obj);
    }
}