using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    // TODO: Find more descriptive name - or assign OwnerId in MobilePartyRegistry in another way.
    public record RegisterOwnerId : IEvent
    {
        public Guid OwnerId { get; set; }

        public RegisterOwnerId(Guid ownerId)
        {
            this.OwnerId = ownerId;
        }
    }
}
