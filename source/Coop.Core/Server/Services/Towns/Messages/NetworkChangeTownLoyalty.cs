using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes Loyalty.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownLoyalty : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public float Loyalty { get; }

        public NetworkChangeTownLoyalty(string townId, float loyalty)
        {
            TownId = townId;
            Loyalty = loyalty;
        }
    }
}
