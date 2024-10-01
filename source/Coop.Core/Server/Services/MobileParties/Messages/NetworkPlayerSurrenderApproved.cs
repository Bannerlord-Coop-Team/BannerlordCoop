using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Player surrender is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkPlayerSurrenderApproved : ICommand
    {
        [ProtoMember(1)]
        public string CaptorPartyId { get; }

        public NetworkPlayerSurrenderApproved(string captorPartyId)
        {
            CaptorPartyId = captorPartyId;
        }
    }
}
