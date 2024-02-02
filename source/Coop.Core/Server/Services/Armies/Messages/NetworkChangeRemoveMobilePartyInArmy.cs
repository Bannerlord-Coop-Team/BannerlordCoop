using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Armies.Messages
{
    /// <summary>
    /// Server sends this data when a Army called OnRemovePartyInternal
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeRemoveMobilePartyInArmy : IEvent
    {
        [ProtoMember(1)]
        public string MobilePartyId { get; }
        [ProtoMember(2, IsRequired = true)]
        public string LeaderMobilePartyId { get; }

        public NetworkChangeRemoveMobilePartyInArmy(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }
    }
}
