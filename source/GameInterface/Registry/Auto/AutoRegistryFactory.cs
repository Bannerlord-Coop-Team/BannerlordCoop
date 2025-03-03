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
    bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, Action<AutoRegistry<T>> registerAll, Action<string, T> onClientCreated = null) where T : class;

    void RegisterType<T>(IAutoRegistry<T> autoRegistry);
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

    public bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, Action<AutoRegistry<T>> registerAll, Action<string, T> onClientCreated = null) where T : class
    {
        ValidateConstructorTypes(ctrosToPatch, typeof(T));

        TypeMapper.AddTypes(new Type[] { typeof(NetworkCreateInstance<T>) });

        var registry = new AutoRegistry<T>(registerAll, Collection);
        var handler = new AutoRegistryHandler<T>(registry, MessageBroker, Network, ObjectManager, onClientCreated);

        foreach (var ctor in ctrosToPatch)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches), nameof(LifetimePatches.Prefix)).MakeGenericMethod(typeof(T));

            SyncPatchCollector.AddPrefix(ctor, patch);
        }


        Disposables.Add(registry);
        Disposables.Add(handler);

        return true;
    }

    private void ValidateConstructorTypes(IEnumerable<MethodBase> ctros, Type expectedType)
    {
        var exceptions = ctros.Where(ctor => ctor.DeclaringType != expectedType).Select(ctor =>
        {
            return new InvalidOperationException($"Constructor does not match type {expectedType} instead was {ctor.DeclaringType}");
        });

        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }
}