using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct PartyComponentLeaderChanged : IEvent
{
    public readonly PartyComponent Instance;
    public readonly Hero NewLeader;

    public PartyComponentLeaderChanged(
        PartyComponent instance,
        Hero newLeader)
    {
        Instance = instance;
        NewLeader = newLeader;
    }
}
