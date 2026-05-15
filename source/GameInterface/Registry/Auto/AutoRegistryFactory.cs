using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Registry.Auto;
public interface IAutoRegistryFactory : IDisposable
{
    void AddRegistry<T>(AutoRegistryBase<T> autoRegistry) where T : class;

    void RegisterAll();
}

internal class AutoRegistryFactory : IAutoRegistryFactory
{
    const string HarmonyID = "CoopAutoRegistryFactory";
    private readonly Harmony harmony = new Harmony(HarmonyID);

    IRegistryCollection Collection { get; }
    IMessageBroker MessageBroker { get; }
    INetwork Network { get; }
    IAutoSyncPatchCollector SyncPatchCollector { get; }
    IObjectManager ObjectManager { get; }
    ISerializableTypeMapper TypeMapper { get; }
    List<IDisposable> Disposables { get; } = new List<IDisposable>();

    List<Action> RegisterAllCallbacks = new List<Action>();

    public AutoRegistryFactory(
        IRegistryCollection collection,
        IMessageBroker messageBroker,
        INetwork network,
        IAutoSyncPatchCollector syncPatchCollector,
        IObjectManager objectManager,
        ISerializableTypeMapper typeMapper)
    {
        Collection = collection;
        MessageBroker = messageBroker;
        Network = network;
        SyncPatchCollector = syncPatchCollector;
        ObjectManager = objectManager;
        TypeMapper = typeMapper;
    }

    public void Dispose()
    {
        Disposables.ForEach(disposable => disposable.Dispose());
    }

    public void AddRegistry<T>(AutoRegistryBase<T> autoRegistry) where T : class
    {
        ValidateConstructorTypes(autoRegistry.Constructors, typeof(T));

        TypeMapper.AddTypes(new Type[] { 
            typeof(NetworkCreateInstance<T>),
            typeof(NetworkDestroyInstance<T>)
        });

        var handler = new AutoRegistryHandler<T>(
            autoRegistry,
            MessageBroker,
            Network,
            ObjectManager
        );

        foreach (var ctor in autoRegistry.Constructors)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches<T>), nameof(LifetimePatches<T>.CreatePrefix));

            SyncPatchCollector.AddPrefix(ctor, patch);
        }

        foreach (var destroy in autoRegistry.DestroyMethods)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches<T>), nameof(LifetimePatches<T>.DestroyPostfix));

            SyncPatchCollector.AddPostfix(destroy, patch);
        }

        RegisterAllCallbacks.Add(autoRegistry.RegisterAllObjects);
        Disposables.Add(handler);
    }

    public void RegisterAll()
    {
        foreach (var callback in RegisterAllCallbacks)
        {
            callback();
        }
    }

    private void ValidateConstructorTypes(IEnumerable<MethodBase> ctros, Type expectedType)
    {
        var exceptions = ctros.Where(ctor => expectedType.IsAssignableFrom(ctor?.DeclaringType) == false).Select(ctor =>
        {
            return new InvalidOperationException($"Constructor does not match type {expectedType} instead was {ctor.DeclaringType}");
        });

        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }
}