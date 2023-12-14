using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ItemRosters.Messages.Commands.Internal;
using GameInterface.Services.ItemRosters.Messages.Events;

namespace GameInterface.Services.ItemRosters.Handlers.Commands.Internal
{
    internal class PrepareItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IBinaryPackageFactory binaryPackageFactory;

        public PrepareItemRosterUpdatedHandler(IMessageBroker broker, IBinaryPackageFactory factory)
        {
            messageBroker = broker;
            binaryPackageFactory = factory;
            messageBroker.Subscribe<PrepareItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<PrepareItemRosterUpdated> payload)
        {
            var equipment_element = binaryPackageFactory.GetBinaryPackage<EquipmentElementBinaryPackage>(payload.What.EquipmentElement);

            var msg = new ItemRosterUpdated(
                payload.What.PartyBaseId, 
                BinaryFormatterSerializer.Serialize(equipment_element), 
                payload.What.Number);

            messageBroker.Publish(this, msg);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PrepareItemRosterUpdated>(Handle);
        }
    }
}
