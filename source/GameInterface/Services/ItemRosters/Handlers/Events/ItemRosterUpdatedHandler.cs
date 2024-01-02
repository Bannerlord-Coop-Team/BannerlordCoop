using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization;
using GameInterface.Services.ItemRosters.Messages.Events;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using GameInterface.Services.MobileParties;
using TaleWorlds.CampaignSystem;
using System;
using Serilog;
using Common.Logging;

namespace GameInterface.Services.ItemRosters.Handlers.Events
{
    /// <summary>
    /// Handles ItemRosterUpdated on client.
    /// </summary>
    internal class ItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IMobilePartyRegistry mobilePartyRegistry;
        private readonly ILogger logger;

        public ItemRosterUpdatedHandler(IMessageBroker messageBroker, IBinaryPackageFactory binaryPackageFactory, IMobilePartyRegistry mobilePartyRegistry)
        {
            if (ModInformation.IsServer)
                return;

            this.messageBroker = messageBroker;
            this.binaryPackageFactory = binaryPackageFactory;
            this.mobilePartyRegistry = mobilePartyRegistry;

            logger = LogManager.GetLogger<ItemRosterUpdatedHandler>();

            messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdated> payload)
        {
            var package_ee = BinaryFormatterSerializer.Deserialize<EquipmentElementBinaryPackage>(payload.What.EquipmentElement);
            var equipmentElement = package_ee.Unpack<EquipmentElement>(binaryPackageFactory);


            if (mobilePartyRegistry.TryGetValue(payload.What.PartyBaseId, out var party))
            {
                party.ItemRoster.AddToCounts(equipmentElement, payload.What.Number);
            } else
            {
                logger.Error("Failed to update mobile party's ItemRoster, party not found");
            }

            try
            {
                Campaign.Current.Settlements.Find((s) =>
                {
                    return s.StringId == payload.What.PartyBaseId;
                }).ItemRoster.AddToCounts(equipmentElement, payload.What.Number);
            } catch(Exception e)
            {
                logger.Error("Failed to update settlement's ItemRoster: " + e.Message);
            }
        }

        public void Dispose()
        {
            if (ModInformation.IsServer)
                return;

            messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        }
    }
}
