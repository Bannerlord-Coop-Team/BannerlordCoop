using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface;
internal class LifetimeSync<T> : IDisposable where T : class
{
    private static bool Initialized = false;
    private readonly IRegistry<T> registry;

    private readonly IHandler handler;
    private readonly Harmony harmony;
    public LifetimeSync(
        IRegistryCollection registries,
        IObjectManager objectManager,
        INetwork network,
        IMessageBroker messageBroker,
        Harmony harmony)
    {
        registry = new Registry(registries);
        handler = new Handler(objectManager, network, messageBroker);

        this.harmony = harmony;

        // Needed for when multiple instances are created, only during testing
        if (Initialized) return;

        var patchMethod = new HarmonyMethod(AccessTools.Method(typeof(LifetimeSync<T>), nameof(Prefix)));
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(T)))
        {
            harmony.Patch(ctor, prefix: patchMethod);
        }

        Initialized = true;
    }

    public void Dispose()
    {
        // Needed for when multiple instances are created, only during testing
        if (!Initialized) return;

        var prefix = AccessTools.Method(typeof(LifetimeSync<T>), nameof(Prefix));
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(T)))
        {
            harmony.Unpatch(ctor, prefix);
        }

        Initialized = false;
    }

    class Registry : RegistryBase<T>
    {
        public Registry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            // Not needed
        }

        protected override string GetNewId(T obj)
        {
            return Guid.NewGuid().ToString();
        }
    }

  
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideCreationPatches>();

    static bool Prefix(T __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(T), Environment.StackTrace);
            return true;
        }

        var message = new ObjectCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    class ObjectCreated : IEvent
    {
        public T Instance { get; }

        public ObjectCreated(T instance)
        {
            Instance = instance;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    class NetworkCreateObject : ICommand
    {
        [ProtoMember(1)]
        public string ObjectId { get; }
        
        public NetworkCreateObject(string objectId)
        {
            ObjectId = objectId;
        }
    }

    class Handler : IHandler
    {
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IMessageBroker messageBroker;

        public Handler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker)
        {
            this.objectManager = objectManager;
            this.network = network;
            this.messageBroker = messageBroker;

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
            if (objectManager.TryGetId(payload.What.Instance, out var id) == false) return;

            var message = new NetworkCreateObject(id);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateObject> payload)
        {
            var obj = ObjectHelper.SkipConstructor<T>();

            objectManager.AddExisting(payload.What.ObjectId, obj);
        }
    }
}
