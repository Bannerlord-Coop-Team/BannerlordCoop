using Common.Messaging;
using GameInterface.Services.MapEventParties;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ReleaseAndTakePrisoners : ICommand
{
    [ProtoMember(1)]
    public readonly FlattenedTroop[] TakenPrisonerRoster;

    [ProtoMember(2)]
    public readonly FlattenedTroop[] ReleasedPrisonerRoster;

    public ReleaseAndTakePrisoners(
        FlattenedTroop[] takenPrisonerRoster,
        FlattenedTroop[] releasedPrisonerRoster)
    {
        TakenPrisonerRoster = takenPrisonerRoster;
        ReleasedPrisonerRoster = releasedPrisonerRoster;
    }
}