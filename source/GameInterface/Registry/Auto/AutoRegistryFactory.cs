using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Util;
using GameInterface.AutoSync;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.ObjectSystem;

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

    /// <summary>
    /// Creates an uninitialized client instance of a managed type and runs its registry's
    /// <c>OnClientCreated</c> callback. The object is deliberately not registered, allowing an aggregate
    /// receive path to construct and wire a complete graph before registering it atomically.
    /// </summary>
    /// <param name="type">The concrete runtime type to create.</param>
    /// <param name="fullId">The full ObjectManager id, including its type prefix.</param>
    /// <param name="obj">The created object, or null when the type is not managed or creation fails.</param>
    bool TryCreateClientObject(Type type, string fullId, out object obj);
}

internal class AutoRegistryFactory : IAutoRegistryFactory
{
    private static readonly ILogger Logger = LogManager.GetLogger<AutoRegistryFactory>();

    const string HarmonyID = "CoopAutoRegistryFactory";
    private readonly Harmony harmony = new Harmony(HarmonyID);

    IRegistryCollection Collection { get; }
    IMessageBroker MessageBroker { get; }
    INetwork Network { get; }
    IAutoSyncPatchCollector PatchCollector { get; }
    IObjectManager ObjectManager { get; }
    ISerializableTypeMapper TypeMapper { get; }
    IMapEventInitializationTracker MapEventInitializationTracker { get; }
    List<IDisposable> Disposables { get; } = new List<IDisposable>();

    List<Action<IDictionary<string, string>>> RegisterAllCallbacks = new List<Action<IDictionary<string, string>>>();

    List<Action<IDictionary<string, string>>> IdRemapCallbacks = new List<Action<IDictionary<string, string>>>();

    private IDictionary<string, string> joinIdRemap;

    private readonly HashSet<Type> managedTypes = new HashSet<Type>();

    private readonly Dictionary<Type, Action<object, string>> clientCreatedCallbacks =
        new Dictionary<Type, Action<object, string>>();

    public AutoRegistryFactory(
        IRegistryCollection collection,
        IMessageBroker messageBroker,
        INetwork network,
        IAutoSyncPatchCollector syncPatchCollector,
        IObjectManager objectManager,
        ISerializableTypeMapper typeMapper,
        IMapEventInitializationTracker mapEventInitializationTracker)
    {
        Collection = collection;
        MessageBroker = messageBroker;
        Network = network;
        PatchCollector = syncPatchCollector;
        ObjectManager = objectManager;
        TypeMapper = typeMapper;
        MapEventInitializationTracker = mapEventInitializationTracker;
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

    public bool TryCreateClientObject(Type type, string fullId, out object obj)
    {
        obj = null;
        if (type == null || type.IsAbstract || type.IsInterface || string.IsNullOrEmpty(fullId))
            return false;

        Action<object, string> onClientCreated = null;
        for (var current = type; current != null; current = current.BaseType)
        {
            if (clientCreatedCallbacks.TryGetValue(current, out onClientCreated))
                break;
        }

        if (onClientCreated == null) return false;

        try
        {
            obj = ObjectHelper.SkipConstructor(type);

            var prefix = type.Name + "_";
            var callbackId = fullId.StartsWith(prefix, StringComparison.Ordinal)
                ? fullId.Substring(prefix.Length)
                : fullId;

            if (obj is MBObjectBase mbObject)
            {
                using (new AllowedThread())
                {
                    mbObject.StringId = callbackId;
                }
            }

            onClientCreated(obj, callbackId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex,
                "Failed to create unregistered client object of type {ObjectType} with id {ObjectId}",
                type,
                fullId);
            obj = null;
            return false;
        }
    }

    public void AddRegistry<T>(AutoRegistryBase<T> autoRegistry) where T : class
    {
        managedTypes.Add(typeof(T));
        clientCreatedCallbacks.Add(
            typeof(T),
            (obj, id) => autoRegistry.OnClientCreated((T)obj, id));

        ValidateConstructorTypes(autoRegistry.Constructors, typeof(T));

        TypeMapper.AddTypes(new Type[] { 
            typeof(NetworkCreateInstance<T>),
            typeof(NetworkDestroyInstance<T>)
        });

        var handler = new AutoRegistryHandler<T>(
            autoRegistry,
            MessageBroker,
            Network,
            ObjectManager,
            MapEventInitializationTracker
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
