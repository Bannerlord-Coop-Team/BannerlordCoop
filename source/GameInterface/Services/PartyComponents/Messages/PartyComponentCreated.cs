using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;
internal record PartyComponentCreated(PartyComponent Instance) : IEvent
{
    public PartyComponent Instance { get; } = Instance;
}
