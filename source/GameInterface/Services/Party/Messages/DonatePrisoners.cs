using Common.Messaging;
using GameInterface.Services.MapEventParties;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct DonatePrisoners : ICommand
{
    [ProtoMember(1)]
    public readonly FlattenedTroop[] RightSidePrisonerRoster;

    [ProtoMember(2)]
    public readonly string CurrentSettlementId;

    [ProtoMember(3)]
    public readonly string RightPartyId;

    public DonatePrisoners(
        FlattenedTroop[] rightSidePrisonerRoster,
        string currentSettlementId,
        string rightPartyId)
    {
        RightSidePrisonerRoster = rightSidePrisonerRoster;
        CurrentSettlementId = currentSettlementId;
        RightPartyId = rightPartyId;
    }
}