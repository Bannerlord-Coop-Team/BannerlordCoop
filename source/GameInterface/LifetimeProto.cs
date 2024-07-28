using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using HarmonyLib;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using static SandBox.Missions.MissionLogics.HideoutMissionController;

namespace GameInterface;

public class TestObject { }


internal class LifetimeProto<T> : IDisposable where T : class
{
    static readonly ILogger Logger = LogManager.GetLogger<LifetimeProto<T>>();

    public static bool Patched = false;
    private LifetimeHandler lifetimeHandler;
    private LifetimeRegistry lifetimeRegistry;

    public LifetimeProto(
        Harmony harmony,
        IMessageBroker messageBroker, 
        INetwork network, 
        IObjectManager objectManager, 
        IRegistryCollection registryCollection)
    {
        lifetimeHandler = new LifetimeHandler(messageBroker, network, objectManager);
        lifetimeRegistry = new LifetimeRegistry(registryCollection);

        Patch(harmony);
    }

    private void Patch(Harmony harmony)
    {
        if (Patched) return;
        Patched = true;

        var prefix = AccessTools.Method(typeof(LifetimeProto<T>), nameof(Prefix));
        harmony.Patch(AccessTools.Constructor(typeof(T)), prefix: new HarmonyMethod(prefix));
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

    private static bool Prefix(T __instance)
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
