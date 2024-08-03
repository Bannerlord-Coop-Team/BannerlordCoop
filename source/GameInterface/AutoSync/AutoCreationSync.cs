using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.AutoSync;

internal class AutoCreationSync<T> : IDisposable where T : class
{
    static readonly ILogger Logger = LogManager.GetLogger<AutoCreationSync<T>>();
    private readonly MethodBase destroyFunction;
    private LifetimeHandler lifetimeHandler;
    private LifetimeRegistry lifetimeRegistry;

    public AutoCreationSync(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IRegistryCollection registryCollection,
        IAutoSyncPatcher autoSyncPatcher)
    {
        lifetimeHandler = new LifetimeHandler(messageBroker, network, objectManager);
        lifetimeRegistry = new LifetimeRegistry(registryCollection);

        var prefix = AccessTools.Method(typeof(AutoCreationSync<T>), nameof(CreationPrefix));
        foreach(var ctor in AccessTools.GetDeclaredConstructors(typeof(T)))
        {
            autoSyncPatcher.AddPrefix(ctor, prefix);
        }
    }

    public void Dispose()
    {
        lifetimeHandler.Dispose();
    }

    class LifetimeRegistry : RegistryBase<T>
    {
        public LifetimeRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
            // not required
        }

        protected override string GetNewId(T obj)
        {
            return Guid.NewGuid().ToString();
        }
    }

    class LifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;


        public LifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ObjectCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateObject>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ObjectCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateObject>(Handle);
        }

        private void Handle(MessagePayload<ObjectCreated> payload)
        {
            objectManager.AddNewObject(payload.What.Instance, out var id);

            network.SendAll(new NetworkCreateObject(id));
        }


        private void Handle(MessagePayload<NetworkCreateObject> payload)
        {
            var newInstance = ObjectHelper.SkipConstructor<T>();

            objectManager.AddExisting(payload.What.InstanceId, newInstance);
        }
    }

    private static bool CreationPrefix(ref T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
            + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new ObjectCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    class ObjectCreated : IEvent
    {
        public ObjectCreated(T instance)
        {
            Instance = instance;
        }

        public T Instance { get; }
    }

    [ProtoContract(SkipConstructor = true)]
    class NetworkCreateObject : ICommand
    {
        public NetworkCreateObject(string instanceId)
        {
            InstanceId = instanceId;
        }

        [ProtoMember(1)]
        public string InstanceId { get; }
    }
}
