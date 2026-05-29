using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract]
internal readonly struct NetworkClearTroopRoster : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    public NetworkClearTroopRoster(string troopRosterId)
    {
        TroopRosterId = troopRosterId;
    }
}
