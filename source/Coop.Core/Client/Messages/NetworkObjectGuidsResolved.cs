using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using ProtoBuf;

namespace Coop.Core.Client.Messages
{
    [ProtoContract]
    public readonly struct NetworkObjectGuidsResolved : INetworkEvent
    {
        [ProtoMember(1)]
        public string dasf;

        public NetworkObjectGuidsResolved(ObjectGuidsResolved objectGuids) : this()
        {
            objectGuids.TransactionID
        }
    }
}
