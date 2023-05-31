using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUpdatePartyAiBehavior : ICommand
    {
        [ProtoMember(1)]
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public NetworkUpdatePartyAiBehavior(AiBehaviorUpdateData data)
        {
            BehaviorUpdateData = data;
        }
    }
}
