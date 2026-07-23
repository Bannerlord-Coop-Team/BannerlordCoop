using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct PrisonerTaken : IEvent
{
    public readonly PartyBase CapturerParty;
    public readonly Hero PrisonerHero;
    /// <summary>
    /// The prisoner's party at the moment of capture. The capture clears the hero's
    /// <c>PartyBelongedTo</c>, so handlers that need to deactivate the party must read it from here.
    /// </summary>
    public readonly MobileParty PrisonerParty;

    public PrisonerTaken(PartyBase capturerParty, Hero prisonerHero, MobileParty prisonerParty)
    {
        CapturerParty = capturerParty;
        PrisonerHero = prisonerHero;
        PrisonerParty = prisonerParty;
    }
}
