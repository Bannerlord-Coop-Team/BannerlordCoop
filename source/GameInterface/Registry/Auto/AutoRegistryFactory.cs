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
    bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, IEnumerable<MethodBase> destroyMethods, Action<AutoRegistry<T>> registerAll, AutoRegistryCallbacks<T> callbacks) where T : class;

    void RegisterType<T>(IAutoRegistry<T> autoRegistry) where T : class;
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

    public void RegisterType<T>(IAutoRegistry<T> autoRegistry) where T : class
    {
        var callbacks = new AutoRegistryCallbacks<T>(autoRegistry);

        TryRegisterType(autoRegistry.Constructors, autoRegistry.DestroyMethods, autoRegistry.RegisterAllObjects, callbacks);
    }

    public bool TryRegisterType<T>(
        IEnumerable<MethodBase> ctrosToPatch,
        IEnumerable<MethodBase> destroyMethods,
        Action<AutoRegistry<T>> registerAll,
        AutoRegistryCallbacks<T> callbacks
    ) where T : class
    {
        ValidateConstructorTypes(ctrosToPatch, typeof(T));
        ValidateConstructorTypes(destroyMethods, typeof(T));

        TypeMapper.AddTypes(new Type[] { typeof(NetworkCreateInstance<T>) });

        var registry = new AutoRegistry<T>(registerAll, Collection);
        var handler = new AutoRegistryHandler<T>(
            registry,
            MessageBroker,
            Network,
            ObjectManager,
            callbacks
        );

        foreach (var ctor in ctrosToPatch)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches), nameof(LifetimePatches.CreatePrefix));

            SyncPatchCollector.AddPrefix(ctor, patch);
        }

        foreach (var destroy in destroyMethods)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches), nameof(LifetimePatches.DestroyPostfix));

            SyncPatchCollector.AddPostfix(destroy, patch);
        }

        Disposables.Add(registry);
        Disposables.Add(handler);

        return true;
    }

    private void ValidateConstructorTypes(IEnumerable<MethodBase> ctros, Type expectedType)
    {
        var exceptions = ctros.Where(ctor => expectedType.IsAssignableFrom(ctor?.DeclaringType) == false).Select(ctor =>
        {
            return new InvalidOperationException($"{ctor.DeclaringType} is not assignable to {expectedType}");
        });

        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }
}