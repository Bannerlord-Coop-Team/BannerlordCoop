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
internal class AutoFieldSync<T, ValueType> : IDisposable where T : class
{

    static readonly ILogger Logger = LogManager.GetLogger<AutoFieldSync<T, ValueType>>();
    private PropertyHandler propertyHandler;

    public AutoFieldSync(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IAutoSyncPatcher autoSyncPatcher,
        IAutoSyncPropertyMapper propertyMapper,
        FieldInfo fieldInfo)
    {
        int setterId = propertyMapper.AddPropertySetter(propertySetter);

        propertyHandler = new PropertyHandler(messageBroker, network, objectManager, propertyMapper, setterId);

        var prefix = AccessTools.Method(typeof(AutoFieldSync<T, ValueType>), nameof(SetterPrefix));
        autoSyncPatcher.AddPrefix(propertySetter, prefix);
    }

    public void Dispose()
    {
        propertyHandler.Dispose();
    }

    class PropertyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly IAutoSyncPropertyMapper propertyMapper;
        private readonly int fieldId;

        public PropertyHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, IAutoSyncPropertyMapper propertyMapper, int fieldId)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            this.propertyMapper = propertyMapper;
            this.fieldId = fieldId;
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
            if (objectManager.TryGetObject(payload.What.InstanceId, out T instance) == false)
            {
                Logger.Error($"Unable to resolve instance for {payload.What.InstanceId}");
                return;
            }

            var setter = propertyMapper.GetSetter(fieldId);

            setter.Invoke(instance, new object[] { payload.What.Value });
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
            Value = value;
        }

        [ProtoMember(1)]
        public string InstanceId { get; }

        [ProtoMember(2)]
        public ValueType Value { get; }
    }
}

