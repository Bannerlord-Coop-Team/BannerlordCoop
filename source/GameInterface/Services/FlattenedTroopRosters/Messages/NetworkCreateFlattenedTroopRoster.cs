using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.FlattenedTroopRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateFlattenedTroopRoster : ICommand
    {
        public string FlattenedTroopRosterId { get; }
        public int Count { get; }

        public NetworkCreateFlattenedTroopRoster(string flattenedTroopRosterId, int count)
        {
            FlattenedTroopRosterId = flattenedTroopRosterId;
            Count = count;
        }
    }
}
