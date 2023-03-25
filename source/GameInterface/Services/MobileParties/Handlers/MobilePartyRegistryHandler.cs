using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyRegistryHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyRegistryHandler>();

        private readonly IMessageBroker _messageBroker;
        private readonly IMobilePartyRegistry _registry;

        public MobilePartyRegistryHandler(IMessageBroker messageBroker,
                                          IMobilePartyRegistry registry)
        {
            _messageBroker = messageBroker;
            _registry = registry;

            _messageBroker.Subscribe<RegisterParties>(HandleRegisterParties);
            _messageBroker.Subscribe<RegisterPartiesWithStringIds>(HandleRegisterPartiesWithStringIds);
        }

        private void HandleRegisterParties(MessagePayload<RegisterParties> obj)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if(objectManager == null) return;

            foreach(var party in objectManager.MobileParties)
            {
                _registry.RegisterNewObject(party);
            }

            _messageBroker.Publish(this, new PartiesRegistered());
        }

        private void HandleRegisterPartiesWithStringIds(MessagePayload<RegisterPartiesWithStringIds> obj)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null) return;

            var stringIdToGuidDict = obj.What.AssociatedStringIdValues;

            // Error recording lists
            var unregisteredParties = new List<string>();
            var badGuidParties = new List<string>();

            foreach (var party in objectManager.MobileParties)
            {
                if(stringIdToGuidDict.TryGetValue(party.StringId, out Guid id))
                {
                    if(id != Guid.Empty)
                    {
                        _registry.RegisterExistingObject(id, party);
                    }
                    else
                    {
                        // Parties with empty guids
                        badGuidParties.Add(party.StringId);
                    }
                }
                else
                {
                    // Existing parties that don't exist in stringIds
                    unregisteredParties.Add(party.StringId);
                }
            }

            // Log any bad guids if they exist
            if(badGuidParties.IsEmpty() == false)
            {
                Logger.Error("The following parties had incorrect Guids: {parties}", badGuidParties);
            }

            // Log any unregistered parties if they exist
            if (unregisteredParties.IsEmpty() == false)
            {
                Logger.Error("The following parties were not registered: {parties}", unregisteredParties);
            }

            var transactionId = obj.What.TransactionID;
            _messageBroker.Publish(this, new PartiesRegistered(transactionId));
        }
    }
}
