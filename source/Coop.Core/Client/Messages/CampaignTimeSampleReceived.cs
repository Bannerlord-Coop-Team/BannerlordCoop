using Common.Messaging;

namespace Coop.Core.Client.Messages;

public record CampaignTimeSampleReceived : IEvent
{
    public int JoinPacketsRemaining { get; }

    public CampaignTimeSampleReceived(int joinPacketsRemaining = -1) =>
        JoinPacketsRemaining = joinPacketsRemaining;
}
