using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Armies.Messages
{
    /// <summary>
    /// Server sends this data when a Army called OnAddPartyInternal
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeAddMobilePartyInArmy : IEvent
    {
        [ProtoMember(1)]
        public string MobilePartyId { get; }
        [ProtoMember(2, IsRequired = true)]
        public string LeaderMobilePartyId { get; }

        public NetworkChangeAddMobilePartyInArmy(string mobilePartyId, string leaderMobilePartyId)
        {
            MobilePartyId = mobilePartyId;
            LeaderMobilePartyId = leaderMobilePartyId;
        }
    }
}