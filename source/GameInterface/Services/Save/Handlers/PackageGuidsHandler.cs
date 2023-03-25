using Common.Messaging;
using GameInterface.Services.Heroes;
using GameInterface.Services.Registry;
using GameInterface.Services.Save.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Handlers
{
    internal class PackageGuidsHandler : IHandler
    {
        private readonly IMessageBroker _messageBroker;
        private readonly IMobilePartyRegistry _partyRegistry;
        private readonly IHeroRegistry _heroRegistry;
        private readonly IControlledHeroRegistry _controlledHeroRegistry;

        public PackageGuidsHandler(
            IMessageBroker messageBroker,
            IMobilePartyRegistry partyRegistry,
            IHeroRegistry heroRegistry,
            IControlledHeroRegistry controlledHeroRegistry)
        {
            _messageBroker = messageBroker;
            _partyRegistry = partyRegistry;
            _heroRegistry = heroRegistry;
            _controlledHeroRegistry = controlledHeroRegistry;

            messageBroker.Subscribe<PackageObjectGuids>(Handle);
        }

        private void Handle(MessagePayload<PackageObjectGuids> obj)
        {
            Dictionary<string, Guid> partyIds = _partyRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);
            Dictionary<string, Guid> heroIds = _heroRegistry.ToDictionary(kvp => kvp.Value.StringId, kvp => kvp.Key);

            var response = new ObjectGuidsPackaged(
                obj.What.TransactionID,
                Campaign.Current?.UniqueGameId,
                _controlledHeroRegistry.ControlledHeros,
                partyIds,
                heroIds);

            _messageBroker.Publish(this, response);
            
        }
    }
}
