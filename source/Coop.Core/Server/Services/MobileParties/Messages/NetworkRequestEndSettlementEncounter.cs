using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting a settlement encounter to end
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestEndSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public uint PartyId { get; }

    [ProtoMember(2)]
    public SettlementLeaveType LeaveType { get; }

    public NetworkRequestEndSettlementEncounter(uint partyId, SettlementLeaveType leaveType = SettlementLeaveType.Leave)
    {
        PartyId = partyId;
        LeaveType = leaveType;
    }
}
