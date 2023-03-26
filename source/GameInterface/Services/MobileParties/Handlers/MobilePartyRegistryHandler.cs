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

            _messageBroker.Subscribe<RegisterAllParties>(HandleRegisterParties);
        }

        private void HandleRegisterParties(MessagePayload<RegisterAllParties> obj)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if(objectManager == null) return;

            foreach(var party in objectManager.MobileParties)
            {
                _registry.RegisterNewObject(party);
            }

            _messageBroker.Publish(this, new PartiesRegistered());
        }
    }
}
