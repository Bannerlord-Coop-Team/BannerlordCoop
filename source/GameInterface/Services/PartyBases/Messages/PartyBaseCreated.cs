using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Messages;

internal class PartyBaseCreated : IEvent
{
    public PartyBaseCreated(PartyBase instance)
    {
        Instance = instance;
    }

    public PartyBase Instance { get; }
}
