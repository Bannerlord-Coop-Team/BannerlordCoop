using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract]
internal readonly struct NetworkRemoveZeroCounts : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    public NetworkRemoveZeroCounts(string troopRosterId)
    {
        TroopRosterId = troopRosterId;
    }
}
