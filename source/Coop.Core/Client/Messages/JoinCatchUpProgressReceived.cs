using Common.Messaging;

namespace Coop.Core.Client.Messages;

public record JoinCatchUpProgressReceived : IEvent
{
    public int PacketsRemaining { get; }

    public JoinCatchUpProgressReceived(int packetsRemaining)
    {
        PacketsRemaining = packetsRemaining;
    }
}
