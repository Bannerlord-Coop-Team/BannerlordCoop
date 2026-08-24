using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Leave;

internal readonly struct PartyRemovedFromMapEvent : IEvent
{
    public MobileParty RemovedParty { get; }

    public PartyRemovedFromMapEvent(MobileParty removedParty)
    {
        RemovedParty = removedParty;
    }
}
