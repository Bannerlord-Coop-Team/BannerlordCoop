using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;
internal record PartyComponentMobilePartyChanged(PartyComponent Component, MobileParty Party) : IEvent
{
    public PartyComponent Component { get; } = Component;
    public MobileParty Party { get; } = Party;
}
