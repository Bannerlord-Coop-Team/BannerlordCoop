using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Client] Keeps the AI fleet attempts the server passes on, so a launcher opening a battle later caps its fleet against them;
/// the server keeps and passes them on in ServerBattleShipFateHandler.
/// </summary>
internal class NavalFleetRecordHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INavalFleetRecords records;
    private readonly IObjectManager objectManager;

    // Takes the client's records and the object manager that tells which of their battles are still open.
    public NavalFleetRecordHandler(IMessageBroker messageBroker, INavalFleetRecords records, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.records = records;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
    }

    // Drops the subscription.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNpcFleetAttempted>(Handle_NetworkNpcFleetAttempted);
    }

    // [Poller] Queues the attempt on the game thread, ahead of any later join or mission open the server sent after it.
    private void Handle_NetworkNpcFleetAttempted(MessagePayload<NetworkNpcFleetAttempted> payload)
    {
        if (ModInformation.IsServer) return;

        var attempt = payload.What;
        GameThread.RunSafe(() => Keep(attempt), context: nameof(Handle_NetworkNpcFleetAttempted));
    }

    // [Game thread] Keeps the attempt, dropping the ones of battles that ended, since a client never sees their finalize event.
    internal void Keep(NetworkNpcFleetAttempted attempt)
    {
        foreach (var mapEventId in records.All.Select(recorded => recorded.InstanceId).Distinct().ToList())
        {
            if (objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent) && !mapEvent.IsFinalized) continue;

            records.Forget(mapEventId);
        }

        records.Record(attempt);
    }
}
