using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

internal readonly struct DestroyPartyApplied : IEvent
{
    public readonly PartyBase VictoriousPartyBase;
    public readonly MobileParty DefeatedParty;

    public DestroyPartyApplied(PartyBase victorousPartyBase, MobileParty defeatedParty)
    {
        VictoriousPartyBase = victorousPartyBase;
        DefeatedParty = defeatedParty;
    }
}
