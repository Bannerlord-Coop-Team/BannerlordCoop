using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.AutoSyncPOC.Mapper;
using GameInterface.AutoSyncPOC.Messages;
using HarmonyLib;
using Serilog;

namespace GameInterface.AutoSyncPOC.Handlers
{
    internal class AutoSyncFieldHandler : IHandler
    {
        private readonly ILogger logger;
        private readonly IMessageBroker messageBroker;
        private readonly IAutoSyncFieldMapper fieldMapper;
        private readonly INetworkIdRegistry networkIdRegistry;
        private readonly IFieldRegistry fieldRegistry;

        public AutoSyncFieldHandler(
            ILogger logger,
            IMessageBroker messageBroker,
            IAutoSyncFieldMapper fieldMapper,
            INetworkIdRegistry networkIdRegistry,
            IFieldRegistry fieldRegistry)
        {
            this.logger = logger;
            this.messageBroker = messageBroker;
            this.fieldMapper = fieldMapper;
            this.networkIdRegistry = networkIdRegistry;
            this.fieldRegistry = fieldRegistry;

            messageBroker.Subscribe<SetFieldCommand>(Handle_FieldSetCommand);
            messageBroker.Subscribe<SetFieldNull>(Handle_FieldSetNullCommand);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SetFieldCommand>(Handle_FieldSetCommand);
        }

        internal void Handle_FieldSetCommand(MessagePayload<SetFieldCommand> payload)
        {
            var message = payload.What;
            var fieldMap = message.FieldMap;

            if (!networkIdRegistry.TryGetObject(fieldMap.NetworkId, out var instance))
            {
                logger.Error("Failed to resolve instance from {id}", fieldMap.NetworkId);
                return;
            }

            if (!fieldRegistry.TryGetField(fieldMap.FieldId, out var field))
            {
                logger.Error("Failed to resolve field from {id}", fieldMap.FieldId);
                return;
            }

            object value = null;
            if (networkIdRegistry.IsTypeManaged(field.FieldType))
            {
                var fieldNetworkId = RawSerializer.Deserialize<ulong>(message.SerializedValue);
                if (!networkIdRegistry.TryGetObject(fieldNetworkId, out value))
                {
                    logger.Error("Failed to resolve field reference for {networkId}", fieldNetworkId);
                    return;
                }
            }
            else
            {
                // TODO make into a cache
                var deserializeMethodInfo = AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Deserialize)).MakeGenericMethod(field.FieldType);
                value = deserializeMethodInfo.Invoke(null, new object[] { message.SerializedValue });
            }

            field.SetValue(instance, value);
        }

        internal void Handle_FieldSetNullCommand(MessagePayload<SetFieldNull> payload)
        {
            var message = payload.What;
            var fieldMap = message.FieldMap;

            if (!networkIdRegistry.TryGetObject(fieldMap.NetworkId, out var instance))
            {
                logger.Error("Failed to resolve instance from {id}", fieldMap.NetworkId);
                return;
            }

            if (!fieldRegistry.TryGetField(fieldMap.FieldId, out var field))
            {
                logger.Error("Failed to resolve field from {id}", fieldMap.FieldId);
                return;
            }

            field.SetValue(instance, null);
        }
    }
}
