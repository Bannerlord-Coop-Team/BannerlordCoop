using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct PartyComponentMobilePartyUpdated : IEvent
{
    public readonly PartyComponent Instance;
    public readonly MobileParty MobileParty;

    public PartyComponentMobilePartyUpdated(PartyComponent instance, MobileParty mobileParty)
    {
        Instance = instance;
        MobileParty = mobileParty;
    }
}
