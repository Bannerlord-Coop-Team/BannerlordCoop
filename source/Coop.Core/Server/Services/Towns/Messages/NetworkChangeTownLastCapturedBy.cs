using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes LastCapturedBy property.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownLastCapturedBy : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public string ClanId { get; }

        public NetworkChangeTownLastCapturedBy(string townId, string clanId)
        {
            TownId = townId;
            ClanId = clanId;
        }
    }
}
