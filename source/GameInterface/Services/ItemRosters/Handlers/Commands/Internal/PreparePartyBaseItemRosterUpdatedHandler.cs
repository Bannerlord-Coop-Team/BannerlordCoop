using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ItemRosters.Messages.Commands.Internal;
using GameInterface.Services.ItemRosters.Messages.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.ItemRosters.Handlers.Commands.Internal
{
    internal class PreparePartyBaseItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IBinaryPackageFactory binaryPackageFactory;

        public PreparePartyBaseItemRosterUpdatedHandler(IMessageBroker broker, IBinaryPackageFactory factory)
        {
            messageBroker = broker;
            binaryPackageFactory = factory;
            messageBroker.Subscribe<PreparePartyBaseItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<PreparePartyBaseItemRosterUpdated> payload)
        {
            var equipment_element = binaryPackageFactory.GetBinaryPackage<EquipmentElementBinaryPackage>(payload.What.EquipmentElement);

            var msg = new PartyBaseItemRosterUpdated(
                payload.What.PartyBaseId, 
                BinaryFormatterSerializer.Serialize(equipment_element), 
                payload.What.Number);

            messageBroker.Publish(this, msg);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PreparePartyBaseItemRosterUpdated>(Handle);
        }
    }
}
