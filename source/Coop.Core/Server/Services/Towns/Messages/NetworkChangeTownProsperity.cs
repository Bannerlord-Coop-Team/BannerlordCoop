using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes Prosperity.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownProsperity: IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public float Prosperity { get; }

        public NetworkChangeTownProsperity(string townId, float prosperity)
        {
            TownId = townId;
            Prosperity = prosperity;
        }
    }
}
