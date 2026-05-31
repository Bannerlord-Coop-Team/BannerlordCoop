using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct PrisonerTaken : IEvent
{
    public readonly PartyBase CapturerParty;
    public readonly Hero PrisonerHero;

    public PrisonerTaken(PartyBase capturerParty, Hero prisonerHero)
    {
        CapturerParty = capturerParty;
        PrisonerHero = prisonerHero;
    }
}
