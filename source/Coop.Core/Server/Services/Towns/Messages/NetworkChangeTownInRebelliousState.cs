using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes rebellious state.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownInRebelliousState : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2, IsRequired = true)]
        public bool InRebelliousState { get; }

        public NetworkChangeTownInRebelliousState(string townId, bool inRebelliousState)
        {
            TownId = townId;
            InRebelliousState = inRebelliousState;
        }
    }
}
