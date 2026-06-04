using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Messages;

public readonly struct PrisonersReleasedAndTaken : IEvent
{
    public readonly FlattenedTroopRoster TakenPrisonerRoster;
    public readonly FlattenedTroopRoster ReleasedPrisonerRoster;

    public PrisonersReleasedAndTaken(
        FlattenedTroopRoster takenPrisonerRoster,
        FlattenedTroopRoster releasedPrisonerRoster)
    {
        TakenPrisonerRoster = takenPrisonerRoster;
        ReleasedPrisonerRoster = releasedPrisonerRoster;
    }
}