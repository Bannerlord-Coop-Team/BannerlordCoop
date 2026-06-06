using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Interfaces;
using Serilog;

namespace Coop.Core.Client.Services.Time.Handlers;

/// <summary>
/// Receives authoritative campaign time from the server and smoothly corrects
/// the client's local campaign time toward it.
/// </summary>
public class CampaignTimeSyncHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignTimeSyncHandler>();

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

        Logger.Verbose("Client correcting campaign time toward server tick {ticks}", serverTicks);

        mapTimeTrackerInterface.SyncCampaignTime(serverTicks);
    }
}
