using Common.Messaging;
using GameInterface.Services.PartyComponents.Data;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;
internal class PartyComponentCreated : IEvent
{
    public PartyComponent Instance { get; }
    public PartyComponentCreated(PartyComponent instance)
    {
        Instance = instance;
    }
}
