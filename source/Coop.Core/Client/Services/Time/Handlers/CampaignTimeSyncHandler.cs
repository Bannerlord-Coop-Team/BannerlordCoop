using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Interfaces;

namespace Coop.Core.Client.Services.Time.Handlers;

/// <summary>
/// Receives authoritative campaign time from the server and paces the client's
/// campaign simulation toward it.
/// </summary>
public class CampaignTimeSyncHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;

    public CampaignTimeSyncHandler(IMessageBroker messageBroker, IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.messageBroker = messageBroker;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;

        messageBroker.Subscribe<CampaignTimeUpdated>(Handle_CampaignTimeUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTimeUpdated>(Handle_CampaignTimeUpdated);
    }

    internal void Handle_CampaignTimeUpdated(MessagePayload<CampaignTimeUpdated> obj)
    {
        var serverTicks = obj.What.ServerTicks;

        mapTimeTrackerInterface.SyncCampaignTime(serverTicks);
    }
}
