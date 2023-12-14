using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization;
using GameInterface.Services.ItemRosters.Messages.Events;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Handlers.Events
{
    /// <summary>
    /// Handles PartyBaseItemRosterUpdated on client.
    /// </summary>
    internal class PartyBaseItemRosterUpdatedHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IBinaryPackageFactory binaryPackageFactory;

        public PartyBaseItemRosterUpdatedHandler(IMessageBroker broker, IBinaryPackageFactory factory)
        {
            if (ModInformation.IsServer)
                return;

            messageBroker = broker;
            binaryPackageFactory = factory;
            messageBroker.Subscribe<PartyBaseItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<PartyBaseItemRosterUpdated> payload)
        {
            var package_ee = BinaryFormatterSerializer.Deserialize<EquipmentElementBinaryPackage>(payload.What.EquipmentElement);
            var equipmentElement = package_ee.Unpack<EquipmentElement>(binaryPackageFactory);

            //TODO: lookup settlement or mobile party by it's ID
            //.ItemRoster.AddToCounts(equipmentElement, payload.What.Number);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyBaseItemRosterUpdated>(Handle);
        }
    }
}
