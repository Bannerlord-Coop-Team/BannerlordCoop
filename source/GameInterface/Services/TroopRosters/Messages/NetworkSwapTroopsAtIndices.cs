using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSwapTroopsAtIndices : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly int FirstIndex;

    [ProtoMember(3)]
    public readonly int SecondIndex;

    public NetworkSwapTroopsAtIndices(string troopRosterId, int firstIndex, int secondIndex)
    {
        TroopRosterId = troopRosterId;
        FirstIndex = firstIndex;
        SecondIndex = secondIndex;
    }
}
