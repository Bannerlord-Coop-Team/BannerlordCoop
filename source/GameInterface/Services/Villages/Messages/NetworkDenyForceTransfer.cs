using Common.Messaging;
using GameInterface.Services.Villages.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkDenyForceTransfer : ICommand
{
    [ProtoMember(1)]
    public readonly VillageHostileAction Action;
    [ProtoMember(2)]
    public readonly string SettlementId;
    [ProtoMember(3)]
    public readonly VillageHostileActionDeniedReason Reason;

    public NetworkDenyForceTransfer(VillageHostileAction action, string settlementId, VillageHostileActionDeniedReason reason)
    {
        Action = action;
        SettlementId = settlementId;
        Reason = reason;
    }
}
