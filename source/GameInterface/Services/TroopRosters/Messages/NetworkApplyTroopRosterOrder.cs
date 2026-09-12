using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkApplyTroopRosterOrder : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly TroopRosterOrderData OrderData;

    public NetworkApplyTroopRosterOrder(string troopRosterId, TroopRosterOrderData orderData)
    {
        TroopRosterId = troopRosterId;
        OrderData = orderData;
    }
}
