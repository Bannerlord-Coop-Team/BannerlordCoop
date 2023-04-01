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
        }
    }
}
