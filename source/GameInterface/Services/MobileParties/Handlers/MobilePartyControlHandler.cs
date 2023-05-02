using Common.Messaging;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class MobilePartyControlHandler : IHandler
    {
        private readonly IMessageBroker _messageBroker;
        private readonly IMobilePartyInterface _partyInterface;

        public MobilePartyControlHandler(IMessageBroker messageBroker, IMobilePartyInterface partyInterface)
        {
            _messageBroker = messageBroker;
            _partyInterface = partyInterface;

            _messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        }

        private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
        {
            var ownerId = obj.What.OwnerId;
            _partyInterface.RegisterAllPartiesAsControlled(ownerId);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        }
    }
}
