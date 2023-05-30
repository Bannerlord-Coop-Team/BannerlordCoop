using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    public record RegisterInstanceId : IEvent
    {
        public Guid InstanceId { get; set; }

        public RegisterInstanceId(Guid instanceId)
        {
            InstanceId = instanceId;
        }
    }
}
