using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Registry.Auto;
public interface IAutoRegistryFactory : IDisposable
{
    bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, IEnumerable<MethodBase> destroyMethods, Action<AutoRegistry<T>> registerAll, AutoRegistryCallbacks<T> callbacks) where T : class;

    void RegisterType<T>(IAutoRegistry<T> autoRegistry) where T : class;
    void BuildPatches();
}

internal class AutoRegistryFactory : IAutoRegistryFactory
{
    private readonly IAutoRegistryPatchCreator patchCreator;

    IRegistryCollection Collection { get; }
    IMessageBroker MessageBroker { get; }
    INetwork Network { get; }
    IAutoSyncPatchCollector SyncPatchCollector { get; }
    IObjectManager ObjectManager { get; }
    ISerializableTypeMapper TypeMapper { get; }

    List<IDisposable> Disposables { get; } = new List<IDisposable>();

    HashSet<(MethodBase, Type)> CtorsToPatch = new HashSet<(MethodBase, Type)>();
    HashSet<(MethodBase, Type)> DestuctorsToPatch = new HashSet<(MethodBase, Type)>();

    public AutoRegistryFactory(
        IRegistryCollection collection,
        IMessageBroker messageBroker,
        INetwork network,
        IAutoSyncPatchCollector syncPatchCollector,
        IAutoRegistryPatchCreator patchCreator,
        IObjectManager objectManager,
        ISerializableTypeMapper typeMapper)
    {
        Collection = collection;
        MessageBroker = messageBroker;
        Network = network;
        SyncPatchCollector = syncPatchCollector;
        this.patchCreator = patchCreator;
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

        if (typeof(T).IsInterface) throw new InvalidOperationException("Interfaces are not supported yet");

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
            var tuple = (ctor, typeof(T));

            if (CtorsToPatch.Contains(tuple)) throw new InvalidOperationException($"{ctor} is already reporting for {typeof(T).Name}");
            CtorsToPatch.Add(tuple);
        }

        foreach (var destroy in destroyMethods)
        {
            var tuple = (destroy, typeof(T));

            if (DestuctorsToPatch.Contains(tuple)) throw new InvalidOperationException($"{tuple} is already reporting for {typeof(T).Name}");
            DestuctorsToPatch.Add(tuple);
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

    public void BuildPatches()
    {
        var prefixNameMap = new Dictionary<Type, string>();
        var postfixNameMap = new Dictionary<Type, string>();

        foreach (var type in CtorsToPatch.Select(tuple => tuple.Item2).Distinct())
        {
            prefixNameMap.Add(type, patchCreator.AddCreateEvent(type));
        }

        foreach (var type in DestuctorsToPatch.Select(tuple => tuple.Item2).Distinct())
        {
            postfixNameMap.Add(type, patchCreator.AddDestroyEvent(type));
        }

        var dynamicType = patchCreator.Build();

        foreach (var (ctor, type) in CtorsToPatch)
        {
            var patch = dynamicType.GetMethod(prefixNameMap[type]);
            SyncPatchCollector.AddPrefix(ctor, patch);
        }

        foreach (var (dtor, type) in DestuctorsToPatch)
        {
            var patch = dynamicType.GetMethod(postfixNameMap[type]);
            SyncPatchCollector.AddPostfix(dtor, patch);
        }
    }
}