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

namespace GameInterface.AutoSync.Internal;

internal class AutoDeletionSync<T> : IDisposable where T : class
{
    static readonly ILogger Logger = LogManager.GetLogger<AutoDeletionSync<T>>();
    private readonly MethodBase destroyFunction;
    private readonly DestructionHandler lifetimeHandler;
    private readonly Harmony harmony = new Harmony(nameof(AutoDeletionSync<T>));

    public AutoDeletionSync(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IAutoSyncPatcher autoSyncPatcher,
        MethodBase deletionFunction)
    {
        destroyFunction = deletionFunction;

        lifetimeHandler = new DestructionHandler(messageBroker, network, objectManager);

        var prefix = AccessTools.Method(typeof(AutoDeletionSync<T>), nameof(DeletionPrefix));
        autoSyncPatcher.AddPrefix(deletionFunction, prefix);
    }

    public void Dispose()
    {
        lifetimeHandler.Dispose();

        harmony.UnpatchAll(nameof(AutoDeletionSync<T>));
    }

    class DestructionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;

        public DestructionHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ObjectDestroyed>(Handle);
            messageBroker.Subscribe<NetworkDestroyObject>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ObjectDestroyed>(Handle);
            messageBroker.Unsubscribe<NetworkDestroyObject>(Handle);
        }

        private void Handle(MessagePayload<ObjectDestroyed> payload)
        {
            if (objectManager.TryGetId(payload.What.Instance, out var id) == false) return;

            objectManager.Remove(payload.What.Instance);

            network.SendAll(new NetworkDestroyObject(id));
        }


        private void Handle(MessagePayload<NetworkDestroyObject> payload)
        {
            if (objectManager.TryGetObject<T>(payload.What.InstanceId, out var obj) == false) return;

            objectManager.Remove(obj);
        }
    }

    private static bool DeletionPrefix(T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
            + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new ObjectDestroyed(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    class ObjectDestroyed : IEvent
    {
        public ObjectDestroyed(T instance)
        {
            Instance = instance;
        }

        public T Instance { get; }
    }

    [ProtoContract(SkipConstructor = true)]
    class NetworkDestroyObject : ICommand
    {
        public NetworkDestroyObject(string instanceId)
        {
            InstanceId = instanceId;
        }

        [ProtoMember(1)]
        public string InstanceId { get; }
    }
}
