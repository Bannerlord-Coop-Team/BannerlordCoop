using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct DoManageGarrison : ICommand
{
    [ProtoMember(1)]
    public readonly string CurrentSettlementId;

    [ProtoMember(2)]
    public readonly string LeftMemberRosterId;

    [ProtoMember(3)]
    public readonly string LeftPrisonerRosterId;

    public DoManageGarrison(
        string currentSettlementId,
        string leftMemberRosterId,
        string leftPrisonerRosterId)
    {
        CurrentSettlementId = currentSettlementId;
        LeftMemberRosterId = leftMemberRosterId;
        LeftPrisonerRosterId = leftPrisonerRosterId;
    }
}