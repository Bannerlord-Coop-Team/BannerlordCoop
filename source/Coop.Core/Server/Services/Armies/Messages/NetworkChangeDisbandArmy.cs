using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Armies.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeDisbandArmy : IEvent
    {
        [ProtoMember(1)]
        public string ArmyId { get; }
        [ProtoMember(2, IsRequired = true)]
        public string Reason { get; }

        public NetworkChangeDisbandArmy(string armyId, string reason)
        {
            ArmyId = armyId;
            Reason = reason;
        }
    }
}