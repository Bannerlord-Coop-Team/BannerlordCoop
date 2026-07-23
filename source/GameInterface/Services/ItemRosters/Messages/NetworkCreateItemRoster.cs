using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.ItemRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateItemRoster : ICommand
    {
        [ProtoMember(1)]
        public string RosterId { get; }

        public NetworkCreateItemRoster(string rosterId)
        {
            RosterId = rosterId;
        }
    }
}
