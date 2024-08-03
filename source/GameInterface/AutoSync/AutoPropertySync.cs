using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.AutoSync;
internal class AutoPropertySync<T, ValueType> : IDisposable where T : class
{

    static readonly ILogger Logger = LogManager.GetLogger<AutoPropertySync<T, ValueType>>();
    private PropertyHandler lifetimeHandler;

    public AutoPropertySync(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IAutoSyncPatcher autoSyncPatcher,
        IAutoSyncTypeMapper autoSyncTypeMapper,
        MethodInfo propertySetter)
    {
        autoSyncTypeMapper.AddType(typeof(ValueType));

        lifetimeHandler = new PropertyHandler(messageBroker, network, objectManager);

        var prefix = AccessTools.Method(typeof(AutoPropertySync<T, ValueType>), nameof(SetterPrefix));
        autoSyncPatcher.AddPrefix(propertySetter, prefix);
    }

    public void Dispose()
    {
        lifetimeHandler.Dispose();
    }

    class PropertyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;


        public PropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {


            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<PropertyChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeProperty>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PropertyChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeProperty>(Handle);
        }

        private void Handle(MessagePayload<PropertyChanged> payload)
        {
            if (objectManager.TryGetId(payload.What.Instance, out var id) == false)
            {
                Logger.Error($"Unable to resolve id for {payload.What.Instance}");
                return;
            }

            var value = payload.What.Value;

            network.SendAll(new NetworkChangeProperty(id, value));
        }


        private void Handle(MessagePayload<NetworkChangeProperty> payload)
        {
            var newInstance = ObjectHelper.SkipConstructor<T>();

            objectManager.AddExisting(payload.What.InstanceId, newInstance);
        }
    }

    private static bool SetterPrefix(T __instance, ValueType value)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
            + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return true;
        }

        var message = new PropertyChanged(__instance, value);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    class PropertyChanged : IEvent
    {
        public PropertyChanged(T instance, ValueType value)
        {
            Instance = instance;
            Value = value;
        }

        public T Instance { get; }
        public ValueType Value { get; }
    }

    [ProtoContract(SkipConstructor = true)]
    class NetworkChangeProperty : ICommand
    {
        public NetworkChangeProperty(string instanceId, ValueType value)
        {
            InstanceId = instanceId;
        }

        [ProtoMember(1)]
        public string InstanceId { get; }

        [ProtoMember(2)]
        public ValueType Value { get; }
    }
}

