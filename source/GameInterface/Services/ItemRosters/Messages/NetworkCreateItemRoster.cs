using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.ItemRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateItemRoster : ICommand
    {
        [ProtoMember(1)]
        public string RosterId { get; }
        [ProtoMember(2)]
        public uint Handle { get; }

        public NetworkCreateItemRoster(string rosterId, uint handle)
        {
            RosterId = rosterId;
            Handle = handle;
        }
    }
}
