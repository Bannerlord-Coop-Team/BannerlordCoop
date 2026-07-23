using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct DonateToGarrison : ICommand
{
    [ProtoMember(1)]
    public readonly string CurrentSettlementId;

    [ProtoMember(2)]
    public readonly string LeftMemberRosterId;

    public DonateToGarrison(
        string currentSettlementId,
        string leftMemberRosterId)
    {
        CurrentSettlementId = currentSettlementId;
        LeftMemberRosterId = leftMemberRosterId;
    }
}