using Common.Messaging;

namespace GameInterface.Services.CampaignService.Messages;
public record CampaignTimeChanged : IEvent
{
    public long NumTicks { get; }
    public long DeltaTime { get; }

    public CampaignTimeChanged(long numTicks, long deltaTime)
    {
        NumTicks = numTicks;
        DeltaTime = deltaTime;
    }
}
