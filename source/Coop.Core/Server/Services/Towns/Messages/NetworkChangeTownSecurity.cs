using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes Security.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownSecurity : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public float Security { get; }

        public NetworkChangeTownSecurity(string townId, float security)
        {
            TownId = townId;
            Security = security;
        }
    }
}
