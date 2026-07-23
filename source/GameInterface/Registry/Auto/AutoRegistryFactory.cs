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

    /// <summary>
    /// Server: collect the owner-derived id -> current server id for every registry-managed object whose
    /// live-create id diverges from the id a joining client re-derives in RegisterAllObjects. Reuses each
    /// registry's own RegisterAllObjects so the id formula has a single source of truth.
    /// </summary>
    void BuildIdRemap(IDictionary<string, string> map);

    /// <summary>
    /// Joining client: stash the server's owner-derived id -> server id map so the next <see cref="RegisterAll"/>
    /// registers each live-created attachment directly under the server's id. Consumed and cleared by RegisterAll.
    /// </summary>
    void SetJoinIdRemap(IDictionary<string, string> map);

    bool IsManaged(Type type);
}

internal class AutoRegistryFactory : IAutoRegistryFactory
{
    const string HarmonyID = "CoopAutoRegistryFactory";
    private readonly Harmony harmony = new Harmony(HarmonyID);

    IRegistryCollection Collection { get; }
    IMessageBroker MessageBroker { get; }
    INetwork Network { get; }
    IAutoSyncPatchCollector PatchCollector { get; }
    IObjectManager ObjectManager { get; }
    ISerializableTypeMapper TypeMapper { get; }
    List<IDisposable> Disposables { get; } = new List<IDisposable>();

    List<Action<IDictionary<string, string>>> RegisterAllCallbacks = new List<Action<IDictionary<string, string>>>();

    List<Action<IDictionary<string, string>>> IdRemapCallbacks = new List<Action<IDictionary<string, string>>>();

    private IDictionary<string, string> joinIdRemap;

    private readonly HashSet<Type> managedTypes = new HashSet<Type>();

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
        PatchCollector = syncPatchCollector;
        ObjectManager = objectManager;
        TypeMapper = typeMapper;
    }

    public void Dispose()
    {
        Disposables.ForEach(disposable => disposable.Dispose());
    }

    // True if the type or any of its base types is registry-managed (the ObjectManager tracks it by id). Walks
    // the base chain so a concrete subclass of a registered base (e.g. a PartyComponent) is also caught.
    public bool IsManaged(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (managedTypes.Contains(current) || Collection.RegistryMap.ContainsKey(current)) return true;
        return false;
    }

    public void AddRegistry<T>(AutoRegistryBase<T> autoRegistry) where T : class
    {
        managedTypes.Add(typeof(T));

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

            PatchCollector.AddPrefix(ctor, patch);
        }

        foreach (var destroy in autoRegistry.DestroyMethods)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches<T>), nameof(LifetimePatches<T>.DestroyPostfix));

            PatchCollector.AddPostfix(destroy, patch);
        }

        RegisterAllCallbacks.Add(autoRegistry.RegisterAllObjectsWithRemap);
        IdRemapCallbacks.Add(autoRegistry.CollectIdRemap);
        Disposables.Add(handler);
    }

    public void SetJoinIdRemap(IDictionary<string, string> map)
    {
        joinIdRemap = map;
    }

    public void RegisterAll()
    {
        try
        {
            foreach (var callback in RegisterAllCallbacks)
            {
                callback(joinIdRemap);
            }
        }
        finally
        {
            joinIdRemap = null;
        }
    }

    public void BuildIdRemap(IDictionary<string, string> map)
    {
        foreach (var callback in IdRemapCallbacks)
        {
            callback(map);
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