using Common;
using Common.Messaging;
using GameInterface.Services.Ships.Messages;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Client] Tracks the AI fleet attempts the server recorded, so a launcher opening one of their battles later caps its fleet
/// against them; ServerNavalNpcFleetAttemptHandler records and sends them.
/// </summary>
internal class NavalNpcFleetAttemptHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INavalNpcFleetAttemptTracker attempts;

    // Takes the client's attempt tracker.
    public NavalNpcFleetAttemptHandler(IMessageBroker messageBroker, INavalNpcFleetAttemptTracker attempts)
    {
        this.messageBroker = messageBroker;
        this.attempts = attempts;

        messageBroker.Subscribe<NetworkNpcFleetAttemptRecorded>(Handle_NetworkNpcFleetAttemptRecorded);
    }

    // Drops the subscription.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNpcFleetAttemptRecorded>(Handle_NetworkNpcFleetAttemptRecorded);
    }

    // [Poller] Queues the attempt on the game thread, ahead of any later join or mission open the server sent after it.
    private void Handle_NetworkNpcFleetAttemptRecorded(MessagePayload<NetworkNpcFleetAttemptRecorded> payload)
    {
        var attempt = payload.What;
        GameThread.RunSafe(() => attempts.Record(attempt), context: nameof(Handle_NetworkNpcFleetAttemptRecorded));
    }
}
