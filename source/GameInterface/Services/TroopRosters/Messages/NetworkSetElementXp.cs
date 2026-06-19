using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetElementXp : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly int Index;

    [ProtoMember(3)]
    public readonly int Number;

    public NetworkSetElementXp(string troopRosterId, int index, int number)
    {
        TroopRosterId = troopRosterId;
        Index = index;
        Number = number;
    }
}
