using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShiftTroopToIndex : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly int TroopIndex;

    [ProtoMember(3)]
    public readonly int TargetIndex;

    public NetworkShiftTroopToIndex(string troopRosterId, int troopIndex, int targetIndex)
    {
        TroopRosterId = troopRosterId;
        TroopIndex = troopIndex;
        TargetIndex = targetIndex;
    }
}
