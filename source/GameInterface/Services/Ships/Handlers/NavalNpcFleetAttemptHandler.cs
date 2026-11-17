using Common;
using Common.Messaging;
using GameInterface.Services.Ships.Messages;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Client] Tracks the AI fleet attempts the server recorded, and drops a battle's when the server forgets them, so a launcher
/// opening the battle later caps its fleet against the current ones; ServerNavalNpcFleetAttemptHandler sends both.
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
        messageBroker.Subscribe<NetworkNpcFleetAttemptsForgotten>(Handle_NetworkNpcFleetAttemptsForgotten);
    }

    // Drops the subscriptions.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNpcFleetAttemptRecorded>(Handle_NetworkNpcFleetAttemptRecorded);
        messageBroker.Unsubscribe<NetworkNpcFleetAttemptsForgotten>(Handle_NetworkNpcFleetAttemptsForgotten);
    }

    // [Poller] Queues the attempt on the game thread, ahead of any later join or mission open the server sent after it.
    private void Handle_NetworkNpcFleetAttemptRecorded(MessagePayload<NetworkNpcFleetAttemptRecorded> payload)
    {
        var attempt = payload.What;
        GameThread.RunSafe(() => attempts.Record(attempt), context: nameof(Handle_NetworkNpcFleetAttemptRecorded));
    }

    // [Poller] Queues dropping the battle's attempts on the game thread, ahead of the re-engaged mission's open and attempts.
    private void Handle_NetworkNpcFleetAttemptsForgotten(MessagePayload<NetworkNpcFleetAttemptsForgotten> payload)
    {
        string mapEventId = payload.What.InstanceId;
        GameThread.RunSafe(() => attempts.Forget(mapEventId), context: nameof(Handle_NetworkNpcFleetAttemptsForgotten));
    }
}
