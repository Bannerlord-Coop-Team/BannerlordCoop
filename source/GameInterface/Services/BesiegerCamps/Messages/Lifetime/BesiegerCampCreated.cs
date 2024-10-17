using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

internal class BesiegerCampCreated : IEvent
{
    public BesiegerCampCreated(BesiegerCamp instance)
    {
        Instance = instance;
    }
    public BesiegerCamp Instance { get; }
}