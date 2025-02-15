using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.AutoSync.Registry;


public interface IAutoRegistryFactory : IDisposable
{
    bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, Action<AutoRegistry<T>> registerAll, Action<T> onClientCreated = null) where T : class;
    void PatchAll();
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
    List<(MethodBase, MethodInfo)> Patches { get; } = new List<(MethodBase, MethodInfo)>();

    public AutoRegistryFactory(
        IRegistryCollection collection,
        IMessageBroker messageBroker,
        INetwork network,
        IAutoSyncPatchCollector syncPatchCollector,
        IObjectManager objectManager,
        ISerializableTypeMapper typeMapper )
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

    public bool TryRegisterType<T>(IEnumerable<MethodBase> ctrosToPatch, Action<AutoRegistry<T>> registerAll, Action<T> onClientCreated = null) where T : class
    {
        TypeMapper.AddTypes(new Type[] { typeof(NetworkCreateInstance<T>) });

        var registry = new AutoRegistry<T>(registerAll, Collection);
        var handler = new AutoRegistryHandler<T>(registry, MessageBroker, Network, ObjectManager, onClientCreated);

        foreach (var ctor in ctrosToPatch)
        {
            var patch = AccessTools.Method(typeof(LifetimePatches), nameof(LifetimePatches.Prefix)).MakeGenericMethod(typeof(T));

            Patches.Add((ctor, patch));
        }
        

        Disposables.Add(registry);
        Disposables.Add(handler);

        return true;
    }

    public void PatchAll()
    {
        if (Harmony.HasAnyPatches(HarmonyID)) return;

        foreach (var (ctor, patch) in Patches)
        {
            harmony.Patch(ctor, patch);
        }

        Patches.Clear();
    }
}

public class AutoRegistry<T> : RegistryBase<T> where T : class
{
    readonly static string InstanceId = $"Coop{typeof(T)}";

    static int InstanceCounter = 0;

    public Action<AutoRegistry<T>> RegisterAllCallback { get; }

    public AutoRegistry(Action<AutoRegistry<T>> registerAllCallback, IRegistryCollection collection) : base(collection)
    {
        RegisterAllCallback = registerAllCallback;
    }


    public override void RegisterAll()
    {
        RegisterAllCallback(this);
    }

    protected override string GetNewId(T obj)
    {
        return $"{InstanceId}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

class AutoRegistryHandler<T> : IHandler where T : class
{
    public AutoRegistry<T> Registry { get; }
    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IObjectManager ObjectManager { get; }
    public Action<T> ClientCreatedCallback { get; }

    public AutoRegistryHandler(AutoRegistry<T> registry, IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, Action<T> clientCreatedCallback = null)
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

        Network.SendAll(new NetworkCreateInstance<T>(id));
    }

    private void Handle_NetworkCreateInstance(MessagePayload<NetworkCreateInstance<T>> payload)
    {
        var newInstance = ObjectHelper.SkipConstructor<T>();

        ObjectManager.AddExisting(payload.What.InstanceId, newInstance);

        if (ClientCreatedCallback != null) ClientCreatedCallback(newInstance);
    }
}


class InstanceCreated<T> : IEvent where T : class
{
    public T Instance { get; }

    public InstanceCreated(T instance)
    {
        Instance = instance;
    }
}

[ProtoContract(SkipConstructor = true)]
class NetworkCreateInstance<T> : ICommand where T : class
{
    [ProtoMember(1)]
    public string InstanceId { get; }

    public NetworkCreateInstance(string instanceId)
    {
        InstanceId = instanceId;
    }
}


internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    internal static bool Prefix<T>(ref T __instance) where T : class
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var message = new InstanceCreated<T>(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}