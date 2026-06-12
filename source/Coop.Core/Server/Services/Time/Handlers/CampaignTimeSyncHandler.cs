using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Time.Interfaces;
using Serilog;
using System;
using System.Timers;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Periodically broadcasts the authoritative campaign time to all clients.
/// </summary>
/// <remarks>
/// Once per second the server reads the current <c>MapTimeTracker</c> tick value and
/// sends a <see cref="CampaignTimeUpdated"/> message to every connected client.
/// </remarks>
public class CampaignTimeSyncHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignTimeSyncHandler>();

    private const double PublishIntervalMs = 1000d;

    private readonly INetwork network;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;

    private readonly Timer publishTimer;

    public CampaignTimeSyncHandler(INetwork network, IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.network = network;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;

        publishTimer = new Timer(PublishIntervalMs) { AutoReset = true };
        publishTimer.Elapsed += PublishCampaignTime;
        publishTimer.Start();
    }

    public void Dispose()
    {
        publishTimer.Elapsed -= PublishCampaignTime;
        publishTimer.Stop();
        publishTimer.Dispose();
    }

    private void PublishCampaignTime(object sender, ElapsedEventArgs e)
    {
        // The timer fires on a thread pool thread; reading campaign time and broadcasting must run
        // on the game loop thread like every other game operation, so marshal the work onto it.
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                // No campaign loaded yet, nothing authoritative to broadcast.
                if (mapTimeTrackerInterface.TryGetCurrentTicks(out long currentTicks) == false) return;

                network.SendAll(new CampaignTimeUpdated(currentTicks));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to broadcast {message}", nameof(CampaignTimeUpdated));
            }
        });
    }
}
