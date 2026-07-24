using Common.Messaging;

namespace Coop.Core.Client.Messages;

public class NetworkGameSaveDataProgress : IEvent
{
    public int PacketsRemaining { get; }

    public NetworkGameSaveDataProgress(int packetsRemaining)
    {
        PacketsRemaining = packetsRemaining;
    }
}
